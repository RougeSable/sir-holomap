using System;
using Xunit;

namespace SirHolomap.Tests
{
    // The list of grids shows sub-grids (wheels) and docked grids: a grid
    // joined to another one is never treated as debris.
    public class GridListingTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void LinkedGridIsShownBelowTheThreshold()
        {
            // A wheel has one block; the default threshold of space is five.
            Assert.False(GridListing.Shown(1, 5, false));
            Assert.True(GridListing.Shown(1, 5, true));
        }

        [Fact]
        public void BigGridIsShownWhetherLinkedOrNot()
        {
            Assert.True(GridListing.Shown(40, 5, false));
            Assert.True(GridListing.Shown(40, 5, true));
        }

        [Fact]
        public void LinkedFlagIsKeptInMemoryAndOnDisk()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            memory.BeginScan();
            memory.See(new Sighting
            {
                Id = 7,
                Name = "Wheel",
                Kind = ContactKind.SmallShip,
                Relation = ContactRelation.Own,
                Blocks = 1,
                Radius = 1,
                Position = new Vec3(1, 2, 3),
                Body = "",
                Linked = true,
            }, T0);
            memory.EndScan(new Vec3(0, 0, 0), 3000, T0);

            MemoryContact contact;
            Assert.True(memory.TryGet(7, out contact));
            Assert.True(contact.Linked);

            var loaded = MapMemory.Load("steam://1.2.3.4:27016", memory.Save());
            Assert.True(loaded.TryGet(7, out contact));
            Assert.True(contact.Linked);
        }

        [Fact]
        public void OldMemoryLinesWithoutTheFlagStillLoad()
        {
            var memory = MapMemory.Load("x", MapMemory.Header + "\n1\t0\t1\t5\t3\t1\t2\t3\t" + T0.Ticks + "\t\tShip\n");
            MemoryContact contact;
            Assert.True(memory.TryGet(1, out contact));
            Assert.False(contact.Linked);
        }
    }
}
