using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace AsciiWall
{
    public static class Log
    {
        static readonly object gate = new object();
        public static string Path;

        public static void Init(string dir)
        {
            Path = System.IO.Path.Combine(dir, "log.txt");
            try { if (File.Exists(Path) && new FileInfo(Path).Length > 200 * 1024) File.Delete(Path); } catch (IOException) { }
        }

        public static void Write(string msg)
        {
            if (Path == null) return;
            lock (gate)
            {
                try { File.AppendAllText(Path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ", CultureInfo.InvariantCulture) + msg + Environment.NewLine); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>User settings. The same names go to the pages as JSON, so a patch from the gallery maps straight onto these fields.</summary>
    public sealed class Config
    {
        public string Scene = "dunes";
        public List<string> Screens = new List<string>();
        public string Mode = "mirror";
        public int Fps = 24, Detail = 1, ClockSize = 1, Rotate = 0, Hue = 0;
        public double Speed = 1.0, Dim = 0.0, Sat = 1.0;
        public string Clock = "off", Tint = "none", Battery = "limit";
        public List<string> Playlist = new List<string>();
        public bool Clock24 = false, ClockDate = true, ClockAll = false, Shuffle = false, PauseCovered = true, Paused = false, Hotkeys = true;

        static readonly int[] FpsChoices = { 15, 24, 30, 60 };
        static readonly int[] RotateChoices = { 0, 1, 5, 15, 30, 60 };
        static readonly string[] ClockChoices = { "off", "center", "top-right", "bottom-right", "bottom-center" };
        static readonly string[] TintChoices = { "none", "mono", "phosphor", "amber", "ice", "rose" };
        static readonly string[] BatteryChoices = { "normal", "limit", "pause" };

        public Dictionary<string, object> ToDict()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["scene"] = Scene; d["screens"] = Screens.ToArray(); d["mode"] = Mode;
            d["fps"] = Fps; d["detail"] = Detail; d["speed"] = Speed; d["dim"] = Dim;
            d["tint"] = Tint; d["hue"] = Hue; d["sat"] = Sat;
            d["clock"] = Clock; d["clock24"] = Clock24; d["clockDate"] = ClockDate; d["clockSize"] = ClockSize; d["clockAll"] = ClockAll;
            d["rotate"] = Rotate; d["shuffle"] = Shuffle; d["playlist"] = Playlist.ToArray();
            d["pauseCovered"] = PauseCovered; d["paused"] = Paused; d["battery"] = Battery; d["hotkeys"] = Hotkeys;
            return d;
        }

        static List<string> SlugList(object v, ICollection<string> known, bool allowBlank)
        {
            List<string> s = new List<string>();
            foreach (object o in (IEnumerable)v)
            {
                string x = o as string;
                bool ok = x != null && ((allowBlank && x == "") || (IsSlug(x) && x.Length > 0 && (known == null || known.Count == 0 || known.Contains(x))));
                if (ok) s.Add(x); else if (allowBlank) s.Add("");
                if (s.Count >= 64) break;
            }
            return s;
        }

        static bool IsSlug(string s)
        {
            if (s == null || s.Length > 40) return false;
            foreach (char c in s) if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-')) return false;
            return true;
        }

        static int Int(object o, int fallback)
        {
            try { return Convert.ToInt32(Convert.ToDouble(o, CultureInfo.InvariantCulture)); } catch (Exception) { return fallback; }
        }

        static double Dbl(object o, double fallback)
        {
            try { return Convert.ToDouble(o, CultureInfo.InvariantCulture); } catch (Exception) { return fallback; }
        }

        static bool Bool(object o, bool fallback)
        {
            return o is bool ? (bool)o : fallback;
        }

        /// <summary>Applies the keys it recognises and ignores anything else. Returns true if something changed.</summary>
        public bool Apply(IDictionary<string, object> p, ICollection<string> knownScenes)
        {
            string before = new JavaScriptSerializer().Serialize(ToDict());
            object v;
            if (p.TryGetValue("scene", out v) && v is string && IsSlug((string)v) && (knownScenes == null || knownScenes.Count == 0 || knownScenes.Contains((string)v))) Scene = (string)v;
            if (p.TryGetValue("screens", out v) && v is IEnumerable && !(v is string)) Screens = SlugList(v, knownScenes, true);
            if (p.TryGetValue("playlist", out v) && v is IEnumerable && !(v is string)) Playlist = SlugList(v, knownScenes, false);
            if (p.TryGetValue("tint", out v) && v is string && Array.IndexOf(TintChoices, (string)v) >= 0) Tint = (string)v;
            if (p.TryGetValue("hue", out v)) Hue = ((Int(v, Hue) % 360) + 360) % 360;
            if (p.TryGetValue("sat", out v)) Sat = Math.Max(0.0, Math.Min(1.6, Dbl(v, Sat)));
            if (p.TryGetValue("battery", out v) && v is string && Array.IndexOf(BatteryChoices, (string)v) >= 0) Battery = (string)v;
            if (p.TryGetValue("hotkeys", out v)) Hotkeys = Bool(v, Hotkeys);
            if (p.TryGetValue("mode", out v) && (Equals(v, "mirror") || Equals(v, "span"))) Mode = (string)v;
            if (p.TryGetValue("fps", out v)) { int n = Int(v, Fps); if (Array.IndexOf(FpsChoices, n) >= 0) Fps = n; }
            if (p.TryGetValue("detail", out v)) Detail = Math.Max(0, Math.Min(2, Int(v, Detail)));
            if (p.TryGetValue("speed", out v)) Speed = Math.Max(0.25, Math.Min(2.0, Dbl(v, Speed)));
            if (p.TryGetValue("dim", out v)) Dim = Math.Max(0.0, Math.Min(0.6, Dbl(v, Dim)));
            if (p.TryGetValue("clock", out v) && v is string && Array.IndexOf(ClockChoices, (string)v) >= 0) Clock = (string)v;
            if (p.TryGetValue("clock24", out v)) Clock24 = Bool(v, Clock24);
            if (p.TryGetValue("clockDate", out v)) ClockDate = Bool(v, ClockDate);
            if (p.TryGetValue("clockAll", out v)) ClockAll = Bool(v, ClockAll);
            if (p.TryGetValue("clockSize", out v)) ClockSize = Math.Max(1, Math.Min(3, Int(v, ClockSize)));
            if (p.TryGetValue("rotate", out v)) { int n = Int(v, Rotate); if (Array.IndexOf(RotateChoices, n) >= 0) Rotate = n; }
            if (p.TryGetValue("shuffle", out v)) Shuffle = Bool(v, Shuffle);
            if (p.TryGetValue("pauseCovered", out v)) PauseCovered = Bool(v, PauseCovered);
            if (p.TryGetValue("paused", out v)) Paused = Bool(v, Paused);
            return before != new JavaScriptSerializer().Serialize(ToDict());
        }

        public static Config Load(string path, ICollection<string> knownScenes)
        {
            Config c = new Config();
            try
            {
                if (File.Exists(path))
                {
                    Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                    if (d != null) c.Apply(d, knownScenes);
                }
            }
            catch (Exception e) { Log.Write("config load failed: " + e.Message); }
            return c;
        }

        public void Save(string path)
        {
            try
            {
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(ToDict()), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
            }
            catch (Exception e) { Log.Write("config save failed: " + e.Message); }
        }
    }

    public sealed class SceneInfo
    {
        public string Slug, Name, Accent;
        public SceneInfo(string slug, string name, string accent) { Slug = slug; Name = name; Accent = accent; }
    }
}
