using System;
using System.Collections.Generic;

namespace SirHolomap
{
    // A planet or a moon, as the system view knows it.
    public sealed class BodyInfo
    {
        public long Id;
        public string Name = "";
        public Vec3 Position;
        public double Radius;
    }

    // A planet and the moons that follow it, in the flat system view.
    public sealed class BodyRow
    {
        public BodyInfo Planet;
        public double DistanceToCentre;
        public readonly List<BodyInfo> Moons = new List<BodyInfo>();
    }

    public static class BodyOrdering
    {
        // A body is the moon of a planet when it is clearly smaller and close
        // to it, a few planet radii away.
        public const double MoonSizeRatio = 0.6;
        public const double MoonDistanceRadii = 6.0;

        // Planets by distance to the centre of the world, each followed by its
        // moons, closest moon first.
        public static List<BodyRow> Order(IList<BodyInfo> bodies)
        {
            var parents = new Dictionary<BodyInfo, BodyInfo>();
            foreach (var body in bodies)
            {
                BodyInfo best = null;
                var bestDistance = double.MaxValue;
                foreach (var other in bodies)
                {
                    if (ReferenceEquals(other, body) || body.Radius > other.Radius * MoonSizeRatio)
                        continue;
                    var distance = body.Position.DistanceTo(other.Position);
                    if (distance <= other.Radius * MoonDistanceRadii && distance < bestDistance)
                    {
                        best = other;
                        bestDistance = distance;
                    }
                }
                if (best != null)
                    parents[body] = best;
            }

            // A moon of a moon is listed with the planet at the top.
            var rows = new List<BodyRow>();
            var byPlanet = new Dictionary<BodyInfo, BodyRow>();
            foreach (var body in bodies)
            {
                if (parents.ContainsKey(body))
                    continue;
                var row = new BodyRow { Planet = body, DistanceToCentre = body.Position.Length };
                rows.Add(row);
                byPlanet[body] = row;
            }
            foreach (var pair in parents)
            {
                var top = pair.Value;
                var guard = 0;
                while (parents.ContainsKey(top) && guard++ < 16)
                    top = parents[top];
                BodyRow row;
                if (byPlanet.TryGetValue(top, out row))
                    row.Moons.Add(pair.Key);
            }

            rows.Sort((a, b) => a.DistanceToCentre.CompareTo(b.DistanceToCentre));
            foreach (var row in rows)
            {
                var planet = row.Planet;
                row.Moons.Sort((a, b) => a.Position.DistanceTo(planet.Position).CompareTo(b.Position.DistanceTo(planet.Position)));
            }
            return rows;
        }
    }
}
