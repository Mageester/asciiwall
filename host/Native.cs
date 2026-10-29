using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AsciiWall
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width { get { return Right - Left; } }
        public int Height { get { return Bottom - Top; } }
    }

    /// <summary>Win32 calls the host needs. Kept in one place so the rest of the code reads as plain C#.</summary>
    public static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        public const long WS_POPUP = 0x80000000L, WS_CHILD = 0x40000000L, WS_MINIMIZE = 0x20000000L, WS_VISIBLE = 0x10000000L;
        public const long WS_CAPTION = 0x00C00000L, WS_SYSMENU = 0x00080000L, WS_THICKFRAME = 0x00040000L;
        public const long WS_MINIMIZEBOX = 0x00020000L, WS_MAXIMIZEBOX = 0x00010000L;
        public const long WS_EX_APPWINDOW = 0x00040000L, WS_EX_TOOLWINDOW = 0x00000080L, WS_EX_NOACTIVATE = 0x08000000L;
        public const long WS_EX_WINDOWEDGE = 0x00000100L, WS_EX_CLIENTEDGE = 0x00000200L, WS_EX_TRANSPARENT = 0x00000020L;
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20, SWP_SHOWWINDOW = 0x40;
        public const int SW_SHOWNOACTIVATE = 4, SW_SHOWMINNOACTIVE = 7, SW_RESTORE = 9;
        public const uint WM_CLOSE = 0x0010, WM_HOTKEY = 0x0312;
        public const int DWMWA_CLOAKED = 13, DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string s);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int val, int size);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT val, int size);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr64(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr64(IntPtr h, int i, IntPtr v);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong32(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetWindowLong32(IntPtr h, int i, int v);

        public static long GetStyle(IntPtr h, int idx)
        {
            long v = IntPtr.Size == 8 ? GetWindowLongPtr64(h, idx).ToInt64() : (long)(uint)GetWindowLong32(h, idx);
            return v & 0xFFFFFFFFL;
        }

        public static void SetStyle(IntPtr h, int idx, long v)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(h, idx, new IntPtr(v));
            else SetWindowLong32(h, idx, unchecked((int)(uint)v));
        }

        /// <summary>Per-monitor DPI awareness, so every rectangle we read or set is in physical pixels.</summary>
        public static void MakeDpiAware()
        {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch (EntryPointNotFoundException) { }
        }

        public static string ClassOf(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(128);
            GetClassName(h, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string TitleOf(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(h, sb, sb.Capacity);
            return sb.ToString();
        }

        public static uint PidOf(IntPtr h)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            return pid;
        }

        public static bool IsCloaked(IntPtr h)
        {
            int cloaked;
            return DwmGetWindowAttribute(h, DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0;
        }

        /// <summary>The window's visible bounds (without the invisible resize border Windows adds).</summary>
        public static bool VisibleBounds(IntPtr h, out RECT r)
        {
            if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf(typeof(RECT))) == 0) return true;
            return GetWindowRect(h, out r);
        }
    }
}
