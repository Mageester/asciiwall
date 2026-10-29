using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AsciiWall
{
    public sealed class MonitorRect
    {
        public int X, Y, W, H;
        public bool Primary;
    }

    public enum WallState { Idle, Starting, Attached, Failed }

    /// <summary>
    /// Owns the browser process that renders the wallpaper and parents its window under the desktop icons.
    /// The browser lives in a job object, so closing (or crashing) the host takes it down too.
    /// </summary>
    public sealed class Wallpaper : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
        {
            public int cb;
            public string lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

        [StructLayout(LayoutKind.Sequential)]
        struct JBASIC
        {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinWs, MaxWs;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IOC { public ulong A, B, C, D, E, F; }

        [StructLayout(LayoutKind.Sequential)]
        struct JEXT { public JBASIC Basic; public IOC Io; public UIntPtr ProcMem, JobMem, PeakProc, PeakJob; }

        [StructLayout(LayoutKind.Sequential)]
        struct JACCT { public long UserTime, KernelTime, PeriodUser, PeriodKernel; public uint PageFaults, TotalProcesses, ActiveProcesses, Terminated; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateProcess(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
        [DllImport("kernel32.dll")] static extern uint ResumeThread(IntPtr h);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attr, string name);
        [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int cls, ref JEXT info, int len);
        [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr proc);
        [DllImport("kernel32.dll")] static extern bool TerminateJobObject(IntPtr job, uint code);
        [DllImport("kernel32.dll")] static extern bool QueryInformationJobObject(IntPtr job, int cls, out JACCT info, int len, IntPtr ret);
        [DllImport("kernel32.dll")] static extern bool QueryInformationJobObject(IntPtr job, int cls, IntPtr info, int len, IntPtr ret);
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

        const uint CREATE_SUSPENDED = 0x4;
        const int STARTF_USESHOWWINDOW = 1;
        const string Title = "AsciiWall Host";

        public volatile WallState State = WallState.Idle;
        public IntPtr Hwnd = IntPtr.Zero;
        public event Action Attached;

        readonly string profileDir;
        IntPtr job = IntPtr.Zero, procHandle = IntPtr.Zero, parent = IntPtr.Zero;
        Process proc;
        int generation;
        long lastCpuTicks;
        DateTime lastCpuAt;

        public Wallpaper(string profileDir) { this.profileDir = profileDir; }

        public bool Alive
        {
            get
            {
                try { return proc != null && !proc.HasExited; } catch (Exception) { return false; }
            }
        }

        /// <summary>Edge first (every Windows 10/11 install has it), then Chrome as a fallback.</summary>
        public static string FindBrowser()
        {
            List<string> c = new List<string>();
            string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)"), pf = Environment.GetEnvironmentVariable("ProgramFiles");
            string lad = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (pf86 != null) c.Add(Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"));
            if (pf != null) c.Add(Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe"));
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe"))
                {
                    object v = k == null ? null : k.GetValue(null);
                    if (v != null) c.Add(v.ToString());
                }
            }
            catch (Exception) { }
            if (pf != null) c.Add(Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"));
            if (pf86 != null) c.Add(Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"));
            if (lad != null) c.Add(Path.Combine(lad, @"Google\Chrome\Application\chrome.exe"));
            foreach (string p in c) if (File.Exists(p)) return p;
            return null;
        }

        /// <summary>Flags that keep the renderer lean and make sure a window parked behind the icons keeps animating.</summary>
        public static string BrowserFlags(string profile, bool edge)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("--user-data-dir=\"").Append(profile).Append("\" ");
            sb.Append("--no-first-run --no-default-browser-check --disable-sync --disable-extensions --disable-default-apps ");
            sb.Append("--disable-background-networking --disable-component-update --disable-breakpad --disable-crash-reporter ");
            sb.Append("--disable-translate --disable-hang-monitor --disable-session-crashed-bubble --hide-crash-restore-bubble --noerrdialogs ");
            sb.Append("--disable-pinch --overscroll-history-navigation=0 --password-store=basic --metrics-recording-only ");
            sb.Append("--disable-renderer-backgrounding --disable-backgrounding-occluded-windows --disable-site-isolation-trials ");
            sb.Append("--disable-features=CalculateNativeWinOcclusion,Translate,MediaRouter,OptimizationHints,AutofillServerCommunication ");
            if (edge) sb.Append("--edge-kiosk-type=fullscreen ");
            return sb.ToString();
        }

        public bool Start(string url)
        {
            Stop();
            string exe = FindBrowser();
            if (exe == null) { Log.Write("no Edge or Chrome found"); State = WallState.Failed; return false; }
            bool edge = exe.EndsWith("msedge.exe", StringComparison.OrdinalIgnoreCase);
            try { Directory.CreateDirectory(profileDir); } catch (Exception) { }

            job = CreateJobObject(IntPtr.Zero, null);
            JEXT ext = new JEXT();
            ext.Basic.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            SetInformationJobObject(job, 9, ref ext, Marshal.SizeOf(typeof(JEXT)));

            StringBuilder cmd = new StringBuilder();
            cmd.Append('"').Append(exe).Append("\" --kiosk ").Append(BrowserFlags(profileDir, edge)).Append('"').Append(url).Append('"');
            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
            si.dwFlags = STARTF_USESHOWWINDOW;
            si.wShowWindow = 7; // SW_SHOWMINNOACTIVE: never steal focus from what the user is doing
            PROCESS_INFORMATION pi;
            if (!CreateProcess(exe, cmd, IntPtr.Zero, IntPtr.Zero, false, CREATE_SUSPENDED, IntPtr.Zero, Path.GetDirectoryName(exe), ref si, out pi))
            {
                Log.Write("CreateProcess failed: " + Marshal.GetLastWin32Error());
                State = WallState.Failed;
                return false;
            }
            AssignProcessToJobObject(job, pi.hProcess);
            ResumeThread(pi.hThread);
            CloseHandle(pi.hThread);
            procHandle = pi.hProcess;
            try { proc = Process.GetProcessById(pi.dwProcessId); } catch (Exception) { proc = null; }
            lastCpuTicks = 0; lastCpuAt = DateTime.UtcNow;
            State = WallState.Starting;
            Hwnd = IntPtr.Zero;
            int gen = Interlocked.Increment(ref generation);
            uint pid = (uint)pi.dwProcessId;
            Thread t = new Thread(delegate() { AttachLoop(gen, pid); });
            t.IsBackground = true;
            t.Name = "wall-attach";
            t.Start();
            return true;
        }

        void AttachLoop(int gen, uint pid)
        {
            DateTime end = DateTime.UtcNow.AddSeconds(45);
            IntPtr win = IntPtr.Zero;
            while (DateTime.UtcNow < end && gen == generation)
            {
                win = FindMain(pid);
                if (win != IntPtr.Zero) break;
                Thread.Sleep(20);
            }
            if (gen != generation) return;
            if (win == IntPtr.Zero) { Log.Write("browser window never appeared"); State = WallState.Failed; return; }

            // The shell can be mid-restart at login; wait for the desktop to have its icon view.
            DateTime shellEnd = DateTime.UtcNow.AddSeconds(60);
            while (gen == generation && !ShellReady() && DateTime.UtcNow < shellEnd) Thread.Sleep(250);
            if (gen != generation) return;

            if (!Reparent(win)) { Log.Write("attach failed"); State = WallState.Failed; return; }
            Hwnd = win;

            // The title only exists once the page has loaded; confirm it is really ours.
            DateTime titleEnd = DateTime.UtcNow.AddSeconds(20);
            while (gen == generation && DateTime.UtcNow < titleEnd)
            {
                if (Native.TitleOf(win).IndexOf(Title, StringComparison.Ordinal) >= 0) break;
                Thread.Sleep(100);
            }
            if (gen != generation) return;
            State = WallState.Attached;
            Log.Write("attached hwnd=0x" + win.ToInt64().ToString("x") + " parent=0x" + parent.ToInt64().ToString("x"));
            Action a = Attached;
            if (a != null) a();
        }

        static IntPtr FindMain(uint pid)
        {
            IntPtr first = IntPtr.Zero, titled = IntPtr.Zero;
            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (Native.PidOf(h) != pid || Native.ClassOf(h) != "Chrome_WidgetWin_1") return true;
                if ((Native.GetStyle(h, Native.GWL_EXSTYLE) & Native.WS_EX_TOOLWINDOW) != 0) return true;
                if (first == IntPtr.Zero) first = h;
                if (Native.TitleOf(h).IndexOf(Title, StringComparison.Ordinal) >= 0) titled = h;
                return true;
            }, IntPtr.Zero);
            return titled != IntPtr.Zero ? titled : first;
        }

        public static bool ShellReady()
        {
            IntPtr pm = Native.FindWindow("Progman", null);
            return pm != IntPtr.Zero && Native.FindWindowEx(pm, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero || LegacyDefViewHost() != IntPtr.Zero;
        }

        static IntPtr LegacyDefViewHost()
        {
            IntPtr found = IntPtr.Zero;
            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (Native.ClassOf(h) == "WorkerW" && Native.FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) { found = h; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public static RECT DesktopRect()
        {
            RECT r = new RECT();
            IntPtr pm = Native.FindWindow("Progman", null);
            if (pm != IntPtr.Zero) Native.GetWindowRect(pm, out r);
            if (r.Width < 100 || r.Height < 100)
            {
                System.Drawing.Rectangle v = SystemInformation.VirtualScreen;
                r.Left = v.Left; r.Top = v.Top; r.Right = v.Right; r.Bottom = v.Bottom;
            }
            return r;
        }

        /// <summary>
        /// Windows 11 24H2 keeps the icon view and a worker window as children of Progman, so the browser
        /// goes in as a Progman child placed directly beneath the icons. Older builds use a sibling WorkerW.
        /// </summary>
        bool Reparent(IntPtr win)
        {
            IntPtr pm = Native.FindWindow("Progman", null);
            if (pm == IntPtr.Zero) return false;
            IntPtr res;
            Native.SendMessageTimeout(pm, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out res);

            IntPtr newParent = pm, after = Native.FindWindowEx(pm, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (after == IntPtr.Zero)
            {
                IntPtr host = LegacyDefViewHost();
                IntPtr worker = host == IntPtr.Zero ? IntPtr.Zero : Native.FindWindowEx(IntPtr.Zero, host, "WorkerW", null);
                if (worker == IntPtr.Zero) return false;
                newParent = worker;
                after = new IntPtr(1); // HWND_BOTTOM
            }

            long st = Native.GetStyle(win, Native.GWL_STYLE);
            st &= ~(Native.WS_POPUP | Native.WS_CAPTION | Native.WS_THICKFRAME | Native.WS_SYSMENU | Native.WS_MINIMIZEBOX | Native.WS_MAXIMIZEBOX | Native.WS_MINIMIZE);
            st |= Native.WS_CHILD | Native.WS_VISIBLE;
            Native.SetStyle(win, Native.GWL_STYLE, st);
            long ex = Native.GetStyle(win, Native.GWL_EXSTYLE);
            ex &= ~(Native.WS_EX_APPWINDOW | Native.WS_EX_WINDOWEDGE | Native.WS_EX_CLIENTEDGE);
            ex |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            Native.SetStyle(win, Native.GWL_EXSTYLE, ex);

            Native.SetParent(win, newParent);
            parent = newParent;
            Fit(win, after);
            Native.ShowWindow(win, Native.SW_SHOWNOACTIVATE);
            return Native.GetParent(win) == newParent;
        }

        void Fit(IntPtr win, IntPtr after)
        {
            RECT r;
            Native.GetWindowRect(parent, out r);
            Native.SetWindowPos(win, after, 0, 0, r.Width, r.Height, Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED | Native.SWP_SHOWWINDOW);
        }

        /// <summary>Re-fit after a display change so the window always covers the whole virtual desktop.</summary>
        public void Resize()
        {
            if (State != WallState.Attached || !Native.IsWindow(Hwnd)) return;
            RECT r;
            Native.GetWindowRect(parent, out r);
            Native.SetWindowPos(Hwnd, IntPtr.Zero, 0, 0, r.Width, r.Height, Native.SWP_NOACTIVATE | Native.SWP_NOMOVE | 0x4 /* SWP_NOZORDER */);
        }

        /// <summary>True while the window is still where we put it (an Explorer restart destroys it).</summary>
        public bool Healthy()
        {
            if (State != WallState.Attached) return true;
            if (!Alive || !Native.IsWindow(Hwnd)) return false;
            IntPtr pm = Native.FindWindow("Progman", null);
            return pm != IntPtr.Zero && Native.GetParent(Hwnd) == parent && (parent == pm || Native.IsWindow(parent));
        }

        public void Stop()
        {
            Interlocked.Increment(ref generation);
            State = WallState.Idle;
            Hwnd = IntPtr.Zero;
            if (job != IntPtr.Zero) { TerminateJobObject(job, 0); CloseHandle(job); job = IntPtr.Zero; }
            if (procHandle != IntPtr.Zero) { CloseHandle(procHandle); procHandle = IntPtr.Zero; }
            if (proc != null) { try { proc.Dispose(); } catch (Exception) { } proc = null; }
        }

        public void Dispose() { Stop(); }

        /// <summary>CPU use of the whole browser tree as a percentage of the machine, plus its memory in MB.</summary>
        public void Sample(out double cpuPercent, out double memMb, out int processes)
        {
            cpuPercent = 0; memMb = 0; processes = 0;
            if (job == IntPtr.Zero) return;
            JACCT a;
            if (QueryInformationJobObject(job, 1, out a, Marshal.SizeOf(typeof(JACCT)), IntPtr.Zero))
            {
                long total = a.UserTime + a.KernelTime;
                DateTime now = DateTime.UtcNow;
                if (lastCpuTicks > 0)
                {
                    double wall = (now - lastCpuAt).TotalSeconds * 1e7 * Environment.ProcessorCount;
                    if (wall > 0) cpuPercent = Math.Max(0, (total - lastCpuTicks) / wall * 100.0);
                }
                lastCpuTicks = total; lastCpuAt = now;
                processes = (int)a.ActiveProcesses;
            }
            IntPtr buf = Marshal.AllocHGlobal(8 + 8 * 128);
            try
            {
                if (QueryInformationJobObject(job, 3, buf, 8 + 8 * 128, IntPtr.Zero))
                {
                    int n = Marshal.ReadInt32(buf, 4);
                    for (int i = 0; i < n && i < 128; i++)
                    {
                        int pid = (int)Marshal.ReadInt64(buf, 8 + i * 8);
                        try { using (Process p = Process.GetProcessById(pid)) memMb += p.WorkingSet64 / 1048576.0; } catch (Exception) { }
                    }
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        public static void Monitors(List<MonitorRect> into)
        {
            into.Clear();
            RECT d = DesktopRect();
            foreach (Screen s in Screen.AllScreens)
            {
                MonitorRect m = new MonitorRect();
                m.X = s.Bounds.X - d.Left; m.Y = s.Bounds.Y - d.Top; m.W = s.Bounds.Width; m.H = s.Bounds.Height; m.Primary = s.Primary;
                into.Add(m);
            }
            into.Sort(delegate(MonitorRect a, MonitorRect b)
            {
                if (a.Primary != b.Primary) return a.Primary ? -1 : 1;
                return a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y);
            });
        }

        static readonly string[] ShellClasses = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow", "TopLevelWindowForOverflowXamlIsland" };

        /// <summary>
        /// A monitor counts as covered when one visible, opaque window fills nearly all of it
        /// (a maximised app, a full-screen game or video), so there is nothing to animate for.
        /// </summary>
        public static bool[] Covered(List<MonitorRect> mons)
        {
            bool[] cov = new bool[mons.Count];
            if (mons.Count == 0) return cov;
            RECT d = DesktopRect();
            int[] ax = new int[mons.Count], ay = new int[mons.Count], aw = new int[mons.Count], ah = new int[mons.Count];
            for (int i = 0; i < mons.Count; i++) { ax[i] = mons[i].X + d.Left; ay[i] = mons[i].Y + d.Top; aw[i] = mons[i].W; ah[i] = mons[i].H; }

            int qs;
            if (SHQueryUserNotificationState(out qs) == 0 && (qs == 2 || qs == 3 || qs == 4))
            {
                for (int i = 0; i < cov.Length; i++) cov[i] = true;
                return cov;
            }

            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
                RECT r;
                if (!Native.GetWindowRect(h, out r)) return true;
                int big = -1;
                for (int i = 0; i < cov.Length; i++)
                {
                    if (cov[i]) continue;
                    if (Overlap(r, ax[i], ay[i], aw[i], ah[i]) >= 0.80) { big = i; break; }
                }
                if (big < 0) return true;
                if (Native.IsCloaked(h)) return true;
                long ex = Native.GetStyle(h, Native.GWL_EXSTYLE);
                if ((ex & (Native.WS_EX_TOOLWINDOW | Native.WS_EX_TRANSPARENT)) != 0) return true;
                string cls = Native.ClassOf(h);
                if (Array.IndexOf(ShellClasses, cls) >= 0) return true;
                RECT v;
                if (!Native.VisibleBounds(h, out v)) return true;
                for (int i = 0; i < cov.Length; i++)
                    if (!cov[i] && Overlap(v, ax[i], ay[i], aw[i], ah[i]) >= 0.90) cov[i] = true;
                return true;
            }, IntPtr.Zero);
            return cov;
        }

        static double Overlap(RECT r, int x, int y, int w, int h)
        {
            int ox = Math.Min(r.Right, x + w) - Math.Max(r.Left, x), oy = Math.Min(r.Bottom, y + h) - Math.Max(r.Top, y);
            if (ox <= 0 || oy <= 0 || w <= 0 || h <= 0) return 0;
            return (double)ox * oy / ((double)w * h);
        }
    }
}
