using System.Collections.Generic;
using Xunit;

namespace SirHolomap.Tests
{
    // La carte relit les planètes toutes les quelques secondes (MapWorld les garde
    // dans un Registry). Ce que le joueur a cliqué doit rester le même objet, sinon
    // sa surbrillance tombe à la lecture suivante.
    public class MapWorldTests
    {
        private sealed class FakeBody
        {
            public long Id;
            public string Name;
            public double X;
        }

        // Une lecture des planètes, comme MapWorld.ReadBodies la fait.
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

            // Relues dans un autre ordre : les mêmes objets, mis à jour.
            var again = Read(registry, 3, 2, 1);
            Assert.Equal(3, created);
            Assert.Same(selected, again[1]);
            Assert.Same(selected, registry.Follow(selected, b => b.Id));
            Assert.Equal(20, selected.X);
            Assert.Contains(again, b => ReferenceEquals(b, selected));

            // Une nouvelle planète apparaît : la sélection ne bouge pas.
            var more = Read(registry, 1, 2, 3, 4);
            Assert.Equal(4, created);
            Assert.Same(selected, more[1]);

            // La planète sélectionnée a disparu : la sélection la suit dehors.
            Read(registry, 1, 3, 4);
            Assert.Null(registry.Follow(selected, b => b.Id));
            FakeBody gone;
            Assert.False(registry.TryGet(2, out gone));

            // Si elle revenait, ce serait un nouvel objet, comme le jeu a fait une nouvelle
            // planète.
            var back = Read(registry, 1, 2, 3, 4);
            Assert.NotSame(selected, back[1]);
            Assert.Equal(5, created);
        }
    }
}
