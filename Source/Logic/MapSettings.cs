using System;
using System.IO;
using System.Xml.Serialization;

namespace SirHolomap
{
    // Les réglages de la carte du joueur. Ils sont les mêmes pour tous les serveurs et sont
    // gardés d'un lancement du jeu à l'autre.
    public sealed class MapSettings
    {
        public const int ThresholdMin = 1;
        public const int ThresholdMax = 100000;

        // Les grilles de moins de blocs sont cachées : les débris d'une grille brisée font
        // beaucoup de bruit. Une valeur pour la vue Planète (A), une pour
        // le voisinage et le système (B et C).
        public int PlanetBlockThreshold = 1;
        public int SpaceBlockThreshold = 5;

        // Onglet Galaxie : les deux décochées montrent tous les serveurs connus.
        public bool GalaxyFavoritesOnly;
        public bool GalaxyVisitedOnly;
        public string GalaxySearch = "";

        // Ce que les vues Planète et Espace proche montrent.
        public bool ShowStations = true;
        public bool ShowShips = true;
        public bool ShowSmallGrids = true;
        public bool ShowPlayers = true;
        public bool ShowGps = true;
        public bool ShowMemories = true;

        // Le globe éclairé depuis la caméra, pour que la face de nuit se lise aussi.
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

    // Lire et écrire les réglages. Un fichier absent ou illisible ne bloque
    // jamais le jeu : les valeurs par défaut sont utilisées à la place.
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
                        problem = "fichier de réglages vide " + path;
                        return new MapSettings();
                    }
                    return read.Normalized();
                }
            }
            catch (Exception e)
            {
                problem = "fichier de réglages illisible " + path + " (" + e.Message + ")";
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
