using System;

namespace SirHolomap
{
    // A star of the night sky the galaxy lies on: where it sits in its tile
    // (0 to 1 both ways), how large and how bright it is, and its colour.
    public struct SkyStar
    {
        public double X;
        public double Y;
        // 1 for an ordinary star, up to a little over 2 for the largest.
        public double Size;
        // Opacity of the star, 0 to 1.
        public double Alpha;
        public byte R;
        public byte G;
        public byte B;
    }

    // The sky behind the galaxy: a few stars, scattered, each one a soft
    // round dot with a light halo (the Star shape), in gentle colours: the
    // white, pale blue, warm orange and rose of the stars of a holographic
    // map, never a hard or loud one. Most stars are small and dim, a few
    // larger and brighter, so that the galaxy and the servers stay what
    // the eye reads first.
    public static class StarSky
    {
        // Stars of the far layer and of the near one, per tile: few, so
        // that the sky reads as depth rather than as noise.
        public const int FarStars = 70;
        public const int NearStars = 18;

        // The gentle colours, and how often each comes.
        private static readonly byte[][] Tints =
        {
            new byte[] { 255, 247, 236 },   // warm white
            new byte[] { 244, 247, 255 },   // cold white
            new byte[] { 198, 218, 255 },   // pale blue
            new byte[] { 255, 214, 172 },   // light orange
            new byte[] { 255, 200, 212 },   // rose
        };

        private static readonly double[] TintShares = { 0.28, 0.22, 0.25, 0.15, 0.10 };

        public static int TintCount
        {
            get { return Tints.Length; }
        }

        // The stars of one layer, the same for every player and every
        // opening of the map.
        public static SkyStar[] Layer(int count, int seed)
        {
            var stars = new SkyStar[Math.Max(0, count)];
            for (var n = 0; n < stars.Length; n++)
            {
                var star = new SkyStar();
                star.X = Hash(seed * 7919 + n * 31 + 1);
                star.Y = Hash(seed * 104729 + n * 17 + 2);
                // Few large ones, many small ones; the large ones brighter.
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

        // One of the gentle colours, picked by a number from 0 to 1.
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
