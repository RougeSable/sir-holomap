using System;
using Xunit;

namespace SirHolomap.Tests
{
    // The galaxy zooms softly around the cursor, lies on a sparse sky of
    // gentle stars; the plane of the nearby space is hidden by the players'
    // characters as by the bodies; the map gives way when the player dies.
    public class GalaxySkyAndOcclusionTests
    {
        private const double Frame = 1 / 60.0;

        [Fact]
        public void GalaxyZoomGlidesAroundTheCursor()
        {
            var camera = new GalaxyCamera();
            Assert.False(camera.Gliding);

            // One notch in, around a point off the middle.
            const double u = 0.4, v = -0.25;
            var x = camera.ShownX + u / camera.ShownZoom;
            var y = camera.ShownY + v / camera.ShownZoom;
            camera.Wheel(1, u, v);
            Assert.Equal(ZoomSteps.Factor, camera.TargetZoom, 9);

            // Every frame the picture grows a little, never all at once, and
            // the point under the cursor stays under it.
            var previous = camera.ShownZoom;
            var frames = 0;
            var largest = 0.0;
            while (camera.Gliding && frames < 600)
            {
                camera.Update(Frame, GalaxyCamera.HalfLife);
                frames++;
                Assert.True(camera.ShownZoom >= previous);
                largest = Math.Max(largest, camera.ShownZoom / previous);
                previous = camera.ShownZoom;
                double su, sv;
                camera.ToScreen(x, y, out su, out sv);
                Assert.Equal(u, su, 6);
                Assert.Equal(v, sv, 6);
            }
            Assert.True(frames >= 10, frames + " frames");
            Assert.True(largest < 1.15, "a frame grew the picture by " + largest);
            Assert.Equal(camera.TargetZoom, camera.ShownZoom, 9);

            // And back out, one notch, around the same point: where it was.
            camera.Wheel(-1, u, v);
            for (var i = 0; i < 600; i++)
                camera.Update(Frame, GalaxyCamera.HalfLife);
            Assert.Equal(1, camera.ShownZoom, 9);
            Assert.Equal(0, camera.ShownX, 6);
            Assert.Equal(0, camera.ShownY, 6);
        }

        [Fact]
        public void QuickNotchesNeverJump()
        {
            var camera = new GalaxyCamera();
            camera.Wheel(-1, 0.1, 0.2);
            camera.Update(Frame, GalaxyCamera.HalfLife);
            // A quick turn of the wheel at another place, mid glide: the
            // shown view goes on from where it is.
            double beforeX = camera.ShownX, beforeY = camera.ShownY, beforeZoom = camera.ShownZoom;
            camera.Wheel(2, -0.5, 0.3);
            camera.Update(Frame, GalaxyCamera.HalfLife);
            Assert.True(Math.Abs(camera.ShownZoom / beforeZoom - 1) < 0.15);
            Assert.True(Math.Abs(camera.ShownX - beforeX) < 0.1);
            Assert.True(Math.Abs(camera.ShownY - beforeY) < 0.1);
            for (var i = 0; i < 600; i++)
            {
                var zoom = camera.ShownZoom;
                var cx = camera.ShownX;
                camera.Update(Frame, GalaxyCamera.HalfLife);
                Assert.True(Math.Abs(camera.ShownZoom / zoom - 1) < 0.15);
                Assert.True(Math.Abs(camera.ShownX - cx) < 0.1);
            }
            Assert.False(camera.Gliding);
            Assert.Equal(camera.TargetX, camera.ShownX, 9);

            // Dragged mid glide: the picture follows the cursor at once.
            camera.Wheel(1, 0.2, 0.2);
            camera.Update(Frame, GalaxyCamera.HalfLife);
            double pu, pv;
            camera.ToScreen(0.3, 0.1, out pu, out pv);
            camera.Pan(0.05, -0.02);
            double qu, qv;
            camera.ToScreen(0.3, 0.1, out qu, out qv);
            Assert.Equal(pu + 0.05, qu, 9);
            Assert.Equal(pv - 0.02, qv, 9);
        }

        [Fact]
        public void GalaxyArrivesFromCloseOnTheCurrentServer()
        {
            var camera = new GalaxyCamera();
            camera.Arrive(MapScales.GalaxyArrivalZoom, 0.3, -0.2);
            Assert.Equal(MapScales.GalaxyArrivalZoom, camera.ShownZoom, 9);
            double u, v;
            camera.ToScreen(0.3, -0.2, out u, out v);
            Assert.Equal(0, u, 9);
            Assert.Equal(0, v, 9);
            for (var i = 0; i < 600; i++)
                camera.Update(Frame, MapCamera_TransitionHalfLife);
            Assert.False(camera.Gliding);
            Assert.Equal(MapScales.GalaxyHomeZoom, camera.ShownZoom, 9);
            Assert.Equal(0, camera.ShownX, 9);

            // Zoomed in past the way back: the view glides on towards the
            // system, the galaxy fading softly, and hands over once there.
            Assert.Equal(1, camera.LeaveFade(1), 9);
            while (!camera.BackToSystem)
                camera.Wheel(1, 0, 0);
            var from = camera.ShownZoom;
            var fade = camera.LeaveFade(from);
            Assert.Equal(1, fade, 9);
            var steps = 0;
            while (camera.ShownZoom < GalaxyCamera.LeaveAt && steps < 600)
            {
                camera.Update(Frame, GalaxyCamera.HalfLife);
                var next = camera.LeaveFade(from);
                Assert.True(next <= fade && fade - next < 0.25, fade + " then " + next);
                fade = next;
                steps++;
            }
            Assert.True(steps > 5 && steps < 600, steps + " frames");
            Assert.Equal(0, fade, 9);
            camera.Rest();
            Assert.False(camera.BackToSystem);
        }

        private const double MapCamera_TransitionHalfLife = 0.22;

        [Fact]
        public void SkyIsSparseSoftAndGentle()
        {
            var far = StarSky.Layer(StarSky.FarStars, 1);
            var near = StarSky.Layer(StarSky.NearStars, 2);
            // Far fewer stars than a dense field: a hundred or so on screen.
            Assert.InRange(far.Length + near.Length, 40, 120);
            Assert.Equal(far[5].X, StarSky.Layer(StarSky.FarStars, 1)[5].X);

            var tints = new System.Collections.Generic.HashSet<int>();
            foreach (var star in far)
            {
                Assert.InRange(star.X, 0, 1);
                Assert.InRange(star.Y, 0, 1);
                Assert.InRange(star.Size, 1, 2.3);
                Assert.InRange(star.Alpha, 0.3, 0.95);
                // Gentle: no channel far below the others, never a loud hue.
                var lowest = Math.Min(star.R, Math.Min(star.G, star.B));
                var highest = Math.Max(star.R, Math.Max(star.G, star.B));
                Assert.True(lowest >= 170 && highest - lowest <= 85, star.R + "," + star.G + "," + star.B);
                tints.Add(star.R << 16 | star.G << 8 | star.B);
            }
            // White, pale blue, orange, rose: several colours show.
            Assert.True(tints.Count >= 4, tints.Count + " colours");

            // Most stars are small and dim, a few larger.
            var small = 0;
            foreach (var star in far)
            {
                if (star.Size < 1.3)
                    small++;
            }
            Assert.True(small > far.Length / 2);
        }

        [Fact]
        public void CharactersHideWhatIsBehindThem()
        {
            Vec3 bottom, top;
            double radius;
            Occlusion.CharacterCapsule(new Vec3(0, 0, 0), new Vec3(0, 1, 0), 1.8, 0.8, out bottom, out top, out radius);
            Assert.Equal(0.4, radius, 9);
            Assert.Equal(0.4, bottom.Y, 9);
            Assert.Equal(1.4, top.Y, 9);

            var eye = new Vec3(0, 1, 10);
            // Behind the character, from the camera: hidden.
            Assert.True(Occlusion.HiddenByCapsule(eye, new Vec3(0, 1, -5), bottom, top, radius));
            Assert.True(Occlusion.HiddenByCapsule(eye, new Vec3(0, 0.3, -20), bottom, top, radius));
            // Inside it: hidden.
            Assert.True(Occlusion.HiddenByCapsule(eye, new Vec3(0.1, 1, 0), bottom, top, radius));
            // In front of it, or beside it: drawn.
            Assert.False(Occlusion.HiddenByCapsule(eye, new Vec3(0, 1, 2), bottom, top, radius));
            Assert.False(Occlusion.HiddenByCapsule(eye, new Vec3(3, 1, -5), bottom, top, radius));
            Assert.False(Occlusion.HiddenByCapsule(eye, new Vec3(0, 3, -1), bottom, top, radius));
            // A camera inside the character never hides the whole map.
            Assert.False(Occlusion.HiddenByCapsule(new Vec3(0, 1, 0), new Vec3(0, 1, -5), bottom, top, radius));

            // A height out of reason: a person's.
            Occlusion.CharacterCapsule(new Vec3(0, 0, 0), new Vec3(0, 2, 0), 40, double.NaN, out bottom, out top, out radius);
            Assert.Equal(Occlusion.CharacterRadius, radius, 9);
            Assert.Equal(Occlusion.CharacterHeight - radius, top.Y, 9);

            // The bodies keep hiding the plane as before.
            Assert.True(Occlusion.HiddenBySphere(eye, new Vec3(0, 1, -50), new Vec3(0, 1, -20), 5));
            Assert.False(Occlusion.HiddenBySphere(eye, new Vec3(0, 1, -10), new Vec3(0, 1, -20), 5));
        }

        [Fact]
        public void LinesAreLookedAtCloselyWhereACharacterStands()
        {
            var eye = new Vec3(0, 10, 10);
            var towards = new Vec3(0, -10, -10);
            var cosine = Math.Cos(0.05);
            // A long line of the plane passing under the character.
            double t0, t1;
            Assert.True(Occlusion.ConeInterval(eye, new Vec3(-1000, 0, 0), new Vec3(1000, 0, 0), towards, cosine, out t0, out t1));
            Assert.True(t0 < 0.5 && t1 > 0.5);
            Assert.True(t1 - t0 < 0.01, (t1 - t0).ToString());
            // A line far aside: nothing to look at.
            Assert.False(Occlusion.ConeInterval(eye, new Vec3(-1000, 0, 300), new Vec3(1000, 0, 300), towards, cosine, out t0, out t1));
            // A line behind the camera: nothing either.
            Assert.False(Occlusion.ConeInterval(eye, new Vec3(-1000, 20, 20), new Vec3(1000, 20, 20), towards, cosine, out t0, out t1));

            // The segment distance the capsule relies on.
            Assert.Equal(1, Occlusion.SegmentDistanceSquared(new Vec3(0, 0, 0), new Vec3(2, 0, 0), new Vec3(1, 1, -1), new Vec3(1, 1, 1)), 9);
            Assert.Equal(4, Occlusion.SegmentPointDistanceSquared(new Vec3(0, 0, 0), new Vec3(2, 0, 0), new Vec3(4, 0, 0)), 9);
        }

        [Fact]
        public void MapGivesWayWhenThePlayerDies()
        {
            Assert.False(MapLife.MustClose(true, true, false));
            Assert.True(MapLife.MustClose(true, true, true));
            Assert.True(MapLife.MustClose(true, false, false));
            Assert.True(MapLife.MustClose(false, true, false));
        }
    }
}
