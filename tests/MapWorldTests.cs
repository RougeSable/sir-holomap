using System.Collections.Generic;
using Xunit;

namespace SirHolomap.Tests
{
    // The map reads the planets again every few seconds (MapWorld keeps them
    // in a Registry). What the player clicked must stay the same object, or
    // its highlight drops at the next reading.
    public class MapWorldTests
    {
        private sealed class FakeBody
        {
            public long Id;
            public string Name;
            public double X;
        }

        // One reading of the planets, as MapWorld.ReadBodies does it.
        private static List<FakeBody> Read(Registry<FakeBody> registry, params long[] ids)
        {
            var bodies = new List<FakeBody>();
            registry.BeginRefresh();
            foreach (var id in ids)
            {
                var body = registry.Keep(id);
                body.Name = "Planet " + id;
                body.X += 10;
                bodies.Add(body);
            }
            registry.EndRefresh();
            return bodies;
        }

        [Fact]
        public void SelectedBodySurvivesRefresh()
        {
            var created = 0;
            var registry = new Registry<FakeBody>(id =>
            {
                created++;
                return new FakeBody { Id = id };
            });

            var bodies = Read(registry, 1, 2, 3);
            var selected = bodies[1];

            // Read again, in another order: the same objects, updated.
            var again = Read(registry, 3, 2, 1);
            Assert.Equal(3, created);
            Assert.Same(selected, again[1]);
            Assert.Same(selected, registry.Follow(selected, b => b.Id));
            Assert.Equal(20, selected.X);
            Assert.Contains(again, b => ReferenceEquals(b, selected));

            // A new planet appears: the selection does not move.
            var more = Read(registry, 1, 2, 3, 4);
            Assert.Equal(4, created);
            Assert.Same(selected, more[1]);

            // The selected planet is gone: the selection follows it out.
            Read(registry, 1, 3, 4);
            Assert.Null(registry.Follow(selected, b => b.Id));
            FakeBody gone;
            Assert.False(registry.TryGet(2, out gone));

            // Should it come back, it is a new object, as the game made a new
            // planet.
            var back = Read(registry, 1, 2, 3, 4);
            Assert.NotSame(selected, back[1]);
            Assert.Equal(5, created);
        }
    }
}
