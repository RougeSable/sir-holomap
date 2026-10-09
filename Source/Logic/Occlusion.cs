using System;

namespace SirHolomap
{
    // What hides the lines of the map drawn over the game's picture: the map
    // draws its plane over the scene, so whatever part of it lies inside a
    // solid thing, or behind it from the camera, is left out. A body is a
    // sphere; a character is a capsule, a segment from its feet to its head
    // with a radius around it, close to the shape of a standing person.
    public static class Occlusion
    {
        // Height and radius of a character when the game gives nothing
        // better.
        public const double CharacterHeight = 1.85;
        public const double CharacterRadius = 0.38;

        // Inside the sphere, or behind it from the eye.
        public static bool HiddenBySphere(Vec3 eye, Vec3 point, Vec3 centre, double radius)
        {
            var r2 = radius * radius;
            if (DistanceSquared(point, centre) < r2)
                return true;
            return SegmentPointDistanceSquared(eye, point, centre) < r2;
        }

        // Inside the capsule, or behind it from the eye. An eye inside the
        // capsule sees through it: the camera never hides the whole map.
        public static bool HiddenByCapsule(Vec3 eye, Vec3 point, Vec3 bottom, Vec3 top, double radius)
        {
            var r2 = radius * radius;
            if (SegmentPointDistanceSquared(bottom, top, eye) < r2)
                return false;
            return SegmentDistanceSquared(eye, point, bottom, top) < r2;
        }

        // The capsule of a standing character: its feet, the way up its body,
        // and its height. A height out of reason falls back to a person's.
        public static void CharacterCapsule(Vec3 feet, Vec3 up, double height, double width,
            out Vec3 bottom, out Vec3 top, out double radius)
        {
            var length = up.Length;
            up = length > 1e-9 ? up * (1 / length) : new Vec3(0, 1, 0);
            if (double.IsNaN(height) || height < 0.8 || height > 4)
                height = CharacterHeight;
            // The game's box of a character is as wide as its collision,
            // wider than the body itself: a little less, for a slim shape.
            radius = double.IsNaN(width) || width < 0.3 || width > 3 ? CharacterRadius : Math.Max(0.3, Math.Min(width / 2, 0.42));
            radius = Math.Min(radius, height / 2);
            bottom = feet + up * radius;
            top = feet + up * (height - radius);
        }

        // The part of a segment [a, b] seen from the eye within an angle of a
        // direction (a cone around it, cosine given): only there can a small
        // thing in that direction hide the segment, so only there need the
        // segment be looked at closely. t0 and t1 along the segment, 0 at a
        // and 1 at b; false when the segment stays out of the cone.
        public static bool ConeInterval(Vec3 eye, Vec3 a, Vec3 b, Vec3 direction, double cosine, out double t0, out double t1)
        {
            t0 = 0;
            t1 = 0;
            var length = direction.Length;
            if (length < 1e-12)
                return false;
            var d = direction * (1 / length);
            var c2 = cosine * cosine;
            var w0 = a - eye;
            var ab = b - a;
            var dw = Dot(d, w0);
            var dab = Dot(d, ab);
            var alpha = dab * dab - c2 * Dot(ab, ab);
            var beta = 2 * dw * dab - 2 * c2 * Dot(w0, ab);
            var gamma = dw * dw - c2 * Dot(w0, w0);

            // Where the answer may change: the ends, where the segment crosses
            // the cone, where it passes the eye's side.
            var cuts = new double[6];
            var count = 0;
            cuts[count++] = 0;
            cuts[count++] = 1;
            if (Math.Abs(alpha) > 1e-18)
            {
                var disc = beta * beta - 4 * alpha * gamma;
                if (disc >= 0)
                {
                    var root = Math.Sqrt(disc);
                    cuts[count++] = (-beta - root) / (2 * alpha);
                    cuts[count++] = (-beta + root) / (2 * alpha);
                }
            }
            else if (Math.Abs(beta) > 1e-18)
            {
                cuts[count++] = -gamma / beta;
            }
            if (Math.Abs(dab) > 1e-18)
                cuts[count++] = -dw / dab;
            var list = new System.Collections.Generic.List<double>();
            for (var i = 0; i < count; i++)
            {
                if (!double.IsNaN(cuts[i]) && cuts[i] >= 0 && cuts[i] <= 1)
                    list.Add(cuts[i]);
            }
            list.Sort();

            var found = false;
            for (var i = 0; i + 1 < list.Count; i++)
            {
                if (list[i + 1] - list[i] < 1e-12)
                    continue;
                var t = (list[i] + list[i + 1]) / 2;
                var w = w0 + ab * t;
                var along = Dot(d, w);
                if (along < 0 || along * along < c2 * Dot(w, w))
                    continue;
                if (!found)
                    t0 = list[i];
                t1 = list[i + 1];
                found = true;
            }
            return found;
        }

        // Squared distance from a point to a segment.
        public static double SegmentPointDistanceSquared(Vec3 a, Vec3 b, Vec3 point)
        {
            var ab = b - a;
            var length2 = Dot(ab, ab);
            var t = length2 > 1e-18 ? Clamp01(Dot(point - a, ab) / length2) : 0;
            return DistanceSquared(a + ab * t, point);
        }

        // Squared distance between two segments.
        public static double SegmentDistanceSquared(Vec3 p1, Vec3 q1, Vec3 p2, Vec3 q2)
        {
            var d1 = q1 - p1;
            var d2 = q2 - p2;
            var r = p1 - p2;
            var a = Dot(d1, d1);
            var e = Dot(d2, d2);
            var f = Dot(d2, r);
            const double tiny = 1e-18;
            double s, t;
            if (a <= tiny && e <= tiny)
                return Dot(r, r);
            if (a <= tiny)
            {
                s = 0;
                t = Clamp01(f / e);
            }
            else
            {
                var c = Dot(d1, r);
                if (e <= tiny)
                {
                    t = 0;
                    s = Clamp01(-c / a);
                }
                else
                {
                    var b = Dot(d1, d2);
                    var denominator = a * e - b * b;
                    s = denominator > tiny ? Clamp01((b * f - c * e) / denominator) : 0;
                    t = (b * s + f) / e;
                    if (t < 0)
                    {
                        t = 0;
                        s = Clamp01(-c / a);
                    }
                    else if (t > 1)
                    {
                        t = 1;
                        s = Clamp01((b - c) / a);
                    }
                }
            }
            return DistanceSquared(p1 + d1 * s, p2 + d2 * t);
        }

        private static double Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        private static double DistanceSquared(Vec3 a, Vec3 b)
        {
            var d = a - b;
            return Dot(d, d);
        }

        private static double Clamp01(double v)
        {
            return v < 0 ? 0 : (v > 1 ? 1 : v);
        }
    }

    // When the map gives way to the game: the world left, or the player's
    // character dead (or gone, waiting to be born again). The game's own
    // screen of respawn must then show and work as without the map.
    public static class MapLife
    {
        public static bool MustClose(bool inWorld, bool hasCharacter, bool characterDead)
        {
            return !inWorld || !hasCharacter || characterDead;
        }
    }
}
