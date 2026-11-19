using Xunit;

namespace SirHolomap.Tests
{
    // Suivre une grille dans la vue Espace proche : le plan reste où il
    // était avant le double clic, et un simple clic lâche alors qu'un glissement
    // ne fait que tourner la vue.
    public class GridFollowTests
    {
        private static readonly Vec3 Up = new Vec3(0, 1, 0);

        [Fact]
        public void KeptPlaneDoesNotMoveWithTheFollowedGrid()
        {
            var plane = new FollowPlane();
            var player = new Vec3(1000, 200, -3000);
            plane.Keep(player, 4000);

            // La grille s'envole, en montant : le plan reste où il était.
            var grid = new Vec3(1500, 260, -2500);
            for (var i = 0; i < 10; i++)
            {
                grid = grid + new Vec3(120, 15, -40);
                Assert.Equal(player.X, plane.OriginOr(grid).X, 9);
                Assert.Equal(player.Y, plane.OriginOr(grid).Y, 9);
                Assert.Equal(player.Z, plane.OriginOr(grid).Z, 9);
                Assert.Equal(4000, plane.DistanceOr(30), 9);
            }

            // Suivre une autre grille garde le premier plan.
            plane.Keep(grid, 50);
            Assert.Equal(player.Y, plane.OriginOr(grid).Y, 9);
            Assert.Equal(4000, plane.DistanceOr(30), 9);

            // Lâché : le plan suit de nouveau le centre de la vue.
            plane.Release();
            Assert.False(plane.Kept);
            Assert.Equal(grid.Y, plane.OriginOr(grid).Y, 9);
            Assert.Equal(30, plane.DistanceOr(30), 9);
        }

        [Fact]
        public void HeightLineOfFollowedGridFollowsItsAltitude()
        {
            var plane = new FollowPlane();
            plane.Keep(new Vec3(0, 100, 0), 4000);

            var low = new Vec3(500, 160, 800);
            var high = new Vec3(520, 260, 790);
            var footLow = FollowPlane.Foot(low, plane.Origin, Up);
            var footHigh = FollowPlane.Foot(high, plane.Origin, Up);

            // Le pied reste sur le plan gardé, juste sous la grille.
            Assert.Equal(100, footLow.Y, 9);
            Assert.Equal(100, footHigh.Y, 9);
            Assert.Equal(low.X, footLow.X, 9);
            Assert.Equal(high.Z, footHigh.Z, 9);

            // Monter allonge la ligne, descendre la raccourcit.
            Assert.Equal(60, low.DistanceTo(footLow), 9);
            Assert.Equal(160, high.DistanceTo(footHigh), 9);
        }

        [Fact]
        public void SimpleClickIsToldApartFromDrag()
        {
            Assert.True(FollowGestures.IsClick(400, 300, 400, 300, 1));
            Assert.True(FollowGestures.IsClick(400, 300, 403, 303, 1));
            Assert.False(FollowGestures.IsClick(400, 300, 420, 300, 1));
            // À une plus grande échelle d'interface, la main peut bouger davantage.
            Assert.True(FollowGestures.IsClick(400, 300, 407, 300, 2));
            Assert.False(FollowGestures.IsClick(400, 300, 412, 300, 2));
        }

        [Fact]
        public void ClickInsideFrameKeepsTheFollow()
        {
            // Un cadre de 300 pixels autour d'une grille au milieu de l'écran.
            Assert.True(FollowGestures.InsideFrame(960, 540, 960, 540, 300, 1));
            Assert.True(FollowGestures.InsideFrame(960 + 155, 540 - 155, 960, 540, 300, 1));
            Assert.False(FollowGestures.InsideFrame(960 + 200, 540, 960, 540, 300, 1));
            Assert.False(FollowGestures.InsideFrame(100, 900, 960, 540, 300, 1));
            // Une toute petite grille lointaine a encore un petit cadre à cliquer.
            Assert.True(FollowGestures.InsideFrame(965, 545, 960, 540, 0, 1));
            Assert.False(FollowGestures.InsideFrame(990, 540, 960, 540, 0, 1));
        }
    }
}
