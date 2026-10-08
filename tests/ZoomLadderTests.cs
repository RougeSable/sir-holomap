using System;
using Xunit;

namespace SirHolomap.Tests
{
    // The wheel glides from the planet (A) to the neighbourhood (B) to the
    // system (C). The switches depend on the scale only, with a gap between
    // the way up and the way down, and every notch multiplies the distance.
    public class ZoomLadderTests
    {
        [Fact]
        public void EachWheelStepMultipliesDistance()
        {
            // Every notch multiplies the distance by the same factor, at every
            // scale.
            var near = ZoomSteps.Apply(100, -1) / 100;
            var far = ZoomSteps.Apply(1000000, -1) / 1000000;
            Assert.Equal(ZoomSteps.Factor, near, 9);
            Assert.Equal(ZoomSteps.Factor, far, 9);
            Assert.Equal(1.0 / ZoomSteps.Factor, ZoomSteps.Apply(500, 1) / 500, 9);
            Assert.Equal(1.0, ZoomSteps.NotchesFromWheel(ZoomSteps.WheelUnitsPerNotch), 9);

            // Ten notches out then ten in: back where it started.
            Assert.Equal(250.0, ZoomSteps.Apply(ZoomSteps.Apply(250, -10), 10), 6);

            // From the lowest point above the ground of a planet to the
            // system view: twenty notches at most, for a small planet and for
            // a large one.
            foreach (var radius in new[] { 19000.0, 60000.0, 120000.0 })
            {
                var notches = NotchesFromGroundToSystem(radius);
                Assert.True(notches <= 20, notches + " notches for a planet of " + radius + " m");
            }

            // In space, from a grid filling the screen to the system view.
            Assert.True(ZoomSteps.NotchesBetween(50, MapScales.SystemAbove) <= 20);

            // The smoothing moves in log space and lands on the target.
            var d = 100.0;
            for (var i = 0; i < 200; i++)
                d = ZoomSteps.Smooth(d, 10000, 1 / 60.0, 0.12);
            Assert.Equal(10000, d, 3);
        }

        // Turns the wheel out one notch at a time, the way the views do: A
        // zooms its altitude above the ground, B and C their distance to the
        // centre of what they look at.
        private static int NotchesFromGroundToSystem(double radius)
        {
            var ladder = new ZoomLadder(radius, ZoomRung.Planet);
            var altitude = MapScales.LowestAltitude;
            var distance = radius + altitude;
            var notches = 0;
            while (ladder.Rung != ZoomRung.System && notches < 100)
            {
                notches++;
                if (ladder.Rung == ZoomRung.Planet)
                {
                    altitude = Math.Min(ZoomSteps.Apply(altitude, -1), MapScales.HighestAltitude(radius));
                    distance = radius + altitude;
                }
                else
                {
                    distance = ZoomSteps.Apply(distance, -1);
                }
                var before = ladder.Rung;
                if (ladder.Update(distance) != before)
                    distance = ladder.Entry(ladder.Rung, distance);
            }
            return notches;
        }

        // Both switches, A to B and B to C, depend on the scale only and keep
        // a gap between the way up and the way down.
        [Fact]
        public void SwitchesDependOnScaleWithHysteresis()
        {
            PlanetToLocalSwitchHasHysteresis();
            LocalToSystemSwitchHasHysteresis();
            SystemToGalaxySwitchHasHysteresis();
        }

        // Out of the system (C) to the galaxy (D): past a few times the size
        // of the system, with the same gap between the way out and the way
        // back.
        private static void SystemToGalaxySwitchHasHysteresis()
        {
            const double extent = 5800000;
            var change = MapScales.SystemToGalaxy(extent);
            Assert.True(change.DownBelow < change.UpAbove);
            Assert.True(change.UpAbove >= extent * MapScales.GalaxyAboveExtent);

            Assert.False(change.Update(change.UpAbove * 0.99));
            Assert.True(change.Update(change.UpAbove * 1.01));
            Assert.True(change.IsUp);

            // Wobbling at the limit keeps the galaxy.
            Assert.False(change.Update(change.UpAbove * 0.98));
            Assert.False(change.Update(change.DownBelow * 1.01));
            Assert.True(change.IsUp);

            // Back to the system only well below.
            Assert.True(change.Update(change.DownBelow * 0.99));
            Assert.False(change.IsUp);

            // A small system still leaves room between C and D.
            Assert.True(MapScales.SystemToGalaxy(0).DownBelow > MapScales.SystemAbove);

            // From the system to the galaxy takes a handful of notches.
            Assert.True(ZoomSteps.NotchesBetween(extent * 1.6, change.UpAbove) <= 6);
        }

        private static void PlanetToLocalSwitchHasHysteresis()
        {
            const double radius = 60000;
            var ladder = new ZoomLadder(radius, ZoomRung.Planet);
            var change = ladder.PlanetSwitch;
            Assert.True(change.DownBelow < change.UpAbove);

            // Zooming out of the globe: the neighbourhood past the limit.
            Assert.Equal(ZoomRung.Planet, ladder.Update(change.UpAbove * 0.99));
            Assert.Equal(ZoomRung.Local, ladder.Update(change.UpAbove * 1.01));

            // Stopping right at the limit, wobbling around it: no flicker.
            Assert.Equal(ZoomRung.Local, ladder.Update(change.UpAbove * 0.98));
            Assert.Equal(ZoomRung.Local, ladder.Update(change.UpAbove * 1.02));
            Assert.Equal(ZoomRung.Local, ladder.Update(change.DownBelow * 1.01));

            // Only well below the limit does the globe come back.
            Assert.Equal(ZoomRung.Planet, ladder.Update(change.DownBelow * 0.99));
            Assert.Equal(ZoomRung.Planet, ladder.Update(change.DownBelow * 1.05));

            // A view entered from its neighbour starts inside the gap: the
            // next look at the same distance keeps it, so two views never
            // take turns while the camera glides.
            var entered = ladder.Entry(ZoomRung.Planet, change.DownBelow * 0.999);
            Assert.True(entered < change.DownBelow);
            Assert.Equal(ZoomRung.Planet, ladder.Update(entered));
            ladder.Update(change.UpAbove * 1.2);
            Assert.Equal(ZoomRung.Local, ladder.Rung);
            var back = ladder.Entry(ZoomRung.Local, change.UpAbove * 1.0001);
            Assert.True(back > change.UpAbove);
            Assert.Equal(ZoomRung.Local, ladder.Update(back));

            // The wheel turned out then in, notch after notch, around the
            // limit: exactly one change each way.
            var fresh = new ZoomLadder(radius, ZoomRung.Planet);
            var distance = radius * 1.5;
            var changes = 0;
            for (var i = 0; i < 12; i++)
                changes += Step(fresh, ref distance, -1);
            Assert.Equal(ZoomRung.System, fresh.Rung);
            Assert.Equal(2, changes);
            for (var i = 0; i < 12; i++)
                changes += Step(fresh, ref distance, 1);
            Assert.Equal(ZoomRung.Planet, fresh.Rung);
            Assert.Equal(4, changes);

            // Without a planet there is no globe to go down to.
            var space = new ZoomLadder(0, ZoomRung.Planet);
            Assert.Equal(ZoomRung.Local, space.Rung);
            Assert.False(space.HasPlanet);
            Assert.Equal(ZoomRung.Local, space.Update(1));

            Assert.Throws<ArgumentException>(() => new ScaleSwitch(10, 10));
        }

        private static int Step(ZoomLadder ladder, ref double distance, double notches)
        {
            distance = ZoomSteps.Apply(distance, notches);
            var before = ladder.Rung;
            if (ladder.Update(distance) == before)
                return 0;
            distance = ladder.Entry(ladder.Rung, distance);
            return 1;
        }

        private static void LocalToSystemSwitchHasHysteresis()
        {
            var ladder = new ZoomLadder(0, ZoomRung.Local);
            var change = ladder.SystemSwitch;
            Assert.True(change.DownBelow < change.UpAbove);

            // Zooming out past the limit: system view.
            Assert.Equal(ZoomRung.Local, ladder.Update(change.UpAbove * 0.99));
            Assert.Equal(ZoomRung.System, ladder.Update(change.UpAbove * 1.01));

            // Stopping right at the limit, wobbling around it: no flicker.
            Assert.Equal(ZoomRung.System, ladder.Update(change.UpAbove * 0.98));
            Assert.Equal(ZoomRung.System, ladder.Update(change.UpAbove * 1.02));
            Assert.Equal(ZoomRung.System, ladder.Update(change.UpAbove * 0.9));

            // Only well below the limit does the neighbourhood come back.
            Assert.Equal(ZoomRung.Local, ladder.Update(change.DownBelow * 0.99));
            Assert.Equal(ZoomRung.Local, ladder.Update(change.DownBelow * 1.05));

            // Entering C from B, or B from C, starts inside the gap.
            Assert.True(ladder.Entry(ZoomRung.System, change.UpAbove) > change.UpAbove);
            Assert.True(ladder.Entry(ZoomRung.Local, change.UpAbove) < change.DownBelow);

            // It depends on the scale, not on what the server sends: a planet
            // as focus moves the limit out with its size.
            var aroundPlanet = new ZoomLadder(120000, ZoomRung.Local);
            Assert.True(aroundPlanet.SystemSwitch.UpAbove > change.UpAbove);
            Assert.True(aroundPlanet.SystemSwitch.DownBelow > aroundPlanet.PlanetSwitch.UpAbove);
        }

        [Fact]
        public void LeavingPlanetCentreKeepsSystemThreshold()
        {
            // B centred on a planet, just past the way out of A: moving the
            // camera off the planet's centre drops the way down to A but
            // never sends the view to C by itself.
            const double radius = 60000;
            var ladder = new ZoomLadder(radius, ZoomRung.Local);
            var distance = ladder.Entry(ZoomRung.Local, radius * (MapScales.PlanetLeaveRadii + 0.3));
            var up = ladder.SystemSwitch.UpAbove;
            Assert.Equal(ZoomRung.Local, ladder.Update(distance));

            ladder.LeavePlanet();
            Assert.False(ladder.HasPlanet);
            Assert.Equal(up, ladder.SystemSwitch.UpAbove);
            distance = ladder.Entry(ZoomRung.Local, distance);
            Assert.Equal(ZoomRung.Local, ladder.Update(distance));
            Assert.Equal(ZoomRung.Local, ladder.Update(distance * 1.01));

            // Zooming in never drops to A any more; out, C as before.
            Assert.Equal(ZoomRung.Local, ladder.Update(10));
            Assert.Equal(ZoomRung.System, ladder.Update(up * 1.01));
        }
    }
}
