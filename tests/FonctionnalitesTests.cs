using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace SirHolomap.Tests
{
    // Les fonctionnalités que le produit déclare à Sir Studio Torch.
    public class FonctionnalitesTests
    {
        private static List<KeyValuePair<string, string>> Declarees()
        {
            using (var document = JsonDocument.Parse(Depot.Lire("fonctionnalites.json").TrimStart('﻿')))
            {
                return document.RootElement.GetProperty("fonctionnalites").EnumerateArray()
                    .Select(f => new KeyValuePair<string, string>(f.GetProperty("cle").GetString(), f.GetProperty("libelle").GetString()))
                    .ToList();
            }
        }

        [Fact]
        public void ChaqueFonctionnaliteDeclareeAUneCleEtUnLibelleFrancais()
        {
            var declarees = Declarees();

            // Ouvrir la carte, vue Système, vue Galaxie, suivi d'une grille.
            Assert.Equal(
                new[] { "holomap.carte", "holomap.systeme", "holomap.galaxie", "holomap.suivi_grille" },
                declarees.Select(d => d.Key).ToArray());
            Assert.Equal(
                new[] { "Ouvrir la carte", "Vue Système", "Vue Galaxie", "Suivi d'une grille" },
                declarees.Select(d => d.Value).ToArray());

            // De 1 à 5 fonctionnalités par produit, une clé de la forme produit.fonctionnalité.
            Assert.InRange(declarees.Count, 1, 5);
            var forme = new Regex(@"^[a-z0-9][a-z0-9_-]{0,63}[.][a-z0-9][a-z0-9_-]{0,63}\z");
            foreach (var declaree in declarees)
            {
                Assert.Matches(forme, declaree.Key);
                Assert.False(string.IsNullOrWhiteSpace(declaree.Value));
            }
            Assert.Equal(declarees.Count, declarees.Select(d => d.Key).Distinct().Count());
        }

        [Fact]
        public void LeCodeDeclareLesMemesCleEtLibellesQueLeFichier()
        {
            var declarees = Declarees();

            Assert.Equal(declarees.Select(d => d.Key).ToArray(), Fonctionnalites.Cles);
            Assert.Equal(declarees.Select(d => d.Value).ToArray(), Fonctionnalites.Libelles);
            foreach (var cle in Fonctionnalites.Cles)
                Assert.True(Fonctionnalites.EstDeclaree(cle));
            Assert.False(Fonctionnalites.EstDeclaree("holomap.inconnue"));
        }
    }
}
