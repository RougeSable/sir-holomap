using System;
using System.Collections.Generic;
using System.IO;

namespace SirHolomap.Tests
{
    // Un dossier temporaire qui disparaît avec le test.
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

    // Le journal du jeu de la session du 2026-10-27 sur PTU, celle qui s'est terminée
    // sur la carte graphique (DXGI_ERROR_DEVICE_REMOVED) quinze secondes après
    // la réapparition, écrit comme le jeu écrit son journal : une ligne par entrée
    // (« heure - Thread: n -> texte »), les piles et le bloc CRASH INFO sur des
    // lignes qui ne portent pas d'heure. Les données du joueur qui s'y trouvent sont celles que le jeu
    // écrit vraiment : l'utilisateur Windows, le compte Steam, le pseudo, les
    // grilles, le serveur. Les lignes du jeu restent en anglais, comme le jeu les écrit.
    internal static class GameLogOf20261027
    {
        public const string WindowsUser = "Jean Dupont";
        public const string SteamId = "76561198012345678";
        public const string Nickname = "RougeSable";
        public const string GridName = "Foreuse Alpha";
        public const string ServerAddress = "203.0.113.7:27016";
        public const string ServerName = "Sirius Immersion PTU";

        public const string ShapeCacheLine = "cache de forme de la planète EarthLike pas prêt : La référence d'objet n'est pas définie à une instance d'un objet.";
        public const string TerrainLine = "terrain de EarthLike non lu : NullReferenceException La référence d'objet n'est pas définie à une instance d'un objet.";

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

            // Trente erreurs du jeu, puis vingt-cinq lignes du greffon.
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
