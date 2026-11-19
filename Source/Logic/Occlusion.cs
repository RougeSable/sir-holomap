using System;

namespace SirHolomap
{
    // Ce qui cache les lignes de la carte dessinées sur l'image du jeu : la carte
    // dessine son plan sur la scène, donc toute partie qui se trouve à l'intérieur d'une
    // chose solide, ou derrière elle depuis la caméra, est laissée de côté. Un astre est une
    // sphère ; un personnage est une capsule, un segment de ses pieds à sa tête
    // avec un rayon autour, proche de la forme d'une personne debout.
    public static class Occlusion
    {
        // Hauteur et rayon d'un personnage quand le jeu ne donne rien
        // de mieux.
        public const double CharacterHeight = 1.85;
        public const double CharacterRadius = 0.38;

        // À l'intérieur de la sphère, ou derrière elle depuis l'œil.
        public static bool HiddenBySphere(Vec3 eye, Vec3 point, Vec3 centre, double radius)
        {
            var r2 = radius * radius;
            if (DistanceSquared(point, centre) < r2)
                return true;
            return SegmentPointDistanceSquared(eye, point, centre) < r2;
        }

        // À l'intérieur de la capsule, ou derrière elle depuis l'œil. Un œil à l'intérieur de la
        // capsule voit à travers : la caméra ne cache jamais toute la carte.
        public static bool HiddenByCapsule(Vec3 eye, Vec3 point, Vec3 bottom, Vec3 top, double radius)
        {
            var r2 = radius * radius;
            if (SegmentPointDistanceSquared(bottom, top, eye) < r2)
                return false;
            return SegmentDistanceSquared(eye, point, bottom, top) < r2;
        }

        // La capsule d'un personnage debout : ses pieds, le chemin vers le haut de son corps,
        // et sa hauteur. Une hauteur déraisonnable retombe sur celle d'une personne.
        public static void CharacterCapsule(Vec3 feet, Vec3 up, double height, double width,
            out Vec3 bottom, out Vec3 top, out double radius)
        {
            var length = up.Length;
            up = length > 1e-9 ? up * (1 / length) : new Vec3(0, 1, 0);
            if (double.IsNaN(height) || height < 0.8 || height > 4)
                height = CharacterHeight;
            // La boîte du personnage donnée par le jeu est aussi large que sa collision,
            // plus large que le corps lui-même : un peu moins, pour une forme mince.
            radius = double.IsNaN(width) || width < 0.3 || width > 3 ? CharacterRadius : Math.Max(0.3, Math.Min(width / 2, 0.42));
            radius = Math.Min(radius, height / 2);
            bottom = feet + up * radius;
            top = feet + up * (height - radius);
        }

        // La partie d'un segment [a, b] vue de l'œil dans un angle autour d'une
        // direction (un cône autour d'elle, cosinus donné) : c'est là seulement qu'une petite
        // chose dans cette direction peut cacher le segment, donc là seulement qu'il faut
        // regarder le segment de près. t0 et t1 le long du segment, 0 en a
        // et 1 en b ; faux quand le segment reste hors du cône.
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

            // Où la réponse peut changer : les extrémités, là où le segment croise
            // le cône, là où il passe du côté de l'œil.
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

        // Distance au carré d'un point à un segment.
        public static double SegmentPointDistanceSquared(Vec3 a, Vec3 b, Vec3 point)
        {
            var ab = b - a;
            var length2 = Dot(ab, ab);
            var t = length2 > 1e-18 ? Clamp01(Dot(point - a, ab) / length2) : 0;
            return DistanceSquared(a + ab * t, point);
        }

        // Distance au carré entre deux segments.
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

    // Quand la carte cède la place au jeu : le monde quitté, ou le
    // personnage du joueur mort (ou parti, attendant de renaître). L'écran
    // de réapparition du jeu doit alors se montrer et marcher comme sans la carte.
    public static class MapLife
    {
        public static bool MustClose(bool inWorld, bool hasCharacter, bool characterDead)
        {
            return !inWorld || !hasCharacter || characterDead;
        }
    }
}
