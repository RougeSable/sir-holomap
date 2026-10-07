using System;
using System.IO;
using Xunit;

namespace SirHolomap.Tests
{
    public class MapSettingsTests
    {
        [Fact]
        public void FiltersAndBlockThresholdsSurviveRestart()
        {
            var directory = Path.Combine(Path.GetTempPath(), "sir-holomap-settings-" + Guid.NewGuid().ToString("N"));
            try
            {
                string problem;
                var first = MapSettingsFile.Load(directory, out problem);
                Assert.Null(problem);
                Assert.False(first.GalaxyFilterActive);

                first.GalaxyFavoritesOnly = true;
                first.GalaxyVisitedOnly = false;
                first.GalaxySearch = "sirius";
                first.PlanetBlockThreshold = 3;
                first.SpaceBlockThreshold = 100;
                first.ShowGps = false;
                MapSettingsFile.Save(directory, first);

                // A new game launch: a new instance read from the disk.
                var next = MapSettingsFile.Load(directory, out problem);
                Assert.Null(problem);
                Assert.True(next.GalaxyFavoritesOnly);
                Assert.False(next.GalaxyVisitedOnly);
                Assert.Equal("sirius", next.GalaxySearch);
                Assert.Equal(3, next.PlanetBlockThreshold);
                Assert.Equal(100, next.SpaceBlockThreshold);
                Assert.False(next.ShowGps);
                Assert.True(next.GalaxyFilterActive);

                // Out of range values are brought back in range.
                next.SpaceBlockThreshold = -5;
                MapSettingsFile.Save(directory, next);
                Assert.Equal(MapSettings.ThresholdMin, MapSettingsFile.Load(directory, out problem).SpaceBlockThreshold);

                // A damaged file falls back to the defaults, never a crash.
                File.WriteAllText(Path.Combine(directory, MapSettingsFile.FileName), "<oops");
                var damaged = MapSettingsFile.Load(directory, out problem);
                Assert.NotNull(problem);
                Assert.Equal(new MapSettings().SpaceBlockThreshold, damaged.SpaceBlockThreshold);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }
    }
}
