namespace SirHolomap
{
    // Which grids the map and its list show. The block threshold keeps the
    // debris of a broken grid out of the way; a grid that is part of a larger
    // whole (a wheel on its suspension, a ship locked to a station by a
    // connector, a rotor head) is not debris, however few blocks it has, and
    // is always shown on its own line.
    public static class GridListing
    {
        public static bool Shown(int blocks, int threshold, bool linked)
        {
            return linked || blocks >= threshold;
        }
    }
}
