using System;
using System.Linq;
using Xunit;

namespace SirHolomap.Tests
{
    // Les antennes, les balises et les combinaisons qui diffusent : elles se montrent
    // sur la carte tant que la radio du joueur les entend, restent à leur dernière
    // position connue quand il sort de leur portée, et disparaissent une fois coupées.
    public class EmitterTests
    {
        private const double SyncRadius = 3000;
        private static readonly DateTime T0 = new DateTime(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

        private static Sighting Emitter(long id, ContactKind kind, Vec3 position, double range)
        {
            return new Sighting
            {
                Id = id,
                Name = "Relais nord",
                Kind = kind,
                Relation = ContactRelation.Neutral,
                Radius = 1,
                Range = range,
                Position = position,
                Body = "EarthLike",
            };
        }

        private static void Scan(MapMemory memory, Vec3 observer, DateTime now, bool emittersRead, Func<long, bool> switchedOff, params Sighting[] heard)
        {
            memory.BeginScan();
            foreach (var s in heard)
                memory.See(s, now);
            if (emittersRead)
                memory.EmittersRead();
            memory.EndScan(observer, SyncRadius, now, switchedOff);
        }

        [Fact]
        public void HeardEmitterIsLiveWithItsKindNameAndRange()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            Scan(memory, new Vec3(0, 0, 0), T0, true, null,
                Emitter(1, ContactKind.Antenna, new Vec3(400, 0, 0), 5000),
                Emitter(2, ContactKind.Beacon, new Vec3(0, 300, 0), 100),
                Emitter(3, ContactKind.Suit, new Vec3(0, 0, 150), 200));

            foreach (var id in new long[] { 1, 2, 3 })
            {
                MemoryContact contact;
                Assert.True(memory.TryGet(id, out contact));
                Assert.True(contact.IsLive);
                Assert.Equal("Relais nord", contact.Name);
                Assert.True(ContactKinds.IsEmitter(contact.Kind));
            }
            MemoryContact antenna;
            memory.TryGet(1, out antenna);
            Assert.Equal(ContactKind.Antenna, antenna.Kind);
            Assert.Equal(5000, antenna.Range);
        }

        [Fact]
        public void EmitterOutOfRangeStaysWhereItWasHeardAsAMemory()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            Scan(memory, new Vec3(0, 0, 0), T0, true, null, Emitter(2, ContactKind.Beacon, new Vec3(50, 0, 0), 100));

            // Le joueur s'éloigne bien au-delà de la portée de la balise, qui est encore là :
            // la radio ne l'entend plus, la carte la garde.
            for (var minute = 1; minute <= 30; minute++)
                Scan(memory, new Vec3(400, 0, 0), T0.AddMinutes(minute), true, id => false);

            MemoryContact contact;
            Assert.True(memory.TryGet(2, out contact));
            Assert.False(contact.IsLive);
            Assert.Equal(50, contact.Position.X);
            Assert.Equal(T0, contact.LastSeenUtc);
        }

        [Fact]
        public void SwitchedOffEmitterDisappearsAtOnce()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            Scan(memory, new Vec3(0, 0, 0), T0, true, null, Emitter(1, ContactKind.Antenna, new Vec3(400, 0, 0), 5000));

            // Le jeu connaît encore l'antenne, et elle ne diffuse plus.
            Scan(memory, new Vec3(0, 0, 0), T0.AddSeconds(1), true, id => id == 1);

            MemoryContact contact;
            Assert.False(memory.TryGet(1, out contact));
        }

        [Fact]
        public void EmitterMissingInsideItsRangeIsForgottenAfterAWhile()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            Scan(memory, new Vec3(0, 0, 0), T0, true, null, Emitter(1, ContactKind.Beacon, new Vec3(40, 0, 0), 1000));

            // Le jeu ne connaît plus la balise (hors de la portée de synchronisation) mais le joueur
            // est dans sa portée de diffusion : si elle diffusait, la radio l'entendrait.
            Scan(memory, new Vec3(0, 0, 0), T0.AddSeconds(1), true, id => false);
            MemoryContact contact;
            Assert.True(memory.TryGet(1, out contact));
            Assert.False(contact.IsLive);

            Scan(memory, new Vec3(0, 0, 0), T0.AddSeconds(1) + MapMemory.ForgetAfter + TimeSpan.FromSeconds(1), true, id => false);
            Assert.False(memory.TryGet(1, out contact));
        }

        [Fact]
        public void EmitterIsLeftAloneWhenTheRadioListCouldNotBeRead()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            Scan(memory, new Vec3(0, 0, 0), T0, true, null, Emitter(1, ContactKind.Antenna, new Vec3(40, 0, 0), 5000));

            for (var minute = 1; minute <= 5; minute++)
                Scan(memory, new Vec3(0, 0, 0), T0.AddMinutes(minute), false, id => true);

            MemoryContact contact;
            Assert.True(memory.TryGet(1, out contact));
            Assert.False(contact.IsLive);
        }

        [Fact]
        public void EmitterComesBackLiveWhenHeardAgain()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            Scan(memory, new Vec3(0, 0, 0), T0, true, null, Emitter(1, ContactKind.Antenna, new Vec3(40, 0, 0), 500));
            Scan(memory, new Vec3(90000, 0, 0), T0.AddHours(1), true, id => false);

            var back = T0.AddHours(2);
            Scan(memory, new Vec3(0, 0, 0), back, true, null, Emitter(1, ContactKind.Antenna, new Vec3(40, 0, 0), 500));

            MemoryContact contact;
            Assert.True(memory.TryGet(1, out contact));
            Assert.True(contact.IsLive);
            Assert.Equal(back, contact.LastSeenUtc);
        }

        [Fact]
        public void SuitFarAwayStaysAndSuitNearbyThatIsMissingGoes()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            Scan(memory, new Vec3(0, 0, 0), T0, true, null, Emitter(3, ContactKind.Suit, new Vec3(50000, 0, 0), 200));

            // Loin, au-delà de la portée de synchronisation : un souvenir.
            for (var minute = 1; minute <= 10; minute++)
                Scan(memory, new Vec3(0, 0, 0), T0.AddMinutes(minute), true, id => false);
            MemoryContact contact;
            Assert.True(memory.TryGet(3, out contact));
            Assert.False(contact.IsLive);

            // Le joueur arrive à sa portée de diffusion : plus rien ne s'y entend.
            var near = T0.AddMinutes(20);
            Scan(memory, new Vec3(49950, 0, 0), near, true, id => false);
            Scan(memory, new Vec3(49950, 0, 0), near + MapMemory.ForgetAfter + TimeSpan.FromSeconds(1), true, id => false);
            Assert.False(memory.TryGet(3, out contact));
        }

        [Fact]
        public void ThePlainGridsKeepTheirOldBehaviourWhenEmittersAreRead()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            var station = new Sighting { Id = 9, Name = "Base", Kind = ContactKind.Station, Blocks = 100, Radius = 20, Position = new Vec3(100, 0, 0) };
            Scan(memory, new Vec3(0, 0, 0), T0, true, null, station);
            for (var minute = 1; minute <= 5; minute++)
                Scan(memory, new Vec3(80000, 0, 0), T0.AddMinutes(minute), true, id => true);

            MemoryContact contact;
            Assert.True(memory.TryGet(9, out contact));
            Assert.False(contact.IsLive);
        }

        [Fact]
        public void RangeSurvivesTheDiskAndOldFilesStillLoad()
        {
            var memory = new MapMemory("steam://1.2.3.4:27016");
            Scan(memory, new Vec3(0, 0, 0), T0, true, null,
                Emitter(1, ContactKind.Antenna, new Vec3(400, 0, 0), 5000.5),
                Emitter(3, ContactKind.Suit, new Vec3(0, 0, 150), 200));

            var again = MapMemory.Load("steam://1.2.3.4:27016", memory.Save());
            Assert.Equal(2, again.Count);
            MemoryContact contact;
            Assert.True(again.TryGet(1, out contact));
            Assert.Equal(ContactKind.Antenna, contact.Kind);
            Assert.Equal(5000.5, contact.Range);
            Assert.False(contact.IsLive);
            Assert.True(again.TryGet(3, out contact));
            Assert.Equal(ContactKind.Suit, contact.Kind);

            // Une ligne d'avant les émetteurs, sans colonne de portée.
            var old = MapMemory.Header + "\n7\t1\t1\t350\t30\t100\t0\t0\t" + T0.Ticks + "\tEarthLike\tBase\n";
            var loaded = MapMemory.Load("steam://1.2.3.4:27016", old);
            Assert.True(loaded.TryGet(7, out contact));
            Assert.Equal(0, contact.Range);
            Assert.Equal("Base", contact.Name);
        }

        [Fact]
        public void EachKindOfEmitterHasItsOwnShape()
        {
            var cross = Images.ShapeImage(Images.Shape.Cross);
            var spark = Images.ShapeImage(Images.Shape.Spark);
            var signal = Images.ShapeImage(Images.Shape.Signal);
            Assert.False(cross.SequenceEqual(spark));
            Assert.False(cross.SequenceEqual(signal));
            Assert.False(spark.SequenceEqual(signal));
            foreach (var image in new[] { cross, spark, signal })
            {
                Assert.Equal(Images.ShapeSize * Images.ShapeSize * 4, image.Length);
                // Chaque forme a un cœur plein, et ses coins restent vides.
                var centre = (Images.ShapeSize / 2 * Images.ShapeSize + Images.ShapeSize / 2) * 4 + 3;
                Assert.True(image[centre] > 200 || image[centre - 4] > 200);
                Assert.Equal(0, image[3]);
            }
            Assert.NotEqual(Images.Shape.Cross, Images.Shape.Spark);
        }

        [Fact]
        public void EmittersAreShownByDefault()
        {
            Assert.True(new MapSettings().ShowEmitters);
            Assert.True(ContactKinds.IsBlockEmitter(ContactKind.Antenna));
            Assert.True(ContactKinds.IsBlockEmitter(ContactKind.Beacon));
            Assert.False(ContactKinds.IsBlockEmitter(ContactKind.Suit));
            Assert.True(ContactKinds.IsEmitter(ContactKind.Suit));
            Assert.False(ContactKinds.IsEmitter(ContactKind.Character));
        }
    }
}
