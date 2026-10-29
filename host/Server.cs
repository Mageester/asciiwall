using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace AsciiWall
{
    /// <summary>
    /// A tiny loopback-only HTTP server: static files for the wallpaper and settings pages, a JSON API,
    /// and a server-sent-events stream that pushes state to every open page. Every API call needs the
    /// per-run token, and the Host header must be loopback, which rules out DNS-rebinding from web pages.
    /// </summary>
    public sealed class WebServer
    {
        sealed class Client
        {
            public TcpClient Tcp;
            public NetworkStream Stream;
            public bool Wall;
        }

        const int PreferredPort = 47831;
        static readonly Dictionary<string, string> Mime = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".html", "text/html; charset=utf-8" }, { ".js", "text/javascript; charset=utf-8" }, { ".css", "text/css; charset=utf-8" },
            { ".json", "application/json; charset=utf-8" }, { ".svg", "image/svg+xml" }, { ".png", "image/png" }, { ".ico", "image/x-icon" },
            { ".woff2", "font/woff2" }, { ".txt", "text/plain; charset=utf-8" }
        };

        readonly Host host;
        readonly string webRoot, userScenes;
        readonly List<Client> clients = new List<Client>();
        TcpListener listener;
        Thread acceptThread;
        volatile bool running;
        string lastSent = "";

        public int Port;
        public string Token;

        public WebServer(Host host, string webRoot, string userScenes)
        {
            this.host = host; this.webRoot = Path.GetFullPath(webRoot); this.userScenes = userScenes;
            byte[] b = new byte[18];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            Token = Convert.ToBase64String(b).Replace('+', 'a').Replace('/', 'b').Replace('=', 'c');
        }

        public int UiClients
        {
            get { lock (clients) { int n = 0; foreach (Client c in clients) if (!c.Wall) n++; return n; } }
        }

        public void Start()
        {
            try { listener = new TcpListener(IPAddress.Loopback, PreferredPort); listener.Start(); }
            catch (SocketException) { listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); }
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            running = true;
            acceptThread = new Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Name = "http-accept";
            acceptThread.Start();
        }

        public void Stop()
        {
            running = false;
            try { listener.Stop(); } catch (Exception) { }
            lock (clients) { foreach (Client c in clients) try { c.Tcp.Close(); } catch (Exception) { } clients.Clear(); }
        }

        void AcceptLoop()
        {
            while (running)
            {
                TcpClient c;
                try { c = listener.AcceptTcpClient(); }
                catch (Exception) { if (!running) return; continue; }
                ThreadPool.QueueUserWorkItem(delegate(object o) { Handle((TcpClient)o); }, c);
            }
        }

        /// <summary>Pushes the state to every page, unless nothing changed since the last push.</summary>
        public void Broadcast(string json, bool force)
        {
            if (!force && json == lastSent) return;
            lastSent = json;
            byte[] data = Encoding.UTF8.GetBytes("data: " + json + "\n\n");
            Send(data);
        }

        public void Ping() { Send(Encoding.ASCII.GetBytes(": keepalive\n\n")); }

        void Send(byte[] data)
        {
            List<Client> snapshot;
            lock (clients) snapshot = new List<Client>(clients);
            foreach (Client c in snapshot)
            {
                try { lock (c) { c.Stream.Write(data, 0, data.Length); c.Stream.Flush(); } }
                catch (Exception) { Drop(c); }
            }
        }

        void Drop(Client c)
        {
            lock (clients) clients.Remove(c);
            try { c.Tcp.Close(); } catch (Exception) { }
        }

        // ---- request handling ----

        sealed class Req
        {
            public string Method, Path, Query;
            public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public byte[] Body = new byte[0];
        }

        void Handle(TcpClient tcp)
        {
            bool keep = false;
            try
            {
                tcp.ReceiveTimeout = 5000; tcp.SendTimeout = 3000; tcp.NoDelay = true;
                NetworkStream ns = tcp.GetStream();
                Req r = ReadRequest(ns);
                if (r == null) return;
                if (!HostOk(r)) { Reply(ns, 403, "text/plain", "forbidden"); return; }
                if (r.Path.StartsWith("/api/", StringComparison.Ordinal)) keep = Api(tcp, ns, r);
                else Static(ns, r);
            }
            catch (Exception e) { Log.Write("http: " + e.Message); }
            finally { if (!keep) { try { tcp.Close(); } catch (Exception) { } } }
        }

        static Req ReadRequest(NetworkStream ns)
        {
            byte[] buf = new byte[16384];
            int len = 0, headEnd = -1;
            while (headEnd < 0)
            {
                int n = ns.Read(buf, len, buf.Length - len);
                if (n <= 0) return null;
                len += n;
                for (int i = Math.Max(0, len - n - 3); i + 3 < len; i++)
                    if (buf[i] == 13 && buf[i + 1] == 10 && buf[i + 2] == 13 && buf[i + 3] == 10) { headEnd = i; break; }
                if (headEnd < 0 && len == buf.Length) return null;
            }
            string[] lines = Encoding.ASCII.GetString(buf, 0, headEnd).Split(new string[] { "\r\n" }, StringSplitOptions.None);
            string[] first = lines[0].Split(' ');
            if (first.Length < 2) return null;
            Req r = new Req();
            r.Method = first[0].ToUpperInvariant();
            string target = first[1];
            int q = target.IndexOf('?');
            r.Path = q < 0 ? target : target.Substring(0, q);
            r.Query = q < 0 ? "" : target.Substring(q + 1);
            for (int i = 1; i < lines.Length; i++)
            {
                int c = lines[i].IndexOf(':');
                if (c > 0) r.Headers[lines[i].Substring(0, c).Trim()] = lines[i].Substring(c + 1).Trim();
            }
            string cl;
            int want = 0;
            if (r.Headers.TryGetValue("Content-Length", out cl)) int.TryParse(cl, out want);
            if (want < 0 || want > 65536) return null;
            if (want > 0)
            {
                byte[] body = new byte[want];
                int have = Math.Min(want, len - (headEnd + 4));
                if (have > 0) Buffer.BlockCopy(buf, headEnd + 4, body, 0, have);
                while (have < want)
                {
                    int n = ns.Read(body, have, want - have);
                    if (n <= 0) return null;
                    have += n;
                }
                r.Body = body;
            }
            return r;
        }

        bool HostOk(Req r)
        {
            string h;
            if (!r.Headers.TryGetValue("Host", out h)) return false;
            if (h != "127.0.0.1:" + Port && h != "localhost:" + Port) return false;
            string origin;
            if (r.Headers.TryGetValue("Origin", out origin) && origin != "http://127.0.0.1:" + Port && origin != "http://localhost:" + Port) return false;
            return true;
        }

        static string QueryValue(string query, string key)
        {
            foreach (string part in query.Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0 && part.Substring(0, eq) == key) return Uri.UnescapeDataString(part.Substring(eq + 1));
            }
            return "";
        }

        bool TokenOk(Req r)
        {
            string k = QueryValue(r.Query, "k");
            if (k.Length != Token.Length) return false;
            int diff = 0;
            for (int i = 0; i < k.Length; i++) diff |= k[i] ^ Token[i];
            return diff == 0;
        }

        bool Api(TcpClient tcp, NetworkStream ns, Req r)
        {
            if (!TokenOk(r)) { Reply(ns, 403, "application/json", "{\"ok\":false,\"error\":\"token\"}"); return false; }
            switch (r.Path)
            {
                case "/api/events":
                {
                    string head = "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-store\r\nConnection: keep-alive\r\n\r\n";
                    byte[] hb = Encoding.ASCII.GetBytes(head);
                    byte[] first = Encoding.UTF8.GetBytes("retry: 2000\n\ndata: " + host.StateJson() + "\n\n");
                    tcp.ReceiveTimeout = 0;
                    ns.Write(hb, 0, hb.Length);
                    ns.Write(first, 0, first.Length);
                    Client c = new Client();
                    c.Tcp = tcp; c.Stream = ns; c.Wall = QueryValue(r.Query, "role") == "wall";
                    lock (clients) clients.Add(c);
                    host.ClientsChanged();
                    return true;
                }
                case "/api/state":
                    Reply(ns, 200, "application/json", host.StateJson());
                    return false;
                case "/api/set":
                    if (r.Method != "POST") break;
                    host.Patch(ParseBody(r));
                    Reply(ns, 200, "application/json", host.StateJson());
                    return false;
                case "/api/scenes":
                    if (r.Method != "POST") break;
                    host.ReportScenes(Encoding.UTF8.GetString(r.Body));
                    Reply(ns, 200, "application/json", "{\"ok\":true}");
                    return false;
                case "/api/cmd":
                {
                    if (r.Method != "POST") break;
                    Dictionary<string, object> d = ParseBody(r);
                    object cmd, arg;
                    d.TryGetValue("cmd", out cmd);
                    d.TryGetValue("arg", out arg);
                    string result = host.Command(cmd as string, arg as string);
                    Reply(ns, 200, "application/json", new JavaScriptSerializer().Serialize(new Dictionary<string, object> { { "ok", true }, { "result", result } }));
                    return false;
                }
            }
            Reply(ns, 404, "application/json", "{\"ok\":false}");
            return false;
        }

        static Dictionary<string, object> ParseBody(Req r)
        {
            try
            {
                Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(Encoding.UTF8.GetString(r.Body)) as Dictionary<string, object>;
                if (d != null) return d;
            }
            catch (Exception) { }
            return new Dictionary<string, object>();
        }

        void Static(NetworkStream ns, Req r)
        {
            if (r.Method != "GET" && r.Method != "HEAD") { Reply(ns, 405, "text/plain", "method"); return; }
            string p = Uri.UnescapeDataString(r.Path);
            if (p == "/") p = "/gallery.html";
            if (p == "/user-scenes.js") { Reply(ns, 200, Mime[".js"], UserScenesJs(), r.Method == "HEAD"); return; }
            if (p.IndexOf('\\') >= 0 || p.IndexOf("..", StringComparison.Ordinal) >= 0 || p.IndexOf(':') >= 0) { Reply(ns, 400, "text/plain", "bad path"); return; }
            string full = Path.GetFullPath(Path.Combine(webRoot, p.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            string mime;
            if (!full.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !Mime.TryGetValue(Path.GetExtension(full), out mime) || !File.Exists(full))
            {
                Reply(ns, 404, "text/plain", "not found");
                return;
            }
            byte[] data = File.ReadAllBytes(full);
            WriteHead(ns, 200, mime, data.Length);
            if (r.Method == "GET") ns.Write(data, 0, data.Length);
        }

        /// <summary>Anything in the user's scenes folder is appended to the built-in scenes.</summary>
        string UserScenesJs()
        {
            StringBuilder sb = new StringBuilder("/* user scenes */\n");
            try
            {
                if (Directory.Exists(userScenes))
                {
                    string[] files = Directory.GetFiles(userScenes, "*.js");
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    foreach (string f in files)
                        sb.Append("try {\n").Append(File.ReadAllText(f, Encoding.UTF8)).Append("\n} catch (e) { console.error('scene file ").Append(Path.GetFileName(f).Replace("'", "")).Append(": ' + e); }\n");
                }
            }
            catch (Exception e) { Log.Write("user scenes: " + e.Message); }
            return sb.ToString();
        }

        static void WriteHead(NetworkStream ns, int code, string mime, int length)
        {
            string status = code == 200 ? "OK" : code == 400 ? "Bad Request" : code == 403 ? "Forbidden" : code == 404 ? "Not Found" : code == 405 ? "Method Not Allowed" : "Error";
            string head = "HTTP/1.1 " + code.ToString(CultureInfo.InvariantCulture) + " " + status + "\r\nContent-Type: " + mime +
                "\r\nContent-Length: " + length.ToString(CultureInfo.InvariantCulture) +
                "\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n";
            byte[] hb = Encoding.ASCII.GetBytes(head);
            ns.Write(hb, 0, hb.Length);
        }

        static void Reply(NetworkStream ns, int code, string mime, string body) { Reply(ns, code, mime, body, false); }

        static void Reply(NetworkStream ns, int code, string mime, string body, bool headOnly)
        {
            byte[] data = Encoding.UTF8.GetBytes(body);
            WriteHead(ns, code, mime, data.Length);
            if (!headOnly) ns.Write(data, 0, data.Length);
        }
    }
}
