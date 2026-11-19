using System;
using System.IO;
using Xunit;

namespace SirHolomap.Tests
{
    public class MemoryTests
    {
        private const double SyncRadius = 3000;
        private static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        private static Sighting Base(Vec3 position)
        {
            return new Sighting
            {
                Id = 42,
                Name = "Outpost",
                Kind = ContactKind.Station,
                Relation = ContactRelation.Own,
                Blocks = 350,
                Radius = 30,
                Position = position,
                Body = "EarthLike",
            };
        }

        private static void Scan(MapMemory memory, Vec3 observer, DateTime now, params Sighting[] seen)
        {
            memory.BeginScan();
            foreach (var s in seen)
                memory.See(s, now);
            memory.EndScan(observer, SyncRadius, now);
        }

        [Fact]
        public void RecordsPositionAndTimeOfSeenGrid()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            Scan(memory, new Vec3(0, 0, 0), T0, Base(new Vec3(100, 0, 0)));

            MemoryContact contact;
            Assert.True(memory.TryGet(42, out contact));
            Assert.True(contact.IsLive);
            Assert.Equal(100, contact.Position.X);
            Assert.Equal(T0, contact.LastSeenUtc);
        }

        [Fact]
        public void KeepsLastKnownPositionOutOfRange()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            var basePosition = new Vec3(100, 0, 0);
            Scan(memory, new Vec3(0, 0, 0), T0, Base(basePosition));

            // Le joueur s'éloigne de 50 km : le jeu cesse d'envoyer la base.
            for (var minute = 1; minute <= 30; minute++)
                Scan(memory, new Vec3(50000, 0, 0), T0.AddMinutes(minute));

            MemoryContact contact;
            Assert.True(memory.TryGet(42, out contact));
            Assert.False(contact.IsLive);
            Assert.Equal(basePosition.X, contact.Position.X);
            Assert.Equal(basePosition.Y, contact.Position.Y);
            Assert.Equal(basePosition.Z, contact.Position.Z);
            Assert.Equal(T0, contact.LastSeenUtc);
        }

        [Fact]
        public void UpdatesWhenSeenAgain()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            var ship = Base(new Vec3(100, 0, 0));
            ship.Kind = ContactKind.LargeShip;
            Scan(memory, new Vec3(0, 0, 0), T0, ship);

            // Loin : toujours retenue là où elle était.
            Scan(memory, new Vec3(80000, 0, 0), T0.AddHours(1));

            // Le vaisseau a bougé pendant l'absence ; le joueur le rencontre de nouveau.
            var moved = ship;
            moved.Position = new Vec3(81000, 500, 0);
            var back = T0.AddHours(2);
            Scan(memory, new Vec3(80000, 0, 0), back, moved);

            MemoryContact contact;
            Assert.True(memory.TryGet(42, out contact));
            Assert.True(contact.IsLive);
            Assert.Equal(81000, contact.Position.X);
            Assert.Equal(back, contact.LastSeenUtc);

            // Quelque chose de retenu à un endroit que le joueur vérifie de nouveau, mais qui n'y est
            // plus : c'est oublié une fois qu'il a manqué un moment.
            var wreck = Base(new Vec3(200, 0, 0));
            wreck.Id = 7;
            Scan(memory, new Vec3(0, 0, 0), back, wreck);
            Scan(memory, new Vec3(90000, 0, 0), back.AddHours(1));
            var returned = back.AddHours(2);
            Scan(memory, new Vec3(0, 0, 0), returned);
            Assert.True(memory.TryGet(7, out contact));
            Scan(memory, new Vec3(0, 0, 0), returned + MapMemory.ForgetAfter + TimeSpan.FromSeconds(1));
            Assert.False(memory.TryGet(7, out contact));
        }

        [Fact]
        public void IsSeparatedPerServer()
        {
            var directory = Path.Combine(Path.GetTempPath(), "sir-holomap-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new MemoryStore(directory);
                var first = store.Load("steam://1.2.3.4:27016");
                Scan(first, new Vec3(0, 0, 0), T0, Base(new Vec3(100, 0, 0)));
                store.Save(first);

                // Un autre serveur, même sur la même machine : rien.
                var other = store.Load("steam://1.2.3.4:27017");
                Assert.Equal(0, other.Count);

                // Le même serveur après une reconnexion : la mémoire est de retour,
                // comme une mémoire.
                var again = store.Load("steam://1.2.3.4:27016");
                MemoryContact contact;
                Assert.True(again.TryGet(42, out contact));
                Assert.False(contact.IsLive);
                Assert.Equal("Outpost", contact.Name);
                Assert.Equal(T0, contact.LastSeenUtc);
                Assert.NotEqual(store.PathFor("steam://1.2.3.4:27016"), store.PathFor("steam://1.2.3.4:27017"));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void SurvivesDamagedLines()
        {
            var memory = MapMemory.Load("x", MapMemory.Header + "\nnot a line\n1\t0\t1\t5\t3\t1\t2\t3\t" + T0.Ticks + "\t\tShip\n");
            Assert.Equal(1, memory.Count);
        }
    }
}
