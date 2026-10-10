using System.Collections.Generic;

namespace SirHolomap
{
    // One grid of a mechanical group: grids held together by rotors, pistons,
    // hinges or wheel suspensions. Connectors never make such a group.
    public struct GridPart
    {
        public long Id;
        public int Blocks;
        public bool Static;

        public GridPart(long id, int blocks, bool isStatic)
        {
            Id = id;
            Blocks = blocks;
            Static = isStatic;
        }
    }

    // Which grids the map and its list show. The block threshold keeps the
    // debris of a broken grid out of the way. A sub-grid (a wheel on its
    // suspension, the head of a rotor, a piston or a hinge) is a part of its
    // parent grid: it never has a line of its own, whatever its size. Ships
    // locked together by a connector are separate grids and keep their lines.
    public static class GridListing
    {
        public static bool Shown(int blocks, int threshold, bool subGrid)
        {
            return !subGrid && blocks >= threshold;
        }

        // The parent of a mechanical group: the grid the player sees as the
        // whole, the one with the most blocks. On a tie, a station before a
        // ship, then the oldest grid (the lowest id), so that the choice
        // never flickers from one scan to the next. 0 for an empty group.
        public static long Parent(IList<GridPart> group)
        {
            if (group == null || group.Count == 0)
                return 0;
            var best = group[0];
            for (var i = 1; i < group.Count; i++)
            {
                var part = group[i];
                if (Before(part, best))
                    best = part;
            }
            return best.Id;
        }

        private static bool Before(GridPart a, GridPart b)
        {
            if (a.Blocks != b.Blocks)
                return a.Blocks > b.Blocks;
            if (a.Static != b.Static)
                return a.Static;
            return a.Id < b.Id;
        }

        // True when a grid drawn at (x, y), of the given radius on screen,
        // shows at least partly inside the rectangle of the map.
        public static bool OnScreen(double x, double y, double radius, double left, double top, double right, double bottom)
        {
            if (radius < 0)
                radius = 0;
            return x + radius >= left && x - radius <= right && y + radius >= top && y - radius <= bottom;
        }
    }
}
