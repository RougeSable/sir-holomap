using System;

namespace SirHolomap
{
    // Une position dans le monde, en mètres. La logique pure de la carte ne
    // dépend pas de la bibliothèque de maths du jeu, pour pouvoir être testée seule.
    public struct Vec3
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);

        public double Length
        {
            get { return Math.Sqrt(X * X + Y * Y + Z * Z); }
        }

        public double DistanceTo(Vec3 other)
        {
            return (this - other).Length;
        }

        public static Vec3 operator +(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vec3 operator -(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vec3 operator *(Vec3 a, double k)
        {
            return new Vec3(a.X * k, a.Y * k, a.Z * k);
        }

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0}, {1:0}, {2:0}", X, Y, Z);
        }
    }
}
