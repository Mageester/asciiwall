using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("AsciiWall")]
[assembly: System.Reflection.AssemblyProduct("AsciiWall")]
[assembly: System.Reflection.AssemblyDescription("Live ASCII wallpapers for the Windows desktop")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace AsciiWall
{
    static class Program
    {
        [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
        [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int which);

        const string Usage =
            "AsciiWall - live ASCII wallpapers\r\n\r\n" +
            "  AsciiWall.exe                 start (or open settings if already running)\r\n" +
            "  AsciiWall.exe --next | --prev | --random\r\n" +
            "  AsciiWall.exe --scene <name>  switch scene (see --list)\r\n" +
            "  AsciiWall.exe --pause | --resume | --toggle-pause\r\n" +
            "  AsciiWall.exe --settings      open the settings window\r\n" +
            "  AsciiWall.exe --restart       restart the wallpaper renderer\r\n" +
            "  AsciiWall.exe --list | --status | --quit | --version\r\n";

        [STAThread]
        static int Main(string[] args)
        {
            string dataDir = Ipc.DataDir();
            Log.Init(dataDir);

            string cmd = null, arg = null;
            bool startup = false, noWall = false;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--startup") startup = true;
                else if (a == "--no-wall") noWall = true; // developer flag: run the tray and server without drawing
                else if (a == "--scene" && i + 1 < args.Length) { cmd = "scene"; arg = args[++i]; }
                else if (a == "--next" || a == "--prev" || a == "--random" || a == "--pause" || a == "--resume" || a == "--settings" || a == "--restart" || a == "--list" || a == "--status" || a == "--quit" || a == "--toggle-pause")
                    cmd = a.Substring(2);
                else if (a == "--version" || a == "--help" || a == "-h" || a == "/?") cmd = a;
            }

            if (cmd != null) return Cli(dataDir, cmd, arg);

            bool created;
            Mutex single = new Mutex(true, "Local\\AsciiWall.Host.v1", out created);
            if (!created)
            {
                if (!startup) Ipc.Send(dataDir, "settings", null);
                return 0;
            }

            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log.Write("fatal: " + e.ExceptionObject); };
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log.Write("error: " + e.Exception); };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!Directory.Exists(Path.Combine(appDir, "web")))
            {
                MessageBox.Show("The 'web' folder next to AsciiWall.exe is missing. Please reinstall AsciiWall.", "AsciiWall", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 2;
            }

            bool firstRun = !File.Exists(Path.Combine(dataDir, "config.json"));
            Host host = new Host(appDir, dataDir);
            Directory.CreateDirectory(Path.Combine(dataDir, "scenes"));
            host.Wall = new Wallpaper(Path.Combine(dataDir, "p"));
            host.Server = new WebServer(host, host.WebDir, Path.Combine(dataDir, "scenes"));
            try { host.Server.Start(); }
            catch (Exception e)
            {
                Log.Write("server: " + e);
                MessageBox.Show("AsciiWall could not open a local port: " + e.Message, "AsciiWall", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 3;
            }

            Dictionary<string, object> run = new Dictionary<string, object>();
            run["port"] = host.Server.Port; run["token"] = host.Server.Token; run["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id;
            File.WriteAllText(Path.Combine(dataDir, "run.json"), new JavaScriptSerializer().Serialize(run));
            host.RefreshMonitors();

            System.Windows.Forms.Timer keepalive = new System.Windows.Forms.Timer();
            keepalive.Interval = 20000;
            keepalive.Tick += delegate { host.Server.Ping(); };
            keepalive.Start();

            Log.Write("started v" + Host.Version + " port " + host.Server.Port + (startup ? " (login)" : ""));
            Application.Run(new TrayApp(host, startup, firstRun, noWall));
            GC.KeepAlive(single);
            return 0;
        }

        static int Cli(string dataDir, string cmd, string arg)
        {
            // A windowed app has no console of its own: borrow the parent's, unless output is already redirected.
            IntPtr stdout = GetStdHandle(-11);
            if (stdout == IntPtr.Zero || stdout == new IntPtr(-1)) AttachConsole(-1);
            if (cmd == "--help" || cmd == "-h" || cmd == "/?") { Console.Write(Usage); return 0; }
            if (cmd == "--version") { Console.WriteLine("AsciiWall " + Host.Version); return 0; }
            string r = Ipc.Send(dataDir, cmd, arg);
            if (r == null) { Console.WriteLine("AsciiWall is not running. Start it from the Start menu first."); return 1; }
            if (r.Length > 0) Console.WriteLine(r);
            return 0;
        }
    }
}
