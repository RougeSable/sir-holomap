using System;
using System.IO;
using System.Xml.Serialization;

namespace SirHolomap
{
    // The player's map settings. They are the same for every server and are
    // kept between game launches.
    public sealed class MapSettings
    {
        public const int ThresholdMin = 1;
        public const int ThresholdMax = 100000;

        // Grids with fewer blocks are hidden: debris of a broken grid make a
        // lot of noise. One value for the planet view (A), one for the
        // neighbourhood and the system (B and C).
        public int PlanetBlockThreshold = 1;
        public int SpaceBlockThreshold = 5;

        // Galaxy tab: both unchecked shows every server known.
        public bool GalaxyFavoritesOnly;
        public bool GalaxyVisitedOnly;
        public string GalaxySearch = "";

        // What the planet and neighbourhood views show.
        public bool ShowStations = true;
        public bool ShowShips = true;
        public bool ShowSmallGrids = true;
        public bool ShowPlayers = true;
        public bool ShowGps = true;
        public bool ShowMemories = true;

        // The globe lit from the camera, so that the night side reads too.
        public bool DaylightGlobe = true;

        public MapSettings Copy()
        {
            return (MapSettings)MemberwiseClone();
        }

        public MapSettings Normalized()
        {
            var copy = Copy();
            copy.PlanetBlockThreshold = Clamp(copy.PlanetBlockThreshold);
            copy.SpaceBlockThreshold = Clamp(copy.SpaceBlockThreshold);
            copy.GalaxySearch = (copy.GalaxySearch ?? "").Trim();
            return copy;
        }

        public bool GalaxyFilterActive
        {
            get { return GalaxyFavoritesOnly || GalaxyVisitedOnly || !string.IsNullOrEmpty(GalaxySearch); }
        }

        public static int Clamp(int value)
        {
            return Math.Max(ThresholdMin, Math.Min(ThresholdMax, value));
        }
    }

    // Reading and writing the settings. A missing or unreadable file never
    // blocks the game: the defaults are used instead.
    public static class MapSettingsFile
    {
        public const string FileName = "settings.xml";

        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(MapSettings));

        public static MapSettings Load(string directory, out string problem)
        {
            problem = null;
            var path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
                return new MapSettings();
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var read = Serializer.Deserialize(stream) as MapSettings;
                    if (read == null)
                    {
                        problem = "empty settings file " + path;
                        return new MapSettings();
                    }
                    return read.Normalized();
                }
            }
            catch (Exception e)
            {
                problem = "unreadable settings file " + path + " (" + e.Message + ")";
                return new MapSettings();
            }
        }

        public static void Save(string directory, MapSettings settings)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, FileName);
            var temporary = path + ".tmp";
            using (var stream = File.Create(temporary))
                Serializer.Serialize(stream, settings.Normalized());
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }
    }
}
