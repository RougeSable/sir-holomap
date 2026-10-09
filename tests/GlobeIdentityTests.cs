using Xunit;

namespace SirHolomap.Tests
{
    // A painted globe is shown again at once when the player joins the same
    // world back, and only for the very planet it was painted from.
    public class GlobeIdentityTests
    {
        private static GlobeIdentity Earth()
        {
            return new GlobeIdentity(42, "EarthLike", 60000, new Vec3(0, 0, 0));
        }

        [Fact]
        public void SamePlanetAfterReconnectionKeepsItsGlobe()
        {
            var again = new GlobeIdentity(42, "EarthLike", 60000.2, new Vec3(0.3, -0.2, 0.1));
            Assert.True(Earth().Matches(again));
        }

        [Fact]
        public void AnotherPlanetNeverTakesTheGlobe()
        {
            Assert.False(Earth().Matches(new GlobeIdentity(43, "EarthLike", 60000, new Vec3(0, 0, 0))));
            Assert.False(Earth().Matches(new GlobeIdentity(42, "Mars", 60000, new Vec3(0, 0, 0))));
            Assert.False(Earth().Matches(new GlobeIdentity(42, "EarthLike", 30000, new Vec3(0, 0, 0))));
            Assert.False(Earth().Matches(new GlobeIdentity(42, "EarthLike", 60000, new Vec3(1000000, 0, 0))));
            Assert.False(Earth().Matches(default(GlobeIdentity)));
        }

        [Fact]
        public void TabNameShrinksToFitItsButton()
        {
            // Fits: its own scale.
            Assert.Equal(0.66f, TextFit.Scale(80, 0.66f, 120, 0.5f));
            // Too wide: just small enough to fit.
            var scale = TextFit.Scale(160, 0.66f, 120, 0.4f);
            Assert.Equal(0.66f * 120 / 160, scale, 4);
            Assert.True(160 * scale / 0.66f <= 120.001f);
            // Far too wide: never below the floor.
            Assert.Equal(0.5f, TextFit.Scale(1000, 0.66f, 120, 0.5f));
            // Nothing measured: left alone.
            Assert.Equal(0.66f, TextFit.Scale(0, 0.66f, 120, 0.5f));
        }
    }
}
