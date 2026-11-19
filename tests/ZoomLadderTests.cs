using System;
using Xunit;

namespace SirHolomap.Tests
{
    // La molette glisse de la planète (A) au voisinage (B) puis au
    // système (C). Les passages ne dépendent que de l'échelle, avec un écart entre
    // la montée et la descente, et chaque cran multiplie la distance.
    public class ZoomLadderTests
    {
        [Fact]
        public void EachNotchMultipliesDistance()
        {
            // Chaque cran multiplie la distance par le même facteur, à toutes les
            // échelles.
            var near = ZoomSteps.Apply(100, -1) / 100;
            var far = ZoomSteps.Apply(1000000, -1) / 1000000;
            Assert.Equal(ZoomSteps.Factor, near, 9);
            Assert.Equal(ZoomSteps.Factor, far, 9);
            Assert.Equal(1.0 / ZoomSteps.Factor, ZoomSteps.Apply(500, 1) / 500, 9);
            Assert.Equal(1.0, ZoomSteps.NotchesFromWheel(ZoomSteps.WheelUnitsPerNotch), 9);

            // Dix crans vers l'extérieur puis dix vers l'intérieur : de retour au départ.
            Assert.Equal(250.0, ZoomSteps.Apply(ZoomSteps.Apply(250, -10), 10), 6);

            // Du point le plus bas au-dessus du sol d'une planète à la
            // vue Système : vingt crans au plus, pour une petite planète et pour
            // une grande.
            foreach (var radius in new[] { 19000.0, 60000.0, 120000.0 })
            {
                var notches = NotchesFromGroundToSystem(radius);
                Assert.True(notches <= 20, notches + " crans pour une planète de " + radius + " m");
            }

            // Dans l'espace, d'une grille qui remplit l'écran à la vue Système.
            Assert.True(ZoomSteps.NotchesBetween(50, MapScales.SystemAbove) <= 20);

            // Le lissage se déplace en espace logarithmique et atterrit sur la cible.
            var d = 100.0;
            for (var i = 0; i < 200; i++)
                d = ZoomSteps.Smooth(d, 10000, 1 / 60.0, 0.12);
            Assert.Equal(10000, d, 3);
        }

        // Tourne la molette vers l'extérieur un cran à la fois, comme le font les vues : A
        // zoome sur son altitude au-dessus du sol, B et C sur leur distance au
        // centre de ce qu'elles regardent.
        private static int NotchesFromGroundToSystem(double radius)
        {
            var ladder = new ZoomLadder(radius, ZoomRung.Planet);
            var altitude = MapScales.LowestAltitude;
            var distance = radius + altitude;
            var notches = 0;
            while (ladder.Rung != ZoomRung.System && notches < 100)
            {
                notches++;
                if (ladder.Rung == ZoomRung.Planet)
                {
                    altitude = Math.Min(ZoomSteps.Apply(altitude, -1), MapScales.HighestAltitude(radius));
                    distance = radius + altitude;
                }
                else
                {
                    distance = ZoomSteps.Apply(distance, -1);
                }
                var before = ladder.Rung;
                if (ladder.Update(distance) != before)
                    distance = ladder.Entry(ladder.Rung, distance);
            }
            return notches;
        }

        // Les deux passages, A vers B et B vers C, ne dépendent que de l'échelle et gardent
        // un écart entre la montée et la descente.
        [Fact]
        public void SwitchesDependOnScaleWithHysteresis()
        {
            PlanetToLocalSwitchHasHysteresis();
            LocalToSystemSwitchHasHysteresis();
        }

        private static void PlanetToLocalSwitchHasHysteresis()
        {
            const double radius = 60000;
            var ladder = new ZoomLadder(radius, ZoomRung.Planet);
            var change = ladder.PlanetSwitch;
            Assert.True(change.DownBelow < change.UpAbove);

            // Dézoomer du globe : le voisinage au-delà de la limite.
            Assert.Equal(ZoomRung.Planet, ladder.Update(change.UpAbove * 0.99));
            Assert.Equal(ZoomRung.Local, ladder.Update(change.UpAbove * 1.01));

            // S'arrêter juste à la limite, en oscillant autour : pas de clignotement.
            Assert.Equal(ZoomRung.Local, ladder.Update(change.UpAbove * 0.98));
            Assert.Equal(ZoomRung.Local, ladder.Update(change.UpAbove * 1.02));
            Assert.Equal(ZoomRung.Local, ladder.Update(change.DownBelow * 1.01));

            // Seulement bien en dessous de la limite le globe revient.
            Assert.Equal(ZoomRung.Planet, ladder.Update(change.DownBelow * 0.99));
            Assert.Equal(ZoomRung.Planet, ladder.Update(change.DownBelow * 1.05));

            // Une vue entrée depuis sa voisine commence dans l'écart : le
            // regard suivant à la même distance la garde, si bien que deux vues n'alternent jamais
            // pendant que la caméra glisse.
            var entered = ladder.Entry(ZoomRung.Planet, change.DownBelow * 0.999);
            Assert.True(entered < change.DownBelow);
            Assert.Equal(ZoomRung.Planet, ladder.Update(entered));
            ladder.Update(change.UpAbove * 1.2);
            Assert.Equal(ZoomRung.Local, ladder.Rung);
            var back = ladder.Entry(ZoomRung.Local, change.UpAbove * 1.0001);
            Assert.True(back > change.UpAbove);
            Assert.Equal(ZoomRung.Local, ladder.Update(back));

            // La molette tournée vers l'extérieur puis vers l'intérieur, cran après cran, autour de la
            // limite : exactement un changement dans chaque sens.
            var fresh = new ZoomLadder(radius, ZoomRung.Planet);
            var distance = radius * 1.5;
            var changes = 0;
            for (var i = 0; i < 12; i++)
                changes += Step(fresh, ref distance, -1);
            Assert.Equal(ZoomRung.System, fresh.Rung);
            Assert.Equal(2, changes);
            for (var i = 0; i < 12; i++)
                changes += Step(fresh, ref distance, 1);
            Assert.Equal(ZoomRung.Planet, fresh.Rung);
            Assert.Equal(4, changes);

            // Sans planète il n'y a pas de globe où descendre.
            var space = new ZoomLadder(0, ZoomRung.Planet);
            Assert.Equal(ZoomRung.Local, space.Rung);
            Assert.False(space.HasPlanet);
            Assert.Equal(ZoomRung.Local, space.Update(1));

            Assert.Throws<ArgumentException>(() => new ScaleSwitch(10, 10));
        }

        private static int Step(ZoomLadder ladder, ref double distance, double notches)
        {
            distance = ZoomSteps.Apply(distance, notches);
            var before = ladder.Rung;
            if (ladder.Update(distance) == before)
                return 0;
            distance = ladder.Entry(ladder.Rung, distance);
            return 1;
        }

        private static void LocalToSystemSwitchHasHysteresis()
        {
            var ladder = new ZoomLadder(0, ZoomRung.Local);
            var change = ladder.SystemSwitch;
            Assert.True(change.DownBelow < change.UpAbove);

            // Dézoomer au-delà de la limite : la vue Système.
            Assert.Equal(ZoomRung.Local, ladder.Update(change.UpAbove * 0.99));
            Assert.Equal(ZoomRung.System, ladder.Update(change.UpAbove * 1.01));

            // S'arrêter juste à la limite, en oscillant autour : pas de clignotement.
            Assert.Equal(ZoomRung.System, ladder.Update(change.UpAbove * 0.98));
            Assert.Equal(ZoomRung.System, ladder.Update(change.UpAbove * 1.02));
            Assert.Equal(ZoomRung.System, ladder.Update(change.UpAbove * 0.9));

            // Seulement bien en dessous de la limite le voisinage revient.
            Assert.Equal(ZoomRung.Local, ladder.Update(change.DownBelow * 0.99));
            Assert.Equal(ZoomRung.Local, ladder.Update(change.DownBelow * 1.05));

            // Entrer en C depuis B, ou en B depuis C, commence dans l'écart.
            Assert.True(ladder.Entry(ZoomRung.System, change.UpAbove) > change.UpAbove);
            Assert.True(ladder.Entry(ZoomRung.Local, change.UpAbove) < change.DownBelow);

            // Cela dépend de l'échelle, pas de ce que le serveur envoie : une planète
            // comme point de focalisation repousse la limite avec sa taille.
            var aroundPlanet = new ZoomLadder(120000, ZoomRung.Local);
            Assert.True(aroundPlanet.SystemSwitch.UpAbove > change.UpAbove);
            Assert.True(aroundPlanet.SystemSwitch.DownBelow > aroundPlanet.PlanetSwitch.UpAbove);
        }

        [Fact]
        public void LeavingPlanetCentreKeepsSystemThreshold()
        {
            // B centré sur une planète, juste au-delà du chemin de sortie de A : éloigner la
            // caméra du centre de la planète supprime la descente vers A mais
            // n'envoie jamais la vue en C toute seule.
            const double radius = 60000;
            var ladder = new ZoomLadder(radius, ZoomRung.Local);
            var distance = ladder.Entry(ZoomRung.Local, radius * (MapScales.PlanetLeaveRadii + 0.3));
            var up = ladder.SystemSwitch.UpAbove;
            Assert.Equal(ZoomRung.Local, ladder.Update(distance));

            ladder.LeavePlanet();
            Assert.False(ladder.HasPlanet);
            Assert.Equal(up, ladder.SystemSwitch.UpAbove);
            distance = ladder.Entry(ZoomRung.Local, distance);
            Assert.Equal(ZoomRung.Local, ladder.Update(distance));
            Assert.Equal(ZoomRung.Local, ladder.Update(distance * 1.01));

            // Zoomer ne retombe plus jamais en A ; dézoomer, C comme avant.
            Assert.Equal(ZoomRung.Local, ladder.Update(10));
            Assert.Equal(ZoomRung.System, ladder.Update(up * 1.01));
        }

        // Tout dézoomé, le système (C) cède la place à la galaxie (D)
        // en quelques crans ; zoomé, la galaxie cède la place au système en
        // quelques crans aussi, et le système revient dans l'écart pour que
        // les deux n'alternent jamais.
        [Fact]
        public void SystemAndGalaxyAreAFewNotchesApart()
        {
            foreach (var size in new[] { 0.0, 150000.0, 2.5e6, 6e7 })
            {
                var change = MapScales.SystemToGalaxy(size);
                var opened = Math.Max(size, MapScales.SmallestSystem) * 1.6;
                var notches = ZoomSteps.NotchesBetween(opened, change.UpAbove);
                Assert.InRange(notches, 2, 6);

                // De retour de la galaxie, dans l'écart : pas de sortie directe.
                var back = change.DownBelow * 0.9;
                Assert.True(back > opened, "le système revient plus près qu'il ne s'ouvre pour " + size);
                change.Reset(false);
                Assert.False(change.Update(back));
                Assert.True(change.Update(change.UpAbove * 1.01));
                Assert.True(change.IsUp);
            }

            // La galaxie repose sur son entier ; trois crans vers l'intérieur, le système.
            var zoom = new GalaxyZoom();
            Assert.Equal(MapScales.GalaxyHomeZoom, zoom.Target);
            var inward = 0;
            while (!zoom.BackToSystem && inward < 20)
            {
                zoom.Wheel(1);
                inward++;
            }
            Assert.InRange(inward, 2, 4);

            // Tout dézoomé d'abord, encore quelques crans.
            zoom.Home();
            for (var i = 0; i < 10; i++)
                zoom.Wheel(-1);
            Assert.Equal(MapScales.GalaxySmallestZoom, zoom.Target, 9);
            inward = 0;
            while (!zoom.BackToSystem && inward < 20)
            {
                zoom.Wheel(1);
                inward++;
            }
            Assert.InRange(inward, 2, 6);

            // Entrée de nouveau, la galaxie est de retour sur son entier : chaque serveur au
            // même endroit.
            zoom.Home();
            Assert.False(zoom.BackToSystem);
            Assert.Equal(MapScales.GalaxyHomeZoom, zoom.Target);
            Assert.True(MapScales.GalaxyArrivalZoom > MapScales.GalaxyHomeZoom);
        }
    }
}
