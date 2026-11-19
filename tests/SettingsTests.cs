using System;
using System.IO;
using Xunit;

namespace SirHolomap.Tests
{
    public class SettingsTests
    {
        private static void InFolder(Action<string> test)
        {
            var directory = Path.Combine(Path.GetTempPath(), "sir-holomap-settings-" + Guid.NewGuid().ToString("N"));
            try
            {
                test(directory);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void GalaxyFiltersSurviveRestart()
        {
            InFolder(directory =>
            {
                string problem;
                var first = MapSettingsFile.Load(directory, out problem);
                Assert.Null(problem);
                Assert.False(first.GalaxyFilterActive);

                first.GalaxyFavoritesOnly = true;
                first.GalaxyVisitedOnly = false;
                first.GalaxySearch = "sirius";
                first.ShowGps = false;
                MapSettingsFile.Save(directory, first);

                // Un nouveau lancement du jeu : une nouvelle instance relue depuis le disque.
                var next = MapSettingsFile.Load(directory, out problem);
                Assert.Null(problem);
                Assert.True(next.GalaxyFavoritesOnly);
                Assert.False(next.GalaxyVisitedOnly);
                Assert.Equal("sirius", next.GalaxySearch);
                Assert.False(next.ShowGps);
                // Le filtre préféré est toujours actif, donc il se montre.
                Assert.True(next.GalaxyFilterActive);

                // Un fichier abîmé retombe sur les valeurs par défaut, jamais un plantage.
                File.WriteAllText(Path.Combine(directory, MapSettingsFile.FileName), "<oops");
                var damaged = MapSettingsFile.Load(directory, out problem);
                Assert.NotNull(problem);
                Assert.False(damaged.GalaxyFilterActive);
            });
        }

        [Fact]
        public void BlockThresholdsSurviveRestart()
        {
            InFolder(directory =>
            {
                string problem;
                var first = MapSettingsFile.Load(directory, out problem);
                first.PlanetBlockThreshold = 3;
                first.SpaceBlockThreshold = 100;
                MapSettingsFile.Save(directory, first);

                // Un nouveau lancement du jeu, n'importe quel serveur : les mêmes seuils, un pour
                // A et un pour B.
                var next = MapSettingsFile.Load(directory, out problem);
                Assert.Null(problem);
                Assert.Equal(3, next.PlanetBlockThreshold);
                Assert.Equal(100, next.SpaceBlockThreshold);

                // Les valeurs hors de portée sont ramenées dans la portée.
                next.SpaceBlockThreshold = -5;
                MapSettingsFile.Save(directory, next);
                Assert.Equal(MapSettings.ThresholdMin, MapSettingsFile.Load(directory, out problem).SpaceBlockThreshold);

                File.WriteAllText(Path.Combine(directory, MapSettingsFile.FileName), "<oops");
                var damaged = MapSettingsFile.Load(directory, out problem);
                Assert.NotNull(problem);
                Assert.Equal(new MapSettings().SpaceBlockThreshold, damaged.SpaceBlockThreshold);
            });
        }
    }
}
