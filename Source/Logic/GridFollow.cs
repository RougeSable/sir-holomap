using System;

namespace SirHolomap
{
    // Following a grid in the neighbourhood view: the gestures that end it.
    // A simple click (left on empty space, or right anywhere on the map)
    // lets go of the grid; a drag only turns the view around it.
    public static class FollowGestures
    {
        // A press released closer than this to where it started is a click;
        // farther, a drag. In pixels at the reference resolution.
        public const double ClickSlopPixels = 5;

        // Room around the frame drawn about the followed grid that still
        // counts as the grid: a click there keeps the follow.
        public const double FrameMarginPixels = 10;

        public static bool IsClick(double pressX, double pressY, double releaseX, double releaseY, double scale)
        {
            var dx = releaseX - pressX;
            var dy = releaseY - pressY;
            return Math.Sqrt(dx * dx + dy * dy) <= ClickSlopPixels * Math.Max(scale, 0.01);
        }

        // Inside the square frame of side `size` drawn around the followed
        // grid, centred on it on screen.
        public static bool InsideFrame(double x, double y, double centreX, double centreY, double size, double scale)
        {
            var half = Math.Max(size, 0) / 2 + FrameMarginPixels * Math.Max(scale, 0.01);
            return Math.Abs(x - centreX) <= half && Math.Abs(y - centreY) <= half;
        }
    }

    // The virtual plane of the neighbourhood view. It is laid through the
    // point the view is centred on; while the camera follows a grid, it stays
    // where it was and as it was before the double click, so that it never
    // moves against the planets, and the height line of the followed grid
    // grows and shrinks as the grid climbs or sinks.
    public sealed class FollowPlane
    {
        public bool Kept { get; private set; }

        // The centre of the plane, a point on it.
        public Vec3 Origin { get; private set; }

        // The camera distance the plane was drawn for: it sets the spacing of
        // the lines and how far they reach.
        public double Distance { get; private set; }

        // Keeps the plane as it is now. A plane already kept stays as it
        // was: following another grid does not move it either.
        public void Keep(Vec3 origin, double distance)
        {
            if (Kept)
                return;
            Kept = true;
            Origin = origin;
            Distance = Math.Max(distance, 1);
        }

        public void Release()
        {
            Kept = false;
        }

        // Where the plane lies this frame: kept, or through the live centre.
        public Vec3 OriginOr(Vec3 live)
        {
            return Kept ? Origin : live;
        }

        public double DistanceOr(double live)
        {
            return Kept ? Distance : live;
        }

        // The point of the plane straight under (or over) a position, along
        // the plane's normal (of length one).
        public static Vec3 Foot(Vec3 position, Vec3 planePoint, Vec3 normal)
        {
            var d = position - planePoint;
            var height = d.X * normal.X + d.Y * normal.Y + d.Z * normal.Z;
            return position - normal * height;
        }
    }
}
