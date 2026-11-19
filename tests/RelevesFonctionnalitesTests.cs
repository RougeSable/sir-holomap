using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace SirHolomap.Tests
{
    // Les points de fonctionnalités qui partent vers Sir Studio Torch.
    public class RelevesFonctionnalitesTests
    {
        private const ulong Joueur = 76561198000000001UL;

        private static readonly DateTime Midi = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void CocherOuDecocherUneCaseNEnvoieAucunPoint()
        {
            var releves = new RelevesFonctionnalites(() => Midi);
            var envoyes = new List<byte[]>();

            // Chaque case du greffon, cochée puis décochée : les réglages ne
            // connaissent pas le suivi, et rien n'attend ni ne part.
            var reglages = new MapSettings();
            foreach (var champ in typeof(MapSettings).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(c => c.FieldType == typeof(bool)))
            {
                champ.SetValue(reglages, true);
                champ.SetValue(reglages, false);
                champ.SetValue(reglages, true);
            }
            reglages.Normalized();
            Assert.Equal(0, releves.EnAttente);
            Assert.Equal(0, releves.Vider(Joueur, octets => { envoyes.Add(octets); return true; }));
            Assert.Empty(envoyes);

            // Le code des cases n'appelle jamais le suivi : ni le panneau de la
            // carte, qui les dessine, ni les réglages qui les gardent.
            foreach (var fichier in new[] { "Source/Game/Panel.cs", "Source/Logic/MapSettings.cs" })
            {
                var source = Depot.Lire(fichier);
                Assert.DoesNotContain("PointsDeFonctionnalites", source);
                Assert.DoesNotContain("RelevesFonctionnalites", source);
                Assert.DoesNotContain("Fonctionnalites.", source);
            }
        }

        [Fact]
        public void LEnvoiDuPointEstMisEnFileHorsDuFilDuJeu()
        {
            var releves = new RelevesFonctionnalites(() => Midi);
            var appels = 0;
            Func<byte[], bool> envoyer = octets => { appels++; return true; };

            // Au geste du joueur, le point est seulement mis en file : rien ne part.
            releves.Compter(Fonctionnalites.Carte);
            Assert.Equal(1, releves.EnAttente);
            Assert.Equal(0, appels);

            // Il part ensuite, par le tour d'envoi, une seule fois.
            Assert.Equal(1, releves.Vider(Joueur, envoyer));
            Assert.Equal(1, appels);
            Assert.Equal(0, releves.EnAttente);
            Assert.Equal(0, releves.Vider(Joueur, envoyer));
            Assert.Equal(1, appels);

            // Une faute d'envoi est avalée : elle ne remonte pas jusqu'au jeu, et le
            // point reste en file pour le tour suivant.
            releves.Compter(Fonctionnalites.Galaxie);
            var fautif = releves.Vider(Joueur, octets => { throw new InvalidOperationException("liaison coupée"); });
            Assert.Equal(0, fautif);
            Assert.Equal(1, releves.EnAttente);
            Assert.Equal(1, releves.Vider(Joueur, envoyer));
            Assert.Equal(2, appels);
        }

        [Fact]
        public void UnSeulPointParCleEtParJour()
        {
            var maintenant = Midi;
            var releves = new RelevesFonctionnalites(() => maintenant);
            var messages = new List<string>();
            Func<byte[], bool> envoyer = octets => { messages.Add(Encoding.UTF8.GetString(octets)); return true; };

            releves.Compter(Fonctionnalites.Systeme);
            releves.Compter(Fonctionnalites.Systeme);
            releves.Compter(Fonctionnalites.SuiviGrille);
            releves.Vider(Joueur, envoyer);
            releves.Compter(Fonctionnalites.Systeme);
            Assert.Equal(0, releves.EnAttente);
            Assert.Equal(new[] { "76561198000000001;holomap.systeme", "76561198000000001;holomap.suivi_grille" }, messages);

            // Le lendemain, le geste compte de nouveau.
            maintenant = Midi.AddDays(1);
            releves.Compter(Fonctionnalites.Systeme);
            Assert.Equal(1, releves.EnAttente);
        }

        [Fact]
        public void LeJourSuitL_HeureDeParis()
        {
            // 23 h 30 UTC le 19 novembre : il est déjà 0 h 30 le 20 à Paris.
            var maintenant = new DateTime(2026, 11, 19, 22, 30, 0, DateTimeKind.Utc);
            var releves = new RelevesFonctionnalites(() => maintenant);
            var envoyer = new Func<byte[], bool>(octets => true);

            releves.Compter(Fonctionnalites.Carte);
            releves.Vider(Joueur, envoyer);
            Assert.Equal(0, releves.EnAttente);

            // Minuit passé à Paris, mais encore le même jour en UTC : le point compte.
            maintenant = new DateTime(2026, 11, 19, 23, 30, 0, DateTimeKind.Utc);
            releves.Compter(Fonctionnalites.Carte);
            Assert.Equal(1, releves.EnAttente);

            Assert.Equal("2026-11-20", RelevesFonctionnalites.JourDeParis(maintenant));
            Assert.Equal("2026-07-01", RelevesFonctionnalites.JourDeParis(new DateTime(2026, 6, 30, 22, 30, 0, DateTimeKind.Utc)));
        }

        [Fact]
        public void UneCleInconnueOuUnJoueurInconnuNEnvoieRien()
        {
            var releves = new RelevesFonctionnalites(() => Midi);
            var appels = 0;
            Func<byte[], bool> envoyer = octets => { appels++; return true; };

            releves.Compter("holomap.inconnue");
            releves.Compter(null);
            releves.Compter("");
            Assert.Equal(0, releves.EnAttente);

            releves.Compter(Fonctionnalites.Carte);
            Assert.Equal(0, releves.Vider(0, envoyer));
            Assert.Equal(0, releves.Vider(Joueur, null));
            Assert.Equal(0, appels);
            Assert.Equal(1, releves.EnAttente);

            releves.Oublier();
            Assert.Equal(0, releves.EnAttente);
        }

        [Fact]
        public void LePointSEnvoieSurLeCanalCommunDuSuivi()
        {
            Assert.Equal(46481, RelevesFonctionnalites.CanalDesPoints);
            Assert.True(RelevesFonctionnalites.CanalDesPoints <= ushort.MaxValue);
            Assert.Equal("76561198000000001;holomap.carte", Encoding.UTF8.GetString(RelevesFonctionnalites.Message(Joueur, Fonctionnalites.Carte)));
        }

        [Fact]
        public void LesQuatreGestesComptentUnPoint()
        {
            // Ce que les tests ne peuvent pas jouer (le jeu) se vérifie dans le code :
            // chaque geste appelle le suivi.
            var ecran = Depot.Lire("Source/Game/MapScreen.cs");
            Assert.Contains("PointsDeFonctionnalites.Point(Fonctionnalites.Carte", ecran);
            Assert.Contains("PointsDeFonctionnalites.Point(Fonctionnalites.Systeme", ecran);
            Assert.Contains("PointsDeFonctionnalites.Point(Fonctionnalites.Galaxie", ecran);
            Assert.Contains("PointsDeFonctionnalites.Point(Fonctionnalites.SuiviGrille", Depot.Lire("Source/Game/LocalView.cs"));
        }
    }
}
