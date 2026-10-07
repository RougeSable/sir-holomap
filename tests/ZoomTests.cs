using System;
using Xunit;

namespace SirHolomap.Tests
{
    public class ZoomTests
    {
        [Fact]
        public void ScrollIsLogarithmic()
        {
            // Every notch multiplies the distance by the same factor, at every
            // scale.
            var near = ZoomSteps.Apply(100, -1) / 100;
            var far = ZoomSteps.Apply(1000000, -1) / 1000000;
            Assert.Equal(ZoomSteps.Factor, near, 9);
            Assert.Equal(ZoomSteps.Factor, far, 9);
            Assert.Equal(1.0 / ZoomSteps.Factor, ZoomSteps.Apply(500, 1) / 500, 9);

            // Ten notches out then ten in: back where it started.
            Assert.Equal(250.0, ZoomSteps.Apply(ZoomSteps.Apply(250, -10), 10), 6);

            // From a few dozen metres above the ground of a 60 km planet to the
            // system view: fewer than 30 notches.
            var planetRadius = 60000.0;
            var toNeighbourhood = ZoomSteps.NotchesBetween(MapScales.LowestAltitude + planetRadius * 0.01,
                planetRadius * MapScales.PlanetLeaveRadii - planetRadius);
            var toSystem = ZoomSteps.NotchesBetween(planetRadius * MapScales.PlanetLeaveRadii,
                MapScales.NeighbourhoodToSystem(planetRadius).UpAbove);
            Assert.True(toNeighbourhood + toSystem < 30, (toNeighbourhood + toSystem) + " notches");

            // In space, from a grid filling the screen to the system view.
            Assert.True(ZoomSteps.NotchesBetween(50, MapScales.SystemAbove) < 30);

            // The smoothing moves in log space and lands on the target.
            var d = 100.0;
            for (var i = 0; i < 200; i++)
                d = ZoomSteps.Smooth(d, 10000, 1 / 60.0, 0.12);
            Assert.Equal(10000, d, 3);
        }

        [Fact]
        public void SwitchBetweenNeighbourhoodAndSystemHasHysteresis()
        {
            var change = MapScales.NeighbourhoodToSystem(0);
            Assert.True(change.DownBelow < change.UpAbove);
            Assert.False(change.IsUp);

            // Zooming out past the limit: system view.
            Assert.False(change.Update(change.UpAbove * 0.99));
            Assert.True(change.Update(change.UpAbove * 1.01));
            Assert.True(change.IsUp);

            // Stopping right at the limit, wobbling around it: no flicker.
            Assert.False(change.Update(change.UpAbove * 0.98));
            Assert.False(change.Update(change.UpAbove * 1.02));
            Assert.False(change.Update(change.UpAbove * 0.9));
            Assert.True(change.IsUp);

            // Only well below the limit does the neighbourhood come back.
            Assert.True(change.Update(change.DownBelow * 0.99));
            Assert.False(change.IsUp);
            Assert.False(change.Update(change.DownBelow * 1.05));
            Assert.False(change.IsUp);

            // It depends on the scale, not on what the server sends: a planet
            // as focus moves the limit out with its size.
            var aroundPlanet = MapScales.NeighbourhoodToSystem(120000);
            Assert.True(aroundPlanet.UpAbove > change.UpAbove);

            Assert.Throws<ArgumentException>(() => new ScaleSwitch(10, 10));
        }
    }
}
