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

                // A new game launch: a new instance read from the disk.
                var next = MapSettingsFile.Load(directory, out problem);
                Assert.Null(problem);
                Assert.True(next.GalaxyFavoritesOnly);
                Assert.False(next.GalaxyVisitedOnly);
                Assert.Equal("sirius", next.GalaxySearch);
                Assert.False(next.ShowGps);
                // The preferred filter is still active, so it is shown.
                Assert.True(next.GalaxyFilterActive);

                // A damaged file falls back to the defaults, never a crash.
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

                // A new game launch, any server: the same thresholds, one for
                // A and one for B.
                var next = MapSettingsFile.Load(directory, out problem);
                Assert.Null(problem);
                Assert.Equal(3, next.PlanetBlockThreshold);
                Assert.Equal(100, next.SpaceBlockThreshold);

                // Out of range values are brought back in range.
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
