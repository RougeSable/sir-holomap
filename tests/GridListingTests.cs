using System;
using System.Collections.Generic;
using Xunit;

namespace SirHolomap.Tests
{
    // The list of grids shows the parent grid only: the sub-grids held by a
    // rotor, a piston, a hinge or a wheel suspension have no line of their own.
    public class GridListingTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void SubGridIsHiddenWhateverItsSize()
        {
            Assert.False(GridListing.Shown(1, 1, true));
            Assert.False(GridListing.Shown(40, 5, true));
        }

        [Fact]
        public void ParentGridIsShownFromTheThreshold()
        {
            Assert.True(GridListing.Shown(40, 5, false));
            Assert.True(GridListing.Shown(5, 5, false));
            Assert.False(GridListing.Shown(4, 5, false));
        }

        [Fact]
        public void TheParentOfAGroupIsItsBiggestGrid()
        {
            // A rover and its four wheels: the rover is the parent, whatever
            // the order the game gives them in.
            var rover = new List<GridPart>
            {
                new GridPart(11, 1, false),
                new GridPart(12, 1, false),
                new GridPart(5, 42, false),
                new GridPart(13, 1, false),
                new GridPart(14, 1, false),
            };
            Assert.Equal(5, GridListing.Parent(rover));
            rover.Reverse();
            Assert.Equal(5, GridListing.Parent(rover));
        }

        [Fact]
        public void ARotorHeadBiggerThanItsBaseIsTheParent()
        {
            // A small base carrying a big ship by a rotor: the ship is what
            // the player sees, it gets the line.
            var group = new List<GridPart> { new GridPart(1, 3, false), new GridPart(2, 120, false) };
            Assert.Equal(2, GridListing.Parent(group));
        }

        [Fact]
        public void TiesNeverFlicker()
        {
            var a = new List<GridPart> { new GridPart(9, 10, false), new GridPart(4, 10, false) };
            var b = new List<GridPart> { new GridPart(4, 10, false), new GridPart(9, 10, false) };
            Assert.Equal(4, GridListing.Parent(a));
            Assert.Equal(4, GridListing.Parent(b));
            var station = new List<GridPart> { new GridPart(4, 10, false), new GridPart(9, 10, true) };
            Assert.Equal(9, GridListing.Parent(station));
        }

        [Fact]
        public void AnEmptyGroupHasNoParent()
        {
            Assert.Equal(0, GridListing.Parent(new List<GridPart>()));
            Assert.Equal(0, GridListing.Parent(null));
        }

        [Fact]
        public void AGridPartlyOnTheMapIsOnScreen()
        {
            Assert.True(GridListing.OnScreen(100, 100, 0, 0, 0, 800, 600));
            // Its centre just past the edge, its hull still in sight.
            Assert.True(GridListing.OnScreen(-20, 100, 50, 0, 0, 800, 600));
            Assert.True(GridListing.OnScreen(830, 100, 50, 0, 0, 800, 600));
            Assert.False(GridListing.OnScreen(-80, 100, 50, 0, 0, 800, 600));
            Assert.False(GridListing.OnScreen(100, 700, 50, 0, 0, 800, 600));
        }

        [Fact]
        public void SubGridFlagIsKeptInMemoryAndOnDisk()
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
                SubGrid = true,
            }, T0);
            memory.EndScan(new Vec3(0, 0, 0), 3000, T0);

            MemoryContact contact;
            Assert.True(memory.TryGet(7, out contact));
            Assert.True(contact.SubGrid);

            var loaded = MapMemory.Load("steam://1.2.3.4:27016", memory.Save());
            Assert.True(loaded.TryGet(7, out contact));
            Assert.True(contact.SubGrid);
        }

        [Fact]
        public void OldMemoryLinesWithoutTheFlagStillLoad()
        {
            var memory = MapMemory.Load("x", MapMemory.Header + "\n1\t0\t1\t5\t3\t1\t2\t3\t" + T0.Ticks + "\t\tShip\n");
            MemoryContact contact;
            Assert.True(memory.TryGet(1, out contact));
            Assert.False(contact.SubGrid);
        }
    }
}
