namespace SirHolomap
{
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
    }
}
