using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("AsciiWall Setup")]
[assembly: AssemblyProduct("AsciiWall")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace AsciiWallSetup
{
    sealed class Options
    {
        public string Dir;
        public bool Startup = true, StartMenu = true, Desktop = false, Launch = true, RemoveData = false;
    }

    /// <summary>Everything the installer and uninstaller do to the machine, kept free of UI so /S can reuse it.</summary>
    static class Ops
    {
        public const string Version = "1.0.0";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AsciiWall";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static string DefaultDir { get { return Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), @"Programs\AsciiWall"); } }
        public static string DataDir { get { return Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "AsciiWall"); } }
        static string StartMenuLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "AsciiWall.lnk"); } }
        static string DesktopLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "AsciiWall.lnk"); } }

        /// <summary>The folder AsciiWall was installed to, from the uninstall entry, if there is one.</summary>
        public static string InstalledDir()
        {
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey)) { object v = k == null ? null : k.GetValue("InstallLocation"); return v == null ? null : v.ToString(); } }
            catch (Exception) { return null; }
        }

        static bool SafeDir(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            string full = Path.GetFullPath(dir).TrimEnd('\\');
            return full.Length > 12 && Path.GetPathRoot(full).TrimEnd('\\') != full && full.IndexOf("AsciiWall", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Asks a running AsciiWall to quit, then makes sure it has.</summary>
        public static void StopRunning(string dir)
        {
            string exe = Path.Combine(dir, "AsciiWall.exe");
            if (File.Exists(exe))
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(exe, "--quit");
                    psi.UseShellExecute = false; psi.CreateNoWindow = true;
                    using (Process p = Process.Start(psi)) { if (p != null) p.WaitForExit(5000); }
                }
                catch (Exception) { }
            }
            for (int i = 0; i < 25; i++)
            {
                List<Process> mine = Running(dir);
                if (mine.Count == 0) return;
                if (i >= 15) foreach (Process p in mine) { try { p.Kill(); } catch (Exception) { } }
                Thread.Sleep(200);
            }
        }

        static List<Process> Running(string dir)
        {
            List<Process> list = new List<Process>();
            string prefix = Path.GetFullPath(dir).TrimEnd('\\') + "\\";
            foreach (Process p in Process.GetProcessesByName("AsciiWall"))
            {
                try { if (p.MainModule.FileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) list.Add(p); }
                catch (Exception) { }
            }
            return list;
        }

        public static void Install(Options o, Action<int, string> progress)
        {
            string dir = Path.GetFullPath(o.Dir).TrimEnd('\\');
            if (!SafeDir(dir)) throw new InvalidOperationException("Please choose a folder with its own name, for example ...\\AsciiWall.");
            progress(3, "Closing AsciiWall if it is running...");
            Directory.CreateDirectory(dir);
            StopRunning(dir);

            string web = Path.Combine(dir, "web");
            if (Directory.Exists(web)) { try { Directory.Delete(web, true); } catch (IOException) { } }

            long total = 0;
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            using (ZipArchive z = new ZipArchive(s, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry e in z.Entries) total += e.Length;
                int n = 0, count = z.Entries.Count;
                foreach (ZipArchiveEntry e in z.Entries)
                {
                    n++;
                    string rel = e.FullName.Replace('/', '\\');
                    if (rel.EndsWith("\\") || rel.Length == 0) continue;
                    string dest = Path.GetFullPath(Path.Combine(dir, rel));
                    if (!dest.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    e.ExtractToFile(dest, true);
                    progress(5 + (int)(80.0 * n / count), "Copying " + Path.GetFileName(dest));
                }
            }

            progress(88, "Adding the uninstaller...");
            string self = Assembly.GetExecutingAssembly().Location, unin = Path.Combine(dir, "Uninstall.exe");
            if (!string.Equals(Path.GetFullPath(self), unin, StringComparison.OrdinalIgnoreCase)) File.Copy(self, unin, true);

            string exe = Path.Combine(dir, "AsciiWall.exe");
            progress(92, "Creating shortcuts...");
            if (o.StartMenu) Shortcut(StartMenuLink, exe, dir);
            else TryDelete(StartMenuLink);
            if (o.Desktop) Shortcut(DesktopLink, exe, dir);
            else TryDelete(DesktopLink);

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (o.Startup) k.SetValue("AsciiWall", "\"" + exe + "\" --startup");
                else k.DeleteValue("AsciiWall", false);
            }

            progress(96, "Registering with Windows...");
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "AsciiWall");
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", "AsciiWall");
                k.SetValue("InstallLocation", dir);
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("UninstallString", "\"" + unin + "\" /uninstall");
                k.SetValue("QuietUninstallString", "\"" + unin + "\" /uninstall /S");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("EstimatedSize", (int)(total / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
            progress(100, "Done");
        }

        public static void Uninstall(Options o, Action<int, string> progress, out string leftover)
        {
            leftover = null;
            string dir = o.Dir == null ? null : Path.GetFullPath(o.Dir).TrimEnd('\\');
            if (!SafeDir(dir)) throw new InvalidOperationException("Could not find where AsciiWall is installed.");
            progress(10, "Closing AsciiWall...");
            StopRunning(dir);

            progress(35, "Removing shortcuts and startup entry...");
            TryDelete(StartMenuLink); TryDelete(DesktopLink);
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true)) { if (k != null) k.DeleteValue("AsciiWall", false); } } catch (Exception) { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch (Exception) { }

            if (o.RemoveData)
            {
                progress(55, "Deleting your settings...");
                try { Directory.Delete(DataDir, true); }
                catch (Exception) { leftover = "Some settings files are still in use and were left in " + DataDir + "."; }
            }

            progress(75, "Removing program files...");
            string self = Assembly.GetExecutingAssembly().Location;
            foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFullPath(f), self, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(f); } catch (Exception) { }
            }
            CleanupDir = dir;
            progress(100, "Done");
        }

        /// <summary>This very file is still running during uninstall, so a helper removes the folder once we have exited.</summary>
        public static string CleanupDir;

        public static void RunCleanup()
        {
            if (CleanupDir == null || !SafeDir(CleanupDir)) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + CleanupDir + "\"");
                psi.CreateNoWindow = true; psi.UseShellExecute = false; psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch (Exception) { }
        }

        static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { } }

        static void Shortcut(string lnk, string target, string workDir)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(t);
            object link = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            Type lt = link.GetType();
            lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
            lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { workDir });
            lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { target + ",0" });
            lt.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "Live ASCII wallpapers" });
            lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }

        public static void Launch(string dir)
        {
            try { Process.Start(new ProcessStartInfo(Path.Combine(dir, "AsciiWall.exe")) { UseShellExecute = false, WorkingDirectory = dir }); } catch (Exception) { }
        }
    }

    /// <summary>A slowly rolling ASCII sea down the left edge of the window.</summary>
    sealed class Banner : Control
    {
        const string Ramp = " .,:;-=+*#%@";
        readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        Font font;
        Brush[] brushes;

        public Banner()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            tick.Interval = 66;
            tick.Tick += delegate { Invalidate(); };
            tick.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { tick.Dispose(); if (font != null) font.Dispose(); if (brushes != null) foreach (Brush b in brushes) b.Dispose(); }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.FromArgb(8, 10, 16));
            if (font == null)
            {
                font = new Font("Consolas", 8.5f);
                brushes = new Brush[16];
                for (int i = 0; i < brushes.Length; i++)
                {
                    float v = i / 15f;
                    brushes[i] = new SolidBrush(Color.FromArgb((int)(40 + 215 * v), (int)(70 + 170 * v), (int)(120 + 100 * v)));
                }
            }
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            SizeF m = g.MeasureString("M", font, 100, StringFormat.GenericTypographic);
            float cw = Math.Max(5, m.Width), ch = Math.Max(9, m.Height);
            int cols = (int)(Width / cw) + 1, rows = (int)(Height / ch) + 1;
            double t = clock.Elapsed.TotalSeconds;
            StringFormat sf = StringFormat.GenericTypographic;
            for (int y = 0; y < rows; y++)
            {
                double fade = Math.Min(1.0, 0.25 + y / (double)rows * 1.1);
                for (int x = 0; x < cols; x++)
                {
                    double v = 0.5 + 0.5 * Math.Sin(x * 0.33 + t * 1.2 + Math.Sin(y * 0.37 + t * 0.6) * 1.7 + y * 0.21);
                    v = Math.Pow(v, 1.6) * fade;
                    int ci = (int)(v * (Ramp.Length - 1));
                    if (ci <= 0) continue;
                    g.DrawString(Ramp[ci].ToString(), font, brushes[Math.Min(15, (int)(v * 15.99))], x * cw, y * ch, sf);
                }
            }
        }
    }

    sealed class Bar : Control
    {
        int value;
        public Bar() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }
        public int Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? Color.Black : Parent.BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Round(r, r.Height / 2))
            using (SolidBrush track = new SolidBrush(Color.FromArgb(30, 35, 50)))
                g.FillPath(track, p);
            int w = (int)(r.Width * value / 100.0);
            if (w > r.Height)
            {
                Rectangle f = new Rectangle(0, 0, w, r.Height);
                using (GraphicsPath p = Round(f, f.Height / 2))
                using (LinearGradientBrush b = new LinearGradientBrush(f, Color.FromArgb(124, 240, 201), Color.FromArgb(143, 184, 255), 0f))
                    g.FillPath(b, p);
            }
        }

        static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    sealed class SetupForm : Form
    {
        static readonly Color Bg = Color.FromArgb(14, 17, 25), Txt = Color.FromArgb(233, 236, 243), Mut = Color.FromArgb(141, 149, 168), Acc = Color.FromArgb(124, 240, 201), Field = Color.FromArgb(24, 28, 40);

        readonly bool uninstall;
        readonly float k;
        readonly Options opt = new Options();
        readonly Panel pageOptions = new Panel(), pageWork = new Panel(), pageDone = new Panel();
        readonly Label workStatus = new Label(), doneTitle = new Label(), doneText = new Label();
        readonly Bar bar = new Bar();
        TextBox path;
        CheckBox cStartup, cMenu, cDesktop, cLaunch, cData;
        Button doneButton;

        int S(int v) { return (int)Math.Round(v * k); }

        public SetupForm(bool uninstall, string dir)
        {
            this.uninstall = uninstall;
            using (Graphics g = CreateGraphics()) k = g.DpiX / 96f;
            opt.Dir = dir;
            Text = uninstall ? "Uninstall AsciiWall" : "AsciiWall Setup";
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Bg; ForeColor = Txt;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(720), S(440));
            try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch (Exception) { }

            Banner banner = new Banner();
            banner.Dock = DockStyle.Left; banner.Width = S(200);
            Controls.Add(banner);

            foreach (Panel p in new Panel[] { pageOptions, pageWork, pageDone })
            {
                p.Dock = DockStyle.Fill; p.BackColor = Bg; p.Padding = new Padding(S(32), S(30), S(32), S(24));
                Controls.Add(p);
                p.BringToFront();
            }
            banner.SendToBack();
            BuildOptions(); BuildWork(); BuildDone();
            Show(pageOptions);
        }

        void Show(Panel p)
        {
            pageOptions.Visible = p == pageOptions; pageWork.Visible = p == pageWork; pageDone.Visible = p == pageDone;
        }

        Label Text2(Control parent, string text, int x, int y, int w, int h, float size, bool bold, Color c)
        {
            Label l = new Label();
            l.Text = text; l.ForeColor = c; l.BackColor = Color.Transparent; l.AutoSize = false;
            l.Font = new Font("Segoe UI" + (bold ? " Semibold" : ""), size);
            l.SetBounds(S(x), S(y), S(w), S(h));
            parent.Controls.Add(l);
            return l;
        }

        CheckBox Check(Control parent, string text, bool on, int y)
        {
            CheckBox c = new CheckBox();
            c.Text = text; c.Checked = on; c.ForeColor = Txt; c.BackColor = Color.Transparent; c.FlatStyle = FlatStyle.Standard; c.UseVisualStyleBackColor = false;
            c.SetBounds(S(2), S(y), S(440), S(26));
            parent.Controls.Add(c);
            return c;
        }

        Button Btn(Control parent, string text, int x, int y, int w, bool primary)
        {
            Button b = new Button();
            b.Text = text; b.FlatStyle = FlatStyle.Flat; b.Cursor = Cursors.Hand;
            b.SetBounds(S(x), S(y), S(w), S(36));
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(52, 58, 80);
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(150, 255, 222) : Color.FromArgb(34, 39, 56);
            b.BackColor = primary ? Acc : Field;
            b.ForeColor = primary ? Color.FromArgb(8, 22, 18) : Txt;
            b.Font = new Font("Segoe UI Semibold", 9.5f);
            parent.Controls.Add(b);
            return b;
        }

        void BuildOptions()
        {
            Panel p = pageOptions;
            if (!uninstall)
            {
                Text2(p, "Install AsciiWall", 0, 0, 440, 40, 20, true, Txt);
                Text2(p, "Live ASCII wallpapers for your desktop. Installs for your account only, with no admin rights.", 0, 44, 440, 44, 10, false, Mut);
                Text2(p, "Install to", 0, 104, 200, 22, 9.5f, false, Mut);
                path = new TextBox();
                path.Text = opt.Dir; path.BackColor = Field; path.ForeColor = Txt; path.BorderStyle = BorderStyle.FixedSingle;
                path.SetBounds(S(0), S(128), S(340), S(26));
                p.Controls.Add(path);
                Button browse = Btn(p, "Browse...", 350, 124, 92, false);
                browse.Click += delegate
                {
                    using (FolderBrowserDialog d = new FolderBrowserDialog())
                    {
                        d.Description = "Choose a folder for AsciiWall";
                        if (d.ShowDialog(this) == DialogResult.OK) path.Text = Path.Combine(d.SelectedPath, "AsciiWall");
                    }
                };
                cStartup = Check(p, "Start AsciiWall when I sign in", true, 176);
                cMenu = Check(p, "Add a Start menu shortcut", true, 206);
                cDesktop = Check(p, "Add a desktop shortcut", false, 236);
                cLaunch = Check(p, "Open AsciiWall when setup finishes", true, 266);
                Button install = Btn(p, "Install", 232, 330, 100, true);
                Button cancel = Btn(p, "Cancel", 342, 330, 100, false);
                cancel.Click += delegate { Close(); };
                install.Click += delegate { opt.Dir = path.Text.Trim(); opt.Startup = cStartup.Checked; opt.StartMenu = cMenu.Checked; opt.Desktop = cDesktop.Checked; opt.Launch = cLaunch.Checked; Run(); };
                AcceptButton = install; CancelButton = cancel;
                Text2(p, "Version " + Ops.Version, 0, 342, 200, 20, 8.5f, false, Mut);
            }
            else
            {
                Text2(p, "Remove AsciiWall?", 0, 0, 440, 40, 20, true, Txt);
                Text2(p, "This closes AsciiWall and removes the program, its shortcuts and its start-up entry.", 0, 44, 440, 44, 10, false, Mut);
                cData = Check(p, "Also delete my settings and custom scenes", false, 120);
                Button go = Btn(p, "Uninstall", 232, 330, 100, true);
                Button cancel = Btn(p, "Cancel", 342, 330, 100, false);
                cancel.Click += delegate { Close(); };
                go.Click += delegate { opt.RemoveData = cData.Checked; Run(); };
                AcceptButton = go; CancelButton = cancel;
            }
        }

        void BuildWork()
        {
            Panel p = pageWork;
            Text2(p, uninstall ? "Removing AsciiWall" : "Installing AsciiWall", 0, 0, 440, 40, 20, true, Txt);
            workStatus.AutoSize = false; workStatus.ForeColor = Mut; workStatus.BackColor = Color.Transparent;
            workStatus.SetBounds(S(0), S(140), S(440), S(24));
            p.Controls.Add(workStatus);
            bar.SetBounds(S(0), S(176), S(440), S(10));
            p.Controls.Add(bar);
        }

        void BuildDone()
        {
            Panel p = pageDone;
            doneTitle.AutoSize = false; doneTitle.ForeColor = Txt; doneTitle.BackColor = Color.Transparent; doneTitle.Font = new Font("Segoe UI Semibold", 20);
            doneTitle.SetBounds(S(0), S(0), S(440), S(40));
            doneText.AutoSize = false; doneText.ForeColor = Mut; doneText.BackColor = Color.Transparent; doneText.Font = new Font("Segoe UI", 10);
            doneText.SetBounds(S(0), S(52), S(440), S(160));
            p.Controls.Add(doneTitle); p.Controls.Add(doneText);
            doneButton = Btn(p, "Finish", 342, 330, 100, true);
            doneButton.Click += delegate { Close(); };
        }

        void Run()
        {
            Show(pageWork);
            workStatus.Text = "Starting...";
            AcceptButton = null; CancelButton = null;
            Thread t = new Thread(delegate()
            {
                string error = null, leftover = null;
                try
                {
                    Action<int, string> prog = delegate(int pct, string msg) { BeginInvoke((Action)delegate { bar.Value = pct; workStatus.Text = msg; }); };
                    if (uninstall) Ops.Uninstall(opt, prog, out leftover); else Ops.Install(opt, prog);
                }
                catch (Exception e) { error = e.Message; }
                BeginInvoke((Action)delegate { Finished(error, leftover); });
            });
            t.IsBackground = true;
            t.Start();
        }

        void Finished(string error, string leftover)
        {
            Show(pageDone);
            if (error != null)
            {
                doneTitle.Text = uninstall ? "Couldn't finish removing" : "Setup didn't finish";
                doneText.Text = error;
                doneButton.Text = "Close";
                return;
            }
            if (uninstall)
            {
                doneTitle.Text = "AsciiWall was removed";
                doneText.Text = "Your desktop wallpaper goes back to the way it was." + (leftover == null ? "" : "\r\n\r\n" + leftover);
            }
            else
            {
                doneTitle.Text = "AsciiWall is ready";
                doneText.Text = "Look for the wave icon in your system tray (the ^ by the clock). Left-click it to pick a scene, or right-click for quick options.\r\n\r\nShortcuts: Ctrl + Alt + Shift with the arrow keys switches scenes.";
                if (opt.Launch) Ops.Launch(opt.Dir);
            }
            AcceptButton = doneButton;
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool silent = false, uninstall = false, launch = false;
            string dir = null;
            Options silentOpts = new Options();
            foreach (string a in args)
            {
                string l = a.ToLowerInvariant();
                if (l == "/s" || l == "/silent" || l == "-s") silent = true;
                else if (l == "/launch") launch = true;
                else if (l == "/nostartup") silentOpts.Startup = false;
                else if (l == "/nomenu") silentOpts.StartMenu = false;
                else if (l == "/desktop") silentOpts.Desktop = true;
                else if (l == "/uninstall" || l == "--uninstall") uninstall = true;
                else if (l.StartsWith("/dir=", StringComparison.Ordinal)) dir = a.Substring(5).Trim('"');
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (uninstall)
            {
                string target = dir ?? Ops.InstalledDir() ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (silent)
                {
                    try { string left; Options o = new Options(); o.Dir = target; Ops.Uninstall(o, delegate(int p, string m) { }, out left); Ops.RunCleanup(); return 0; }
                    catch (Exception) { return 1; }
                }
                Application.Run(new SetupForm(true, target));
                Ops.RunCleanup();
                return 0;
            }

            string install = dir ?? Ops.InstalledDir() ?? Ops.DefaultDir;
            if (silent)
            {
                try { silentOpts.Dir = install; Ops.Install(silentOpts, delegate(int p, string m) { }); if (launch) Ops.Launch(install); return 0; }
                catch (Exception) { return 1; }
            }
            Application.Run(new SetupForm(false, install));
            return 0;
        }
    }
}
