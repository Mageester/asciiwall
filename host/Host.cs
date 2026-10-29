using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AsciiWall
{
    public static class Autostart
    {
        const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "AsciiWall";

        public static bool Get()
        {
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath)) return k != null && k.GetValue(ValueName) != null; }
            catch (Exception) { return false; }
        }

        public static void Set(bool on, string exe)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (on) k.SetValue(ValueName, "\"" + exe + "\" --startup");
                    else k.DeleteValue(ValueName, false);
                }
            }
            catch (Exception e) { Log.Write("autostart: " + e.Message); }
        }
    }

    /// <summary>
    /// The shared state: settings, the scene list, monitors, what is covered. Pages, the tray menu,
    /// hotkeys and the command line all go through here, so they always agree.
    /// </summary>
    public sealed class Host
    {
        public const string Version = "1.0.0";
        public const string RepoUrl = "https://github.com/Mageester/asciiwall";

        public readonly string AppDir, DataDir, WebDir, ConfigPath, ScenesPath, ExePath;
        public Config Cfg;
        public List<SceneInfo> Scenes = new List<SceneInfo>();
        public WebServer Server;
        public Wallpaper Wall;
        public List<MonitorRect> Mons = new List<MonitorRect>();
        public bool[] Covered = new bool[0];
        public bool Locked, OnBattery;

        public Action<Action> Post = delegate(Action a) { a(); };
        public Action OpenSettings, Quit, Restart, HotkeysChanged;
        public event Action Changed;

        readonly object gate = new object();
        readonly Random rng = new Random();
        readonly System.Windows.Forms.Timer rotation = new System.Windows.Forms.Timer();
        System.Threading.Timer saveTimer;
        Dictionary<string, object> stats;
        int statTick;

        public Host(string appDir, string dataDir)
        {
            AppDir = appDir; DataDir = dataDir;
            WebDir = Path.Combine(appDir, "web");
            ConfigPath = Path.Combine(dataDir, "config.json");
            ScenesPath = Path.Combine(dataDir, "scenes.json");
            ExePath = Path.Combine(appDir, "AsciiWall.exe");
            Cfg = Config.Load(ConfigPath, CachedSlugs());
            ReadCachedScenes();
            rotation.Tick += delegate { RotateTick(); };
            saveTimer = new System.Threading.Timer(delegate { lock (gate) Cfg.Save(ConfigPath); }, null, Timeout.Infinite, Timeout.Infinite);
            ScheduleRotation();
        }

        // ---- scene list ----

        List<string> CachedSlugs()
        {
            List<string> r = new List<string>();
            try
            {
                if (File.Exists(ScenesPath))
                {
                    object[] arr = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(ScenesPath, Encoding.UTF8)) as object[];
                    if (arr != null) foreach (object o in arr) { Dictionary<string, object> d = o as Dictionary<string, object>; if (d != null && d.ContainsKey("slug")) r.Add(d["slug"] as string); }
                }
            }
            catch (Exception) { }
            return r;
        }

        void ReadCachedScenes()
        {
            try
            {
                if (File.Exists(ScenesPath)) ParseScenes(File.ReadAllText(ScenesPath, Encoding.UTF8));
            }
            catch (Exception) { }
        }

        bool ParseScenes(string json)
        {
            List<SceneInfo> list = new List<SceneInfo>();
            object[] arr = new JavaScriptSerializer().DeserializeObject(json) as object[];
            if (arr == null) return false;
            foreach (object o in arr)
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d == null) continue;
                string slug = d.ContainsKey("slug") ? d["slug"] as string : null, name = d.ContainsKey("name") ? d["name"] as string : null, acc = d.ContainsKey("accent") ? d["accent"] as string : null;
                if (string.IsNullOrEmpty(slug) || slug.Length > 40) continue;
                list.Add(new SceneInfo(slug, name ?? slug, acc != null && acc.Length == 7 && acc[0] == '#' ? acc : "#8fb8ff"));
            }
            if (list.Count == 0) return false;
            lock (gate) Scenes = list;
            return true;
        }

        public void ReportScenes(string json)
        {
            try
            {
                if (!ParseScenes(json)) return;
                try { File.WriteAllText(ScenesPath, json, new UTF8Encoding(false)); } catch (Exception) { }
                lock (gate)
                {
                    if (FindScene(Cfg.Scene) == null && Scenes.Count > 0) Cfg.Scene = Scenes[0].Slug;
                }
                Fire();
            }
            catch (Exception e) { Log.Write("scenes: " + e.Message); }
        }

        public SceneInfo FindScene(string slug)
        {
            foreach (SceneInfo s in Scenes) if (s.Slug == slug) return s;
            return null;
        }

        List<string> Slugs()
        {
            List<string> l = new List<string>();
            foreach (SceneInfo s in Scenes) l.Add(s.Slug);
            return l;
        }

        // ---- changing things ----

        public void Patch(Dictionary<string, object> p)
        {
            bool restartRotation = false, hotkeys = false;
            lock (gate)
            {
                object v;
                if (p.TryGetValue("startup", out v) && v is bool)
                {
                    Autostart.Set((bool)v, ExePath);
                    p.Remove("startup");
                    Fire();
                }
                int oldRotate = Cfg.Rotate;
                bool oldHot = Cfg.Hotkeys;
                if (!Cfg.Apply(p, Slugs())) { return; }
                restartRotation = oldRotate != Cfg.Rotate;
                hotkeys = oldHot != Cfg.Hotkeys;
                saveTimer.Change(400, Timeout.Infinite);
            }
            if (restartRotation) Post(delegate { ScheduleRotation(); });
            if (hotkeys && HotkeysChanged != null) Post(HotkeysChanged);
            Fire();
        }

        public void Set(string key, object value)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d[key] = value;
            Patch(d);
        }

        /// <summary>Picks a scene for every screen at once (what a tray click or a hotkey means).</summary>
        public void ChooseScene(string slug)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["scene"] = slug;
            d["screens"] = new object[0];
            Patch(d);
        }

        List<string> Pool()
        {
            List<string> pool = new List<string>();
            lock (gate)
            {
                foreach (SceneInfo s in Scenes) if (Cfg.Playlist.Count == 0 || Cfg.Playlist.Contains(s.Slug)) pool.Add(s.Slug);
                if (pool.Count == 0) pool = Slugs();
            }
            return pool;
        }

        public string Step(int dir)
        {
            List<string> pool = Pool();
            if (pool.Count == 0) return "";
            int i = pool.IndexOf(Cfg.Scene);
            string slug = i < 0 ? pool[0] : pool[((i + dir) % pool.Count + pool.Count) % pool.Count];
            ChooseScene(slug);
            return slug;
        }

        public string RandomScene()
        {
            List<string> pool = Pool();
            if (pool.Count == 0) return "";
            if (pool.Count > 1) pool.Remove(Cfg.Scene);
            string slug = pool[rng.Next(pool.Count)];
            ChooseScene(slug);
            return slug;
        }

        public void TogglePause() { Set("paused", !Cfg.Paused); }

        void ScheduleRotation()
        {
            rotation.Stop();
            if (Cfg.Rotate > 0) { rotation.Interval = Cfg.Rotate * 60000; rotation.Start(); }
        }

        void RotateTick()
        {
            if (Cfg.Paused || Hold()) return;
            bool allCovered = Covered.Length > 0;
            foreach (bool c in Covered) if (!c) allCovered = false;
            if (allCovered) return;
            if (Cfg.Shuffle) RandomScene(); else Step(1);
        }

        public string Command(string cmd, string arg)
        {
            switch (cmd)
            {
                case "next": return Step(1);
                case "prev": return Step(-1);
                case "random": return RandomScene();
                case "scene":
                    if (arg != null && (FindScene(arg) != null || Scenes.Count == 0)) { ChooseScene(arg); return arg; }
                    return "unknown scene";
                case "pause": Set("paused", true); return "paused";
                case "resume": Set("paused", false); return "running";
                case "toggle-pause": TogglePause(); return Cfg.Paused ? "paused" : "running";
                case "settings": if (OpenSettings != null) Post(OpenSettings); return "ok";
                case "restart": if (Restart != null) Post(Restart); return "ok";
                case "quit": if (Quit != null) Post(Quit); return "bye";
                case "list":
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (SceneInfo s in Scenes) sb.Append(s.Slug).Append('\n');
                    return sb.ToString().TrimEnd();
                }
                case "status":
                    return "scene=" + Cfg.Scene + " paused=" + Cfg.Paused + " hold=" + Hold() + " wallpaper=" + Wall.State;
            }
            return "unknown command";
        }

        // ---- derived state ----

        public bool Hold()
        {
            return Locked || (OnBattery && Cfg.Battery == "pause");
        }

        int FpsCap()
        {
            return OnBattery && Cfg.Battery == "limit" ? Math.Min(15, Cfg.Fps) : Cfg.Fps;
        }

        public string StateJson()
        {
            lock (gate)
            {
                Dictionary<string, object> d = Cfg.ToDict();
                List<object> mons = new List<object>();
                foreach (MonitorRect m in Mons)
                {
                    Dictionary<string, object> md = new Dictionary<string, object>();
                    md["x"] = m.X; md["y"] = m.Y; md["w"] = m.W; md["h"] = m.H;
                    mons.Add(md);
                }
                d["monitors"] = mons.ToArray();
                object[] cov = new object[Covered.Length];
                for (int i = 0; i < cov.Length; i++) cov[i] = Covered[i];
                d["covered"] = cov;
                d["hold"] = Hold();
                d["fpsCap"] = FpsCap();
                d["onBattery"] = OnBattery;
                d["startup"] = Autostart.Get();
                d["version"] = Version;
                d["wallState"] = Wall == null ? "Idle" : Wall.State.ToString();
                if (stats != null) d["stats"] = stats;
                return new JavaScriptSerializer().Serialize(d);
            }
        }

        void Fire()
        {
            Action a = Changed;
            if (a != null) Post(a);
            Push(false);
        }

        public void Push(bool force)
        {
            if (Server != null) Server.Broadcast(StateJson(), force);
        }

        public void ClientsChanged() { statTick = 99; }

        // ---- periodic work, once a second on the UI thread ----

        public void RefreshMonitors()
        {
            lock (gate) Wallpaper.Monitors(Mons);
            if (Wall != null) Wall.Resize();
            Push(true);
        }

        public void Tick()
        {
            bool onBat = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
            bool changed = onBat != OnBattery;
            OnBattery = onBat;

            if (Mons.Count == 0) lock (gate) Wallpaper.Monitors(Mons);

            bool[] cov = new bool[Mons.Count];
            if (Cfg.PauseCovered && !Cfg.Paused && !Locked && Wall != null && Wall.State == WallState.Attached) cov = Wallpaper.Covered(Mons);
            if (cov.Length != Covered.Length) changed = true;
            else for (int i = 0; i < cov.Length; i++) if (cov[i] != Covered[i]) changed = true;
            lock (gate) Covered = cov;

            if (Server != null && Server.UiClients > 0 && Wall != null)
            {
                if (++statTick >= 5)
                {
                    statTick = 0;
                    double cpu, mem; int n;
                    Wall.Sample(out cpu, out mem, out n);
                    Dictionary<string, object> s = new Dictionary<string, object>();
                    s["cpu"] = Math.Round(cpu, 1); s["mem"] = Math.Round(mem); s["procs"] = n;
                    lock (gate) stats = s;
                    changed = true;
                }
            }
            else if (stats != null) { lock (gate) stats = null; }

            if (changed) { Fire(); }
        }
    }
}
