using System;
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

            // A game joined without an address keeps its place by its key.
            var lobby = GalaxyPlacement.PositionOf("lobby:90071992547409");
            var again = GalaxyPlacement.PositionOf("lobby:90071992547409");
            Assert.Equal(lobby.X, again.X);
            Assert.Equal(lobby.Y, again.Y);
            Assert.Equal(GalaxyPlacement.KeyOf("steam://85.10.200.17:27016"), GalaxyPlacement.KeyOf("85.10.200.17:27016"));
        }

        [Fact]
        public void DifferentServersNeverSamePlace()
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

        private static double Distance(PlacedIcon a, PlacedIcon b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static void AssertApart(PlacedIcon[] placed, double spacing)
        {
            for (var i = 0; i < placed.Length; i++)
            {
                for (var j = i + 1; j < placed.Length; j++)
                    Assert.True(Distance(placed[i], placed[j]) >= spacing * 0.999,
                        "icons " + i + " and " + j + " are " + Distance(placed[i], placed[j]) + " apart");
            }
        }

        // Close icons gather and spread around the middle of their group: none
        // covers another, each keeps its own spot to be clicked. An icon
        // alone stays exactly at its place.
        [Fact]
        public void CloseIconsSpreadWithoutCovering()
        {
            var icons = new List<ScreenIcon>
            {
                new ScreenIcon(0, 11, 100, 100),
                new ScreenIcon(1, 22, 104, 103),
                new ScreenIcon(2, 33, 99, 108),
                new ScreenIcon(3, 44, 400, 300),
            };

            var placed = GalaxyLayout.Dots(icons, 16);
            Assert.Equal(4, placed.Length);
            AssertApart(placed, 16);
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(i, placed[i].Index);
                Assert.Equal(3, placed[i].GroupSize);
                Assert.True(placed[i].Moved);
                Assert.Equal(icons[i].X, placed[i].TrueX);
                Assert.Equal(icons[i].Y, placed[i].TrueY);
                // Still close to where it truly is.
                Assert.True(Distance(placed[i], new PlacedIcon { X = 101, Y = 103.7 }) < 16);
            }
            Assert.False(placed[3].Moved);
            Assert.Equal(1, placed[3].GroupSize);
            Assert.Equal(400, placed[3].X);
            Assert.Equal(300, placed[3].Y);

            // Zoomed in, the same icons are far enough apart to stand alone.
            var zoomed = new List<ScreenIcon>();
            foreach (var icon in icons)
                zoomed.Add(new ScreenIcon(icon.Index, icon.Key, icon.X * 8, icon.Y * 8));
            foreach (var p in GalaxyLayout.Dots(zoomed, 16))
                Assert.False(p.Moved);
        }

        // However many icons crowd the same spot, every one stays apart from
        // all the others: on top of each other, or scattered in a small area.
        [Fact]
        public void CrowdedIconsNeverOverlap()
        {
            foreach (var count in new[] { 2, 3, 6, 7, 20, 150 })
            {
                var stacked = new List<ScreenIcon>();
                for (var i = 0; i < count; i++)
                    stacked.Add(new ScreenIcon(i, (ulong)(1000 + i), 500, 500));
                AssertApart(GalaxyLayout.Dots(stacked, 12), 12);
            }

            var random = new Random(304);
            var scattered = new List<ScreenIcon>();
            for (var i = 0; i < 400; i++)
                scattered.Add(new ScreenIcon(i, (ulong)random.Next(), random.NextDouble() * 300, random.NextDouble() * 300));
            AssertApart(GalaxyLayout.Dots(scattered, 12), 12);

            // Two neighbours on a ring are never closer than the spacing.
            for (var rank = 1; rank < 300; rank++)
            {
                var chord = 2 * rank * Math.Sin(Math.PI / GalaxyLayout.RingCapacity(rank));
                Assert.True(chord >= 1, "ring " + rank + ": " + chord);
            }
        }

        // The place of an icon depends on the servers and the view only:
        // never on the order of the list, so the current server listed first
        // moves nobody.
        [Fact]
        public void LayoutIgnoresTheOrderOfTheList()
        {
            var random = new Random(7);
            var icons = new List<ScreenIcon>();
            for (var i = 0; i < 60; i++)
                icons.Add(new ScreenIcon(i, (ulong)(random.Next() + 1), 200 + random.NextDouble() * 80, 200 + random.NextDouble() * 80,
                    90 + random.NextDouble() * 120, 38));

            var reversed = new List<ScreenIcon>(icons);
            reversed.Reverse();
            for (var i = 0; i < reversed.Count; i++)
                reversed[i] = new ScreenIcon(i, reversed[i].Key, reversed[i].X, reversed[i].Y, reversed[i].Width, reversed[i].Height);

            var dots = GalaxyLayout.Dots(icons, 12);
            var dotsReversed = GalaxyLayout.Dots(reversed, 12);
            var rows = GalaxyLayout.Rows(icons, 8, 4);
            var rowsReversed = GalaxyLayout.Rows(reversed, 8, 4);
            for (var i = 0; i < icons.Count; i++)
            {
                var j = icons.Count - 1 - i;
                Assert.Equal(dots[i].X, dotsReversed[j].X, 9);
                Assert.Equal(dots[i].Y, dotsReversed[j].Y, 9);
                Assert.Equal(rows[i].X, rowsReversed[j].X, 9);
                Assert.Equal(rows[i].Y, rowsReversed[j].Y, 9);
            }
        }

        private static bool Cover(ScreenIcon a, PlacedIcon pa, ScreenIcon b, PlacedIcon pb, double iconHalf)
        {
            var aLeft = pa.X - iconHalf;
            var bLeft = pb.X - iconHalf;
            return aLeft < bLeft + b.Width && bLeft < aLeft + a.Width
                && pa.Y - a.Height / 2 < pb.Y + b.Height / 2 && pb.Y - b.Height / 2 < pa.Y + a.Height / 2;
        }

        // Servers shown with their name, close to each other: their rows
        // stack in a column, and no name covers another. A server alone keeps
        // its exact place.
        [Fact]
        public void NamedServersNeverCoverEachOther()
        {
            var icons = new List<ScreenIcon>
            {
                new ScreenIcon(0, 501, 300, 300, 180, 38),
                new ScreenIcon(1, 502, 306, 296, 140, 38),
                new ScreenIcon(2, 503, 340, 320, 220, 38),
                new ScreenIcon(3, 504, 900, 700, 160, 38),
            };
            var placed = GalaxyLayout.Rows(icons, 8, 4);
            for (var i = 0; i < icons.Count; i++)
            {
                for (var j = i + 1; j < icons.Count; j++)
                    Assert.False(Cover(icons[i], placed[i], icons[j], placed[j], 8), "rows " + i + " and " + j + " cover each other");
            }
            Assert.True(placed[0].Moved && placed[1].Moved && placed[2].Moved);
            Assert.Equal(3, placed[0].GroupSize);
            // Highest first, in one column.
            Assert.True(placed[1].Y < placed[0].Y && placed[0].Y < placed[2].Y);
            Assert.Equal(placed[0].X, placed[1].X, 9);
            Assert.False(placed[3].Moved);
            Assert.Equal(900, placed[3].X);
            Assert.Equal(700, placed[3].Y);

            // Many named servers in a small area.
            var random = new Random(42);
            var crowd = new List<ScreenIcon>();
            for (var i = 0; i < 40; i++)
                crowd.Add(new ScreenIcon(i, (ulong)(i * 7 + 3), random.NextDouble() * 400, random.NextDouble() * 400,
                    80 + random.NextDouble() * 200, 38));
            var crowdPlaced = GalaxyLayout.Rows(crowd, 8, 4);
            for (var i = 0; i < crowd.Count; i++)
            {
                for (var j = i + 1; j < crowd.Count; j++)
                    Assert.False(Cover(crowd[i], crowdPlaced[i], crowd[j], crowdPlaced[j], 8), "rows " + i + " and " + j + " cover each other");
            }
        }
    }
}
