using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AsciiWall
{
    sealed class DarkColors : ProfessionalColorTable
    {
        static readonly Color Bg = Color.FromArgb(24, 27, 38), Hot = Color.FromArgb(46, 52, 74), Line = Color.FromArgb(52, 58, 80);
        public override Color ToolStripDropDownBackground { get { return Bg; } }
        public override Color ImageMarginGradientBegin { get { return Bg; } }
        public override Color ImageMarginGradientMiddle { get { return Bg; } }
        public override Color ImageMarginGradientEnd { get { return Bg; } }
        public override Color MenuBorder { get { return Line; } }
        public override Color MenuItemBorder { get { return Hot; } }
        public override Color MenuItemSelected { get { return Hot; } }
        public override Color MenuItemSelectedGradientBegin { get { return Hot; } }
        public override Color MenuItemSelectedGradientEnd { get { return Hot; } }
        public override Color MenuItemPressedGradientBegin { get { return Hot; } }
        public override Color MenuItemPressedGradientMiddle { get { return Hot; } }
        public override Color MenuItemPressedGradientEnd { get { return Hot; } }
        public override Color SeparatorDark { get { return Line; } }
        public override Color SeparatorLight { get { return Bg; } }
        public override Color CheckBackground { get { return Hot; } }
        public override Color CheckSelectedBackground { get { return Hot; } }
        public override Color CheckPressedBackground { get { return Hot; } }
    }

    sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        public Color Accent = Color.FromArgb(143, 184, 255);
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.FromArgb(232, 235, 246) : Color.FromArgb(124, 131, 154);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.FromArgb(170, 178, 204);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(Accent))
            {
                int d = Math.Max(6, r.Height / 3);
                e.Graphics.FillEllipse(b, r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
            }
        }
    }

    sealed class MsgWindow : NativeWindow
    {
        public Action<int> OnHotkey;
        public MsgWindow() { CreateHandle(new CreateParams()); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)Native.WM_HOTKEY && OnHotkey != null) OnHotkey(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }

    /// <summary>The tray icon, its menu, hotkeys, and the one-second heartbeat that keeps the wallpaper healthy.</summary>
    public sealed class TrayApp : ApplicationContext
    {
        readonly Host host;
        readonly NotifyIcon ni = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly DarkRenderer renderer = new DarkRenderer();
        readonly System.Windows.Forms.Timer beat = new System.Windows.Forms.Timer(), displayTimer = new System.Windows.Forms.Timer(), retry = new System.Windows.Forms.Timer();
        readonly Control ui = new Control();
        readonly MsgWindow msg;
        readonly string settingsProfile;
        readonly float scale;
        IntPtr iconHandle = IntPtr.Zero;
        string iconKey = "";
        bool quitting, hotkeysOn, gaveUp, firstRun;
        int failures;
        DateTime lastStart = DateTime.UtcNow, attachedAt = DateTime.MaxValue;

        readonly bool noWall;

        public TrayApp(Host host, bool startup, bool firstRun, bool noWall)
        {
            this.host = host; this.firstRun = firstRun; this.noWall = noWall;
            settingsProfile = Path.Combine(host.DataDir, "s");
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            IntPtr dummy = ui.Handle;
            msg = new MsgWindow();
            msg.OnHotkey = OnHotkey;

            host.Post = delegate(Action a) { try { ui.BeginInvoke(a); } catch (Exception) { } };
            host.OpenSettings = OpenSettings;
            host.Quit = Quit;
            host.Restart = RestartWallpaper;
            host.HotkeysChanged = SyncHotkeys;
            host.Changed += RefreshTray;

            menu.Renderer = renderer;
            menu.Font = new Font("Segoe UI", 9.25f);
            menu.ShowImageMargin = true;
            menu.Opening += delegate { BuildMenu(); };

            ni.ContextMenuStrip = menu;
            ni.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) OpenSettings(); };
            RefreshTray();
            ni.Visible = true;

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemEvents.SessionSwitch += OnSession;
            SystemEvents.PowerModeChanged += OnPower;

            displayTimer.Interval = 1500;
            displayTimer.Tick += delegate { displayTimer.Stop(); host.RefreshMonitors(); };
            beat.Interval = 1000;
            beat.Tick += delegate { Beat(); };
            retry.Tick += delegate { retry.Stop(); StartWallpaper(); };

            host.Wall.Attached += delegate
            {
                host.Post(delegate
                {
                    attachedAt = DateTime.UtcNow;
                    host.RefreshMonitors();
                    if (firstRun) { firstRun = false; OpenSettings(); }
                });
            };

            SyncHotkeys();
            beat.Start();

            if (startup)
            {
                retry.Interval = 3000;
                retry.Start();
            }
            else StartWallpaper();
        }

        // ---- wallpaper lifecycle ----

        void StartWallpaper()
        {
            if (noWall) return;
            attachedAt = DateTime.MaxValue;
            lastStart = DateTime.UtcNow;
            string url = "http://127.0.0.1:" + host.Server.Port + "/wall.html?k=" + host.Server.Token;
            if (!host.Wall.Start(url)) Balloon("Couldn't start the wallpaper", "AsciiWall needs Microsoft Edge (or Chrome) to draw. See log.txt in the data folder.");
        }

        void RestartWallpaper()
        {
            failures = 0; gaveUp = false;
            host.Wall.Stop();
            StartWallpaper();
        }

        void Beat()
        {
            if (quitting) return;
            try { host.Tick(); } catch (Exception e) { Log.Write("tick: " + e); }

            Wallpaper w = host.Wall;
            if (retry.Enabled || gaveUp) return;
            bool broken = w.State == WallState.Failed
                || (w.State == WallState.Attached && !w.Healthy())
                || (w.State == WallState.Starting && (DateTime.UtcNow - lastStart).TotalSeconds > 90);
            if (w.State == WallState.Attached && (DateTime.UtcNow - attachedAt).TotalSeconds > 60) failures = 0;
            if (!broken) return;

            failures++;
            Log.Write("wallpaper unhealthy (state " + w.State + "), restart #" + failures);
            w.Stop();
            if (failures > 6)
            {
                gaveUp = true;
                Balloon("The wallpaper keeps stopping", "Choose Restart wallpaper from the tray menu to try again.");
                return;
            }
            retry.Interval = Math.Min(30, 1 << Math.Min(failures, 5)) * 1000;
            retry.Start();
        }

        void OnDisplayChanged(object s, EventArgs e) { host.Post(delegate { displayTimer.Stop(); displayTimer.Start(); }); }

        void OnSession(object s, SessionSwitchEventArgs e)
        {
            bool? lockedNow = null;
            if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.ConsoleDisconnect || e.Reason == SessionSwitchReason.RemoteDisconnect) lockedNow = true;
            if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.ConsoleConnect || e.Reason == SessionSwitchReason.RemoteConnect) lockedNow = false;
            if (lockedNow == null) return;
            bool v = lockedNow.Value;
            host.Post(delegate { host.Locked = v; host.Push(true); });
        }

        void OnPower(object s, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) host.Post(delegate { displayTimer.Stop(); displayTimer.Start(); });
        }

        // ---- hotkeys: Ctrl+Alt+Shift + Right / Left / R / P ----

        void SyncHotkeys()
        {
            bool want = host.Cfg.Hotkeys;
            if (want == hotkeysOn) return;
            const uint mods = 0x1 | 0x2 | 0x4 | 0x4000;
            if (want)
            {
                bool ok = Native.RegisterHotKey(msg.Handle, 1, mods, 0x27);
                ok &= Native.RegisterHotKey(msg.Handle, 2, mods, 0x25);
                ok &= Native.RegisterHotKey(msg.Handle, 3, mods, 0x52);
                ok &= Native.RegisterHotKey(msg.Handle, 4, mods, 0x50);
                if (!ok) Log.Write("some hotkeys were already taken");
            }
            else for (int i = 1; i <= 4; i++) Native.UnregisterHotKey(msg.Handle, i);
            hotkeysOn = want;
        }

        void OnHotkey(int id)
        {
            if (id == 1) host.Step(1);
            else if (id == 2) host.Step(-1);
            else if (id == 3) host.RandomScene();
            else if (id == 4) host.TogglePause();
        }

        // ---- the settings window: the gallery page in an app-style browser window ----

        void OpenSettings()
        {
            try
            {
                IntPtr existing = IntPtr.Zero;
                Native.EnumWindows(delegate(IntPtr h, IntPtr l)
                {
                    if (Native.IsWindowVisible(h) && Native.ClassOf(h) == "Chrome_WidgetWin_1" && Native.TitleOf(h).StartsWith("AsciiWall Settings", StringComparison.Ordinal)) { existing = h; return false; }
                    return true;
                }, IntPtr.Zero);
                if (existing != IntPtr.Zero)
                {
                    if (Native.IsIconic(existing)) Native.ShowWindow(existing, Native.SW_RESTORE);
                    Native.SetForegroundWindow(existing);
                    return;
                }

                string url = "http://127.0.0.1:" + host.Server.Port + "/gallery.html?k=" + host.Server.Token;
                string exe = Wallpaper.FindBrowser();
                if (exe == null) { Process.Start(url); return; }
                Directory.CreateDirectory(settingsProfile);
                ProcessStartInfo psi = new ProcessStartInfo(exe);
                psi.UseShellExecute = false;
                psi.Arguments = "--app=\"" + url + "\" --user-data-dir=\"" + settingsProfile + "\" --window-size=1240,860 --no-first-run --no-default-browser-check --disable-extensions --disable-sync --disable-background-networking --disable-component-update --disable-breakpad --disable-features=Translate,MediaRouter";
                Process.Start(psi);
            }
            catch (Exception e) { Log.Write("settings: " + e.Message); }
        }

        // ---- tray icon ----

        void RefreshTray()
        {
            SceneInfo cur = host.FindScene(host.Cfg.Scene);
            string name = cur == null ? host.Cfg.Scene : cur.Name;
            Color accent = ParseColor(cur == null ? "#8fb8ff" : cur.Accent);
            bool dim = host.Cfg.Paused;
            renderer.Accent = accent;
            string text = "AsciiWall - " + name + (dim ? " (paused)" : "");
            ni.Text = text.Length > 63 ? text.Substring(0, 63) : text;

            string key = accent.ToArgb() + (dim ? "d" : "");
            if (key == iconKey) return;
            iconKey = key;
            Icon icon = MakeIcon(accent, dim);
            ni.Icon = icon;
            if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
            iconHandle = icon.Handle;
        }

        static Color ParseColor(string hex)
        {
            try
            {
                int n = int.Parse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return Color.FromArgb(255, (n >> 16) & 255, (n >> 8) & 255, n & 255);
            }
            catch (Exception) { return Color.FromArgb(143, 184, 255); }
        }

        /// <summary>Three drifting wave lines on a dark tile, tinted with the current scene's colour.</summary>
        Icon MakeIcon(Color accent, bool dim)
        {
            int s = Math.Max(16, SystemInformation.SmallIconSize.Width);
            using (Bitmap bmp = new Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float r = s * 0.24f;
                using (GraphicsPath path = RoundRect(0.5f, 0.5f, s - 1, s - 1, r))
                using (LinearGradientBrush bg = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(34, 40, 62), Color.FromArgb(13, 15, 26), 90f))
                    g.FillPath(bg, path);
                Color c = dim ? Color.FromArgb(130, 136, 154) : accent;
                float[] ys = { 0.33f, 0.52f, 0.71f };
                int[] alpha = { 255, 190, 120 };
                for (int row = 0; row < 3; row++)
                {
                    List<PointF> pts = new List<PointF>();
                    for (int i = 0; i <= 12; i++)
                    {
                        float x = s * (0.18f + 0.64f * i / 12f);
                        float y = s * ys[row] + (float)Math.Sin(i / 12.0 * Math.PI * 2 + row * 1.3) * s * 0.07f;
                        pts.Add(new PointF(x, y));
                    }
                    using (Pen p = new Pen(Color.FromArgb(alpha[row], c), Math.Max(1.2f, s * 0.085f)))
                    {
                        p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                        g.DrawCurve(p, pts.ToArray(), 0.5f);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddArc(x, y, r * 2, r * 2, 180, 90);
            p.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            p.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure();
            return p;
        }

        void Balloon(string title, string text)
        {
            try { ni.ShowBalloonTip(5000, title, text, ToolTipIcon.Info); } catch (Exception) { }
        }

        // ---- menu ----

        Bitmap Swatch(string hex)
        {
            int d = (int)Math.Round(12 * scale);
            Bitmap b = new Bitmap(d, d);
            using (Graphics g = Graphics.FromImage(b))
            using (SolidBrush br = new SolidBrush(ParseColor(hex)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(br, 1, 1, d - 2, d - 2);
            }
            return b;
        }

        ToolStripMenuItem Item(string text, Action click)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            if (click != null) it.Click += delegate { click(); };
            return it;
        }

        ToolStripMenuItem Check(string text, bool on, Action click)
        {
            ToolStripMenuItem it = Item(text, click);
            it.Checked = on;
            return it;
        }

        void Radios(ToolStripMenuItem parent, string[] labels, int selected, Action<int> pick)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                int n = i;
                parent.DropDownItems.Add(Check(labels[i], i == selected, delegate { pick(n); }));
            }
        }

        static int Nearest(double[] choices, double v)
        {
            int best = 0;
            for (int i = 1; i < choices.Length; i++) if (Math.Abs(choices[i] - v) < Math.Abs(choices[best] - v)) best = i;
            return best;
        }

        void SceneItems(ToolStripMenuItem parent, string selected, Action<string> pick, bool withDefault)
        {
            if (withDefault) parent.DropDownItems.Add(Check("Same as the main scene", selected == "", delegate { pick(""); }));
            if (host.Scenes.Count == 0) { ToolStripMenuItem wait = Item("Loading scenes...", null); wait.Enabled = false; parent.DropDownItems.Add(wait); return; }
            foreach (SceneInfo sc in host.Scenes)
            {
                SceneInfo cap = sc;
                ToolStripMenuItem it = Item(sc.Name, delegate { pick(cap.Slug); });
                it.Image = Swatch(sc.Accent);
                it.ImageScaling = ToolStripItemImageScaling.None;
                if (sc.Slug == selected) it.Font = new Font(menu.Font, FontStyle.Bold);
                parent.DropDownItems.Add(it);
            }
        }

        void BuildMenu()
        {
            Config c = host.Cfg;
            foreach (ToolStripItem old in menu.Items) { if (old.Image != null) old.Image.Dispose(); }
            menu.Items.Clear();

            SceneInfo cur = host.FindScene(c.Scene);
            ToolStripMenuItem head = Item("AsciiWall   " + (cur == null ? c.Scene : cur.Name), null);
            head.Enabled = false;
            head.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(head);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem scenes = Item("Scenes", null);
            SceneItems(scenes, c.Scene, delegate(string slug) { host.ChooseScene(slug); }, false);
            menu.Items.Add(scenes);
            ToolStripMenuItem next = Item("Next scene", delegate { host.Step(1); });
            next.ShortcutKeyDisplayString = "Ctrl+Alt+Shift+Right";
            ToolStripMenuItem prev = Item("Previous scene", delegate { host.Step(-1); });
            prev.ShortcutKeyDisplayString = "Ctrl+Alt+Shift+Left";
            ToolStripMenuItem rnd = Item("Surprise me", delegate { host.RandomScene(); });
            rnd.ShortcutKeyDisplayString = "Ctrl+Alt+Shift+R";
            menu.Items.Add(next); menu.Items.Add(prev); menu.Items.Add(rnd);

            ToolStripMenuItem rot = Item("Auto-change", null);
            int[] mins = { 0, 1, 5, 15, 30, 60 };
            Radios(rot, new string[] { "Off", "Every minute", "Every 5 minutes", "Every 15 minutes", "Every 30 minutes", "Every hour" }, Array.IndexOf(mins, c.Rotate), delegate(int i) { host.Set("rotate", mins[i]); });
            rot.DropDownItems.Add(new ToolStripSeparator());
            rot.DropDownItems.Add(Check("Shuffle", c.Shuffle, delegate { host.Set("shuffle", !c.Shuffle); }));
            menu.Items.Add(rot);

            if (host.Mons.Count > 1)
            {
                ToolStripMenuItem scr = Item("Screens", null);
                Radios(scr, new string[] { "Each screen on its own", "One scene spanning all screens" }, c.Mode == "span" ? 1 : 0, delegate(int i) { host.Set("mode", i == 1 ? "span" : "mirror"); });
                scr.DropDownItems.Add(new ToolStripSeparator());
                for (int m = 0; m < host.Mons.Count; m++)
                {
                    int idx = m;
                    ToolStripMenuItem one = Item("Screen " + (m + 1) + (host.Mons[m].Primary ? " (main)" : ""), null);
                    string chosen = idx < c.Screens.Count ? c.Screens[idx] : "";
                    SceneItems(one, chosen, delegate(string slug)
                    {
                        List<string> list = new List<string>(c.Screens);
                        while (list.Count < host.Mons.Count) list.Add("");
                        list[idx] = slug;
                        Dictionary<string, object> d = new Dictionary<string, object>();
                        d["screens"] = list.ToArray(); d["mode"] = "mirror";
                        host.Patch(d);
                    }, true);
                    scr.DropDownItems.Add(one);
                }
                menu.Items.Add(scr);
            }

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem look = Item("Look", null);
            ToolStripMenuItem detail = Item("Detail", null);
            Radios(detail, new string[] { "Soft (lightest)", "Balanced", "Sharp (finest)" }, c.Detail, delegate(int i) { host.Set("detail", i); });
            ToolStripMenuItem speed = Item("Speed", null);
            double[] speeds = { 0.5, 0.75, 1, 1.5, 2 };
            Radios(speed, new string[] { "Slow", "Relaxed", "Normal", "Brisk", "Fast" }, Nearest(speeds, c.Speed), delegate(int i) { host.Set("speed", speeds[i]); });
            ToolStripMenuItem bright = Item("Brightness", null);
            double[] dims = { 0, 0.15, 0.3, 0.45 };
            Radios(bright, new string[] { "100%", "85%", "70%", "55%" }, Nearest(dims, c.Dim), delegate(int i) { host.Set("dim", dims[i]); });
            ToolStripMenuItem tint = Item("Color", null);
            string[] tints = { "none", "mono", "phosphor", "amber", "ice", "rose" };
            Radios(tint, new string[] { "Original", "Monochrome", "Green phosphor", "Amber terminal", "Ice", "Rose" }, Array.IndexOf(tints, c.Tint), delegate(int i) { host.Set("tint", tints[i]); });
            look.DropDownItems.Add(detail); look.DropDownItems.Add(speed); look.DropDownItems.Add(bright); look.DropDownItems.Add(tint);
            menu.Items.Add(look);

            ToolStripMenuItem clock = Item("Clock", null);
            string[] pos = { "off", "center", "top-right", "bottom-right", "bottom-center" };
            Radios(clock, new string[] { "Off", "Center", "Top right", "Bottom right", "Bottom center" }, Array.IndexOf(pos, c.Clock), delegate(int i) { host.Set("clock", pos[i]); });
            clock.DropDownItems.Add(new ToolStripSeparator());
            clock.DropDownItems.Add(Check("24-hour time", c.Clock24, delegate { host.Set("clock24", !c.Clock24); }));
            clock.DropDownItems.Add(Check("Show the date", c.ClockDate, delegate { host.Set("clockDate", !c.ClockDate); }));
            menu.Items.Add(clock);

            ToolStripMenuItem perf = Item("Performance", null);
            ToolStripMenuItem fps = Item("Frame rate", null);
            int[] rates = { 15, 24, 30, 60 };
            Radios(fps, new string[] { "15 fps (lightest)", "24 fps", "30 fps", "60 fps (smoothest)" }, Array.IndexOf(rates, c.Fps), delegate(int i) { host.Set("fps", rates[i]); });
            ToolStripMenuItem bat = Item("On battery", null);
            string[] bm = { "normal", "limit", "pause" };
            Radios(bat, new string[] { "Run normally", "Limit to 15 fps", "Pause" }, Array.IndexOf(bm, c.Battery), delegate(int i) { host.Set("battery", bm[i]); });
            perf.DropDownItems.Add(fps);
            perf.DropDownItems.Add(bat);
            perf.DropDownItems.Add(Check("Pause when a window covers the screen", c.PauseCovered, delegate { host.Set("pauseCovered", !c.PauseCovered); }));
            menu.Items.Add(perf);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Check(c.Paused ? "Resume wallpaper" : "Pause wallpaper", c.Paused, delegate { host.TogglePause(); }));
            ToolStripMenuItem settings = Item("Settings and gallery...", OpenSettings);
            settings.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(settings);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(Check("Start with Windows", Autostart.Get(), delegate { host.Set("startup", !Autostart.Get()); }));
            menu.Items.Add(Check("Keyboard shortcuts", c.Hotkeys, delegate { host.Set("hotkeys", !c.Hotkeys); }));
            menu.Items.Add(Item("Restart wallpaper", RestartWallpaper));
            menu.Items.Add(Item("Open data folder", delegate { try { Process.Start(host.DataDir); } catch (Exception) { } }));
            if (Host.RepoUrl.Length > 0) menu.Items.Add(Item("About AsciiWall " + Host.Version, delegate { try { Process.Start(Host.RepoUrl); } catch (Exception) { } }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Item("Quit", Quit));
        }

        // ---- exit ----

        void Quit()
        {
            if (quitting) return;
            quitting = true;
            beat.Stop(); retry.Stop(); displayTimer.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            SystemEvents.SessionSwitch -= OnSession;
            SystemEvents.PowerModeChanged -= OnPower;
            if (hotkeysOn) for (int i = 1; i <= 4; i++) Native.UnregisterHotKey(msg.Handle, i);
            host.Wall.Stop();
            host.Server.Stop();
            try { File.Delete(Path.Combine(host.DataDir, "run.json")); } catch (Exception) { }
            ni.Visible = false;
            ni.Dispose();
            if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
            msg.DestroyHandle();
            ExitThread();
        }
    }
}
