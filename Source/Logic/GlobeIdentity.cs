using System;

namespace SirHolomap
{
    // What makes a painted globe still right for a body: the same planet
    // entity, of the same kind, the same size, at the same place. A globe
    // painted before the player left a world is shown again at once when
    // they join it back, instead of a grey disc while it is painted anew.
    public struct GlobeIdentity
    {
        // A planet does not move: a metre is room enough for rounding.
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

    // The scale a label is drawn at so that it fits a width: its own scale
    // when it fits, smaller when it does not, never below a floor (a label
    // still too wide at the floor is then cut by the caller).
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
