using System;
using System.Collections.Generic;
using System.IO;

namespace SirHolomap.Tests
{
    // A temporary folder that goes away with the test.
    internal sealed class TempFolder : IDisposable
    {
        public readonly string Path;

        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sir-holomap-reports-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, true);
            }
            catch (Exception)
            {
            }
        }
    }

    // The game's log of the session of 2026-10-27 on PTU, the one that ended
    // on the graphics card (DXGI_ERROR_DEVICE_REMOVED) fifteen seconds after
    // the respawn, written the way the game writes its log: a line per entry
    // ("time - Thread: n -> text"), the stacks and the CRASH INFO block on
    // lines that carry no time. The player data in it is the kind the game
    // really writes: the Windows user, the Steam account, the nickname, the
    // grids, the server.
    internal static class GameLogOf20261027
    {
        public const string WindowsUser = "Jean Dupont";
        public const string SteamId = "76561198012345678";
        public const string Nickname = "RougeSable";
        public const string GridName = "Foreuse Alpha";
        public const string ServerAddress = "203.0.113.7:27016";
        public const string ServerName = "Sirius Immersion PTU";

        public const string ShapeCacheLine = "planet shape cache of EarthLike not ready: Object reference not set to an instance of an object.";
        public const string TerrainLine = "terrain of EarthLike not read: NullReferenceException Object reference not set to an instance of an object.";

        public static readonly DateTime SessionStartUtc = new DateTime(2026, 10, 27, 19, 3, 12, DateTimeKind.Utc);
        public static readonly DateTime CrashedAtUtc = new DateTime(2026, 10, 27, 19, 14, 17, 913, DateTimeKind.Utc);

        private static string Line(int minute, int second, int thread, string text)
        {
            return "2026-10-27 21:" + minute.ToString("00") + ":" + second.ToString("00") + ".100 - Thread: " + thread.ToString().PadLeft(3) + " ->  " + text;
        }

        public static string Text()
        {
            var lines = new List<string>();
            lines.Add(Line(3, 11, 1, "Version: SpaceEngineers 01_208_015"));
            lines.Add(Line(3, 11, 1, "Environment.ProcessorCount: 16"));
            lines.Add(Line(3, 12, 1, "Log file: C:\\Users\\" + WindowsUser + "\\AppData\\Roaming\\SpaceEngineers\\SpaceEngineers_20261027_210311_405.log"));
            lines.Add(Line(3, 13, 1, "Loading game from C:\\Users\\" + WindowsUser + "\\AppData\\Roaming\\SpaceEngineers\\Saves\\" + SteamId + "\\Orbit"));
            lines.Add(Line(3, 15, 1, "Steam user: " + Nickname + " (" + SteamId + ")"));
            lines.Add(Line(3, 20, 1, "Connecting to server " + ServerAddress + " (" + ServerName + ")"));
            lines.Add(Line(3, 22, 1, "Player " + Nickname + " joined the world"));
            lines.Add(Line(3, 30, 1, "Grid '" + GridName + "' loaded for " + Nickname));
            lines.Add(Line(3, 31, 1, "Entity name: " + GridName));

            // Thirty errors of the game, then twenty-five lines of the plugin.
            for (var i = 1; i <= 30; i++)
                lines.Add(Line(4 + i / 10, i, 7, "Error loading audio bank " + i + ": file not found"));
            for (var i = 1; i <= 25; i++)
                lines.Add(Line(8, i, 1, "[sir-holomap] scan " + i + ": 12 contacts"));

            lines.Add(Line(14, 2, 14, "[sir-holomap] " + ShapeCacheLine));
            lines.Add(Line(14, 2, 14, "[sir-holomap] " + TerrainLine));
            lines.Add(Line(14, 17, 1, "CRASH INFO"));
            lines.Add("  Version: 01_208_015");
            lines.Add("  GPU: NVIDIA GeForce RTX 3070");
            lines.Add("  Driver: 31.0.15.5123");
            lines.Add("  Profile folder: C:\\Users\\" + WindowsUser + "\\AppData\\Roaming\\SpaceEngineers");
            lines.Add(Line(14, 17, 1, "Exception occurred: SharpDX.SharpDXException: HRESULT: [0x887A0005], Module: [SharpDX.DXGI], ApiCode: [DXGI_ERROR_DEVICE_REMOVED/DeviceRemoved], Message: The GPU device instance has been suspended."));
            lines.Add("   at SharpDX.Result.CheckError()");
            lines.Add("   at SharpDX.DXGI.SwapChain.Present(Int32 syncInterval, PresentFlags flags)");
            lines.Add("   at VRageRender.MyRender11.Present()");
            lines.Add("   at Sandbox.MySandboxGame.Draw()");
            return string.Join("\n", lines.ToArray()) + "\n";
        }
    }
}
