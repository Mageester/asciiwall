using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace AsciiWall
{
    public static class Ipc
    {
        public static string DataDir()
        {
            string d = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Path.GetTempPath(), "AsciiWall");
            Directory.CreateDirectory(d);
            return d;
        }

        /// <summary>Sends a command to the running instance. Returns null if none is running.</summary>
        public static string Send(string dataDir, string cmd, string arg)
        {
            try
            {
                string path = Path.Combine(dataDir, "run.json");
                if (!File.Exists(path)) return null;
                Dictionary<string, object> run = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
                if (run == null) return null;
                string url = "http://127.0.0.1:" + run["port"] + "/api/cmd?k=" + Uri.EscapeDataString(run["token"] as string);
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST"; req.Timeout = 4000; req.Proxy = null;
                Dictionary<string, object> body = new Dictionary<string, object>();
                body["cmd"] = cmd; body["arg"] = arg;
                byte[] data = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(body));
                req.ContentLength = data.Length;
                using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    Dictionary<string, object> r = new JavaScriptSerializer().DeserializeObject(sr.ReadToEnd()) as Dictionary<string, object>;
                    return r != null && r.ContainsKey("result") ? r["result"] as string : "";
                }
            }
            catch (Exception) { return null; }
        }
    }
}
