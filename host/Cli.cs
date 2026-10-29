using System;

namespace AsciiWall
{
    /// <summary>AsciiWallCtl.exe: a console program for scripts, terminals and launchers (Stream Deck, AutoHotkey, Task Scheduler).</summary>
    static class Ctl
    {
        const string Usage =
            "AsciiWallCtl - control a running AsciiWall\r\n\r\n" +
            "  AsciiWallCtl --next | --prev | --random\r\n" +
            "  AsciiWallCtl --scene <name>     switch scene (names from --list)\r\n" +
            "  AsciiWallCtl --pause | --resume | --toggle-pause\r\n" +
            "  AsciiWallCtl --list             print scene names\r\n" +
            "  AsciiWallCtl --status           print the current state\r\n" +
            "  AsciiWallCtl --settings         open the settings window\r\n" +
            "  AsciiWallCtl --restart | --quit\r\n";

        static int Main(string[] args)
        {
            string cmd = null, arg = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--scene" && i + 1 < args.Length) { cmd = "scene"; arg = args[++i]; }
                else if (a.StartsWith("--", StringComparison.Ordinal)) cmd = a.Substring(2);
            }
            if (cmd == null || cmd == "help") { Console.Write(Usage); return cmd == null ? 1 : 0; }
            string r = Ipc.Send(Ipc.DataDir(), cmd, arg);
            if (r == null) { Console.Error.WriteLine("AsciiWall is not running."); return 1; }
            if (r.Length > 0) Console.WriteLine(r);
            return 0;
        }
    }
}
