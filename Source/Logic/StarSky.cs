using System;

namespace SirHolomap
{
    // Une étoile du ciel nocturne sur lequel repose la galaxie : où elle se trouve dans sa tuile
    // (0 à 1 dans les deux sens), sa taille, sa luminosité et sa couleur.
    public struct SkyStar
    {
        public double X;
        public double Y;
        // 1 pour une étoile ordinaire, jusqu'à un peu plus de 2 pour la plus grande.
        public double Size;
        // Opacité de l'étoile, 0 à 1.
        public double Alpha;
        public byte R;
        public byte G;
        public byte B;
    }

    // Le ciel derrière la galaxie : quelques étoiles éparses, chacune un point
    // rond et doux avec un halo léger (la forme Star), aux couleurs douces : le
    // blanc, le bleu pâle, l'orange chaud et le rose des étoiles d'une carte
    // holographique, jamais dures ni criardes. La plupart des étoiles sont petites et ternes, quelques-unes
    // plus grandes et plus brillantes, pour que la galaxie et les serveurs restent ce que
    // l'œil lit d'abord.
    public static class StarSky
    {
        // Les étoiles de la couche lointaine et de la proche, par tuile : peu, pour
        // que le ciel se lise comme de la profondeur plutôt que comme du bruit.
        public const int FarStars = 70;
        public const int NearStars = 18;

        // Les couleurs douces, et la fréquence de chacune.
        private static readonly byte[][] Tints =
        {
            new byte[] { 255, 247, 236 },   // blanc chaud
            new byte[] { 244, 247, 255 },   // blanc froid
            new byte[] { 198, 218, 255 },   // bleu pâle
            new byte[] { 255, 214, 172 },   // orange clair
            new byte[] { 255, 200, 212 },   // rose
        };

        private static readonly double[] TintShares = { 0.28, 0.22, 0.25, 0.15, 0.10 };

        public static int TintCount
        {
            get { return Tints.Length; }
        }

        // Les étoiles d'une couche, les mêmes pour chaque joueur et chaque
        // ouverture de la carte.
        public static SkyStar[] Layer(int count, int seed)
        {
            var stars = new SkyStar[Math.Max(0, count)];
            for (var n = 0; n < stars.Length; n++)
            {
                var star = new SkyStar();
                star.X = Hash(seed * 7919 + n * 31 + 1);
                star.Y = Hash(seed * 104729 + n * 17 + 2);
                // Peu de grandes, beaucoup de petites ; les grandes plus brillantes.
                var weight = Math.Pow(Hash(seed * 53 + n * 7 + 4), 3);
                star.Size = 1 + 1.2 * weight;
                star.Alpha = 0.32 + 0.5 * weight + 0.12 * Hash(seed * 13 + n * 101 + 3);
                byte r, g, b;
                Tint(Hash(seed * 3 + n * 211 + 5), out r, out g, out b);
                star.R = r;
                star.G = g;
                star.B = b;
                stars[n] = star;
            }
            return stars;
        }

        // Une des couleurs douces, choisie par un nombre de 0 à 1.
        public static void Tint(double pick, out byte r, out byte g, out byte b)
        {
            var index = Tints.Length - 1;
            var sum = 0.0;
            for (var i = 0; i < TintShares.Length; i++)
            {
                sum += TintShares[i];
                if (pick < sum)
                {
                    index = i;
                    break;
                }
            }
            r = Tints[index][0];
            g = Tints[index][1];
            b = Tints[index][2];
        }

        private static double Hash(int n)
        {
            unchecked
            {
                var h = (uint)n;
                h ^= h >> 16;
                h *= 0x7feb352d;
                h ^= h >> 15;
                h *= 0x846ca68b;
                h ^= h >> 16;
                return h / 4294967296.0;
            }
        }
    }
}
