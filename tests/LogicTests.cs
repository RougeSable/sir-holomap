using System;
using System.Collections.Generic;
using Xunit;

namespace SirHolomap.Tests
{
    public class ClickTests
    {
        [Fact]
        public void SingleClickWaitsForTheDoubleClickWindow()
        {
            var clicks = new ClickTracker();
            object target;
            Assert.Equal(ClickKind.None, clicks.Click(10, 10, 1.0, "planet", out target));
            Assert.Equal(ClickKind.None, clicks.Poll(1.1, out target));
            Assert.Equal(ClickKind.Single, clicks.Poll(1.0 + ClickTracker.DoubleClickSeconds + 0.01, out target));
            Assert.Equal("planet", target);
        }

        [Fact]
        public void DoubleClickNeverFiresTheSingleClick()
        {
            var clicks = new ClickTracker();
            object target;
            clicks.Click(10, 10, 1.0, "grid", out target);
            Assert.Equal(ClickKind.Double, clicks.Click(12, 11, 1.2, "grid", out target));
            Assert.Equal("grid", target);
            Assert.Equal(ClickKind.None, clicks.Poll(5.0, out target));
        }

        [Fact]
        public void TwoClicksFarApartAreTwoSingleClicks()
        {
            var clicks = new ClickTracker();
            object target;
            clicks.Click(10, 10, 1.0, "a", out target);
            Assert.Equal(ClickKind.None, clicks.Click(300, 10, 1.1, "b", out target));
            Assert.Equal(ClickKind.Single, clicks.Poll(2.0, out target));
            Assert.Equal("b", target);
        }
    }

    public class BodyOrderingTests
    {
        [Fact]
        public void PlanetsByDistanceToCentreWithTheirMoons()
        {
            // The vanilla star system, roughly.
            var earth = new BodyInfo { Name = "EarthLike", Position = new Vec3(0, 0, 0), Radius = 60000 };
            var moon = new BodyInfo { Name = "Moon", Position = new Vec3(16384, 136384, -113616), Radius = 9500 };
            var mars = new BodyInfo { Name = "Mars", Position = new Vec3(1031072, 131072, 1631072), Radius = 60000 };
            var europa = new BodyInfo { Name = "Europa", Position = new Vec3(916384, 16384, 1616384), Radius = 9500 };
            var alien = new BodyInfo { Name = "Alien", Position = new Vec3(131072, 131072, 5731072), Radius = 60000 };
            var titan = new BodyInfo { Name = "Titan", Position = new Vec3(36384, 226384, 5796384), Radius = 9500 };
            var triton = new BodyInfo { Name = "Triton", Position = new Vec3(-284463, -2434463, 365536), Radius = 40000 };

            var rows = BodyOrdering.Order(new List<BodyInfo> { titan, mars, moon, triton, alien, europa, earth });
            Assert.Equal(new[] { "EarthLike", "Mars", "Triton", "Alien" }, rows.ConvertAll(r => r.Planet.Name).ToArray());
            Assert.Equal("Moon", rows[0].Moons[0].Name);
            Assert.Equal("Europa", rows[1].Moons[0].Name);
            Assert.Empty(rows[2].Moons);
            Assert.Equal("Titan", rows[3].Moons[0].Name);
        }
    }

    public class AddressTests
    {
        [Fact]
        public void TheSameServerWrittenDifferentlyIsRecognised()
        {
            Assert.Equal("85.10.200.17:27016", ServerAddress.Normalize("steam://85.10.200.17:27016"));
            Assert.Equal("85.10.200.17:27016", ServerAddress.Normalize("85.10.200.17"));
            Assert.Equal("sirius.example.org:27020", ServerAddress.Normalize("Sirius.Example.org:27020"));
        }

        [Fact]
        public void HistoryRoundTrips()
        {
            var history = new ServerHistory();
            var when = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            history.RecordVisit("steam://1.2.3.4:27016", "Sirius Immersion", "1.2.3.4:27016", when);
            var again = ServerHistory.Load(history.Save());
            Assert.Equal("Sirius Immersion", again.Find("steam://1.2.3.4:27016").Name);
            Assert.Equal(when, again.FindByAddress("steam://1.2.3.4:27016").LastVisitUtc);
            Assert.Equal("12 days", TimeAgo.Format(TimeSpan.FromDays(12)));
        }
    }

    public class ImageTests
    {
        [Fact]
        public void PicturesHaveTheirSize()
        {
            Assert.Equal(Images.ShapeSize * Images.ShapeSize * 4, Images.ShapeImage(Images.Shape.Triangle).Length);
            Assert.Equal(64 * 64 * 4, Images.GalaxyImage(64).Length);
            var colors = new float[8 * 4 * 3];
            var heights = new float[8 * 4];
            Assert.Equal(32 * 32 * 4, Images.GlobeImage(32, 8, 4, colors, heights, 60000, 0, null).Length);
        }

        // The globe painted through the map's camera, for when the game's
        // camera cannot be borrowed: what faces the camera is the side of the
        // planet the camera is above, and the sky around stays clear.
        [Fact]
        public void GlobeSeenThroughTheCameraShowsTheSideBelowIt()
        {
            const int columns = 16, rows = 8;
            var colors = new float[columns * rows * 3];
            var heights = new float[columns * rows];
            // Red where x > 0 (longitude around 0), blue elsewhere.
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var lon = (column + 0.5) * 2 * Math.PI / columns;
                    var i = (row * columns + column) * 3;
                    var red = Math.Cos(lon) > 0;
                    colors[i] = red ? 1f : 0f;
                    colors[i + 2] = red ? 0f : 1f;
                }
            }

            const int size = 64;
            var tan = Math.Tan(Math.PI / 6);
            var light = new Vec3(1, 0, 0);
            // Above longitude 0 (on +x), looking at the centre.
            var image = Images.GlobeViewImage(size, size, columns, rows, colors, heights, 60000,
                new Vec3(3, 0, 0), new Vec3(-1, 0, 0), new Vec3(0, 0, -1), new Vec3(0, 1, 0), tan, tan, light, null);
            Assert.Equal(size * size * 4, image.Length);
            var centre = (size / 2 * size + size / 2) * 4;
            Assert.Equal(255, image[centre + 3]);
            Assert.True(image[centre] > 150 && image[centre + 2] < 60);
            Assert.Equal(0, image[3]);

            // Above the other side, lit from there: blue faces the camera.
            var far = Images.GlobeViewImage(size, size, columns, rows, colors, heights, 60000,
                new Vec3(-3, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 0, 1), new Vec3(0, 1, 0), tan, tan, new Vec3(-1, 0, 0), null);
            Assert.True(far[centre + 2] > 150 && far[centre] < 60);
        }
    }

    public class WayBackTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void AFailedJoinOffersTheServerJustLeft()
        {
            var way = new WayBack();
            way.Leave("steam://1.2.3.4:27016", "Sirius", "Other", Start);
            // Joining: nothing to offer.
            Assert.False(way.Update(Start.AddSeconds(2), false, true));
            Assert.False(way.Update(Start.AddSeconds(12), false, true));
            // Back at the menu, idle.
            Assert.False(way.Update(Start.AddSeconds(13), false, false));
            Assert.True(way.Update(Start.AddSeconds(18), false, false));
            Assert.Equal("steam://1.2.3.4:27016", way.BackConnection);
            Assert.Equal("Sirius", way.BackName);
            // Once only.
            Assert.False(way.Update(Start.AddSeconds(30), false, false));
        }

        [Fact]
        public void AJoinThatWorksOffersNothing()
        {
            var way = new WayBack();
            way.Leave("steam://1.2.3.4:27016", "Sirius", "Other", Start);
            Assert.False(way.Update(Start.AddSeconds(5), false, true));
            Assert.False(way.Update(Start.AddSeconds(40), true, false));
            Assert.False(way.Armed);
            Assert.False(way.Update(Start.AddSeconds(60), false, false));
        }

        [Fact]
        public void TheWorldBeingLeftIsNotASuccessfulJoin()
        {
            var way = new WayBack();
            way.Leave("steam://1.2.3.4:27016", "Sirius", "Other", Start);
            Assert.False(way.Update(Start.AddSeconds(0.1), true, false));
            Assert.True(way.Armed);
            Assert.False(way.Update(Start.AddSeconds(11), false, false));
            Assert.True(way.Update(Start.AddSeconds(16), false, false));
        }

        [Fact]
        public void UnknownScreensOfferNothing()
        {
            var way = new WayBack();
            way.Leave("steam://1.2.3.4:27016", "Sirius", "Other", Start);
            Assert.False(way.Update(Start.AddSeconds(30), false, null));
            Assert.False(way.Update(Start.AddSeconds(60), false, null));
        }
    }
}
