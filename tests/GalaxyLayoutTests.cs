using System.Collections.Generic;
using Xunit;

namespace SirHolomap.Tests
{
    public class GalaxyLayoutTests
    {
        [Fact]
        public void SameServerAlwaysSamePlace()
        {
            var a = GalaxyPlacement.PositionOf("steam://85.10.200.17:27016");
            var b = GalaxyPlacement.PositionOf("85.10.200.17:27016");
            var c = GalaxyPlacement.PositionOf("  STEAM://85.10.200.17:27016/ ");
            Assert.Equal(a.X, b.X);
            Assert.Equal(a.Y, b.Y);
            Assert.Equal(a.X, c.X);
            Assert.Equal(a.Y, c.Y);

            // Inside the galaxy disc.
            var radius = System.Math.Sqrt(a.X * a.X + a.Y * a.Y);
            Assert.InRange(radius, GalaxyShape.InnerRadius, GalaxyShape.OuterRadius);
        }

        [Fact]
        public void TwoServersNeverSamePlace()
        {
            var seen = new Dictionary<string, string>();

            // Neighbouring ports and neighbouring addresses: the worst case
            // for a careless hash.
            for (var host = 0; host < 64; host++)
            {
                for (var port = 27010; port < 27080; port++)
                {
                    var address = "10.0.0." + host + ":" + port;
                    var p = GalaxyPlacement.PositionOf(address);
                    var key = p.X.ToString("R") + "|" + p.Y.ToString("R");
                    Assert.False(seen.ContainsKey(key), address + " collides with " + (seen.ContainsKey(key) ? seen[key] : ""));
                    seen[key] = address;
                }
            }

            // The packing is exact for IPv4 and port, and the mix reversible.
            var keys = new HashSet<ulong>();
            for (ulong k = 0; k < 20000; k++)
                Assert.True(keys.Add(GalaxyPlacement.Mix48(k * 7919)));
        }

        [Fact]
        public void CloseIconsAreClustered()
        {
            var icons = new List<ScreenIcon>
            {
                new ScreenIcon(0, 100, 100),
                new ScreenIcon(1, 104, 103),
                new ScreenIcon(2, 99, 108),
                new ScreenIcon(3, 400, 300),
            };

            var clusters = IconClustering.Cluster(icons, 16);
            Assert.Equal(2, clusters.Count);
            var crowded = clusters[0].Count == 3 ? clusters[0] : clusters[1];
            var alone = clusters[0].Count == 3 ? clusters[1] : clusters[0];
            Assert.Equal(3, crowded.Count);
            Assert.Contains(0, crowded.Members);
            Assert.Contains(1, crowded.Members);
            Assert.Contains(2, crowded.Members);
            Assert.Equal(1, alone.Count);

            // Zoomed in, the same icons are far enough apart to stand alone.
            var zoomed = new List<ScreenIcon>();
            foreach (var icon in icons)
                zoomed.Add(new ScreenIcon(icon.Index, icon.X * 8, icon.Y * 8));
            Assert.Equal(4, IconClustering.Cluster(zoomed, 16).Count);
        }
    }
}
