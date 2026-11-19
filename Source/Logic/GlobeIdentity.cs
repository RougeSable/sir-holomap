using System;

namespace SirHolomap
{
    // Ce qui garde un globe peint juste pour un astre : la même entité de
    // planète, du même genre, de la même taille, au même endroit. Un globe
    // peint avant que le joueur quitte un monde se montre de nouveau tout de suite quand
    // il y revient, au lieu d'un disque gris pendant qu'il est repeint.
    public struct GlobeIdentity
    {
        // Une planète ne bouge pas : un mètre laisse assez de place aux arrondis.
        public const double Tolerance = 1.0;

        public readonly long Id;
        public readonly string Type;
        public readonly double Radius;
        public readonly Vec3 Centre;

        public GlobeIdentity(long id, string type, double radius, Vec3 centre)
        {
            Id = id;
            Type = type ?? "";
            Radius = radius;
            Centre = centre;
        }

        public bool Matches(GlobeIdentity other)
        {
            return Id == other.Id
                && string.Equals(Type ?? "", other.Type ?? "", StringComparison.Ordinal)
                && Math.Abs(Radius - other.Radius) <= Tolerance
                && Centre.DistanceTo(other.Centre) <= Tolerance;
        }
    }

    // L'échelle à laquelle on dessine une étiquette pour qu'elle tienne dans une largeur : la sienne
    // quand elle tient, plus petite sinon, jamais sous un plancher (une étiquette
    // encore trop large au plancher est alors coupée par l'appelant).
    public static class TextFit
    {
        public static float Scale(float widthAtScale, float scale, float width, float floor)
        {
            if (widthAtScale <= 0 || width <= 0 || widthAtScale <= width)
                return scale;
            var fitted = scale * width / widthAtScale;
            return Math.Max(Math.Min(fitted, scale), Math.Min(floor, scale));
        }
    }
}
