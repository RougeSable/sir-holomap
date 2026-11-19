using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace SirHolomap.Tests
{
    // Le greffon parle la langue réglée dans le jeu, pour chaque langue que le
    // jeu offre ; un texte absent d'une langue se montre en français, la langue
    // de référence.
    public class LocalizationTests
    {
        private static readonly Regex Placeholder = new Regex(@"\{\d+\}");

        private static List<string> TextKeys()
        {
            return typeof(Texts).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => p.Name)
                .ToList();
        }

        private static string Placeholders(string text)
        {
            var found = Placeholder.Matches(text).Cast<Match>().Select(m => m.Value).ToList();
            found.Sort(StringComparer.Ordinal);
            return string.Join(",", found);
        }

        // Le français est la référence : il n'a pas de table à lui, et sert aussi
        // à la langue « English » de l'énumération, qui est celle de la référence.
        private static bool EstLaReference(GameLanguage language)
        {
            return language == GameLanguage.English || language == GameLanguage.French;
        }

        [Fact]
        public void EveryKeyExistsInEveryGameLanguage()
        {
            var keys = TextKeys();
            Assert.True(keys.Count > 100, keys.Count + " textes");
            var reference = Texts.EnglishTable();
            var catalog = new TextCatalog(reference, Translations.All());

            // Chaque texte que le greffon montre a sa référence en français.
            foreach (var key in keys)
            {
                Assert.True(reference.ContainsKey(key), "pas de texte de référence pour " + key);
                Assert.False(string.IsNullOrWhiteSpace(reference[key]), "texte de référence vide pour " + key);
            }
            Assert.Equal(keys.Count, reference.Count);

            // Chaque langue de la liste du jeu a son propre texte pour chacun
            // d'eux, avec les mêmes trous à remplir.
            var languages = Enum.GetValues(typeof(GameLanguage)).Cast<GameLanguage>().ToList();
            Assert.Equal(27, languages.Count);
            var tables = Translations.All();
            foreach (var language in languages)
            {
                if (EstLaReference(language))
                    continue;
                Assert.True(tables.ContainsKey(language), "pas de textes en " + language);
                foreach (var key in keys)
                {
                    Assert.True(catalog.Has(language, key), key + " manque en " + language);
                    Assert.Equal(Placeholders(reference[key]), Placeholders(catalog.Get(language, key)));
                }
                foreach (var key in tables[language].Keys)
                    Assert.True(reference.ContainsKey(key), language + " a un texte inconnu " + key);
            }

            // Les numéros de la liste propre au jeu (VRage.MyLanguagesEnum).
            Assert.Equal(GameLanguage.French, Localization.FromGame(6));
            Assert.Equal(GameLanguage.German, Localization.FromGame(3));
            Assert.Equal(GameLanguage.Japanese, Localization.FromGame(26));

            // Le joueur dont le jeu est en français lit la référence.
            Assert.Equal(reference["JoinPasswordLabel"], catalog.Get(GameLanguage.French, "JoinPasswordLabel"));

            // Traduits, pas copiés : quelques textes qui diffèrent dans chaque
            // langue à lettres latines.
            Assert.NotEqual(reference["StayButton"], catalog.Get(GameLanguage.German, "StayButton"));
            Assert.NotEqual(reference["JoinPasswordLabel"], catalog.Get(GameLanguage.German, "JoinPasswordLabel"));
        }

        // Le premier onglet de la vue Système garde son nom, « Système 2D ».
        [Fact]
        public void FlatSystemTabKeepsItsName()
        {
            var reference = Texts.EnglishTable();
            var catalog = new TextCatalog(reference, Translations.All());
            Assert.Equal("Système 2D", reference["TabBodies"]);
            Assert.Equal("Système 2D", catalog.Get(GameLanguage.French, "TabBodies"));
        }

        // Le second onglet de la vue Système n'est plus « Système 3D » : c'est
        // « Orbites », traduit dans chaque langue, sans « 3D » dedans, et jamais
        // du même nom que ses voisins.
        [Fact]
        public void OrbitsTabReplacesSystem3D()
        {
            var reference = Texts.EnglishTable();
            var catalog = new TextCatalog(reference, Translations.All());
            Assert.Equal("Orbites", reference["TabOrrery"]);
            Assert.Equal("Orbites", catalog.Get(GameLanguage.French, "TabOrrery"));
            Assert.Equal("Umlaufbahnen", catalog.Get(GameLanguage.German, "TabOrrery"));
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                var name = catalog.Get(language, "TabOrrery");
                Assert.True(catalog.Has(language, "TabOrrery") || EstLaReference(language), "pas d'onglet Orbites en " + language);
                Assert.DoesNotContain("3D", name);
                Assert.DoesNotContain("3B", name);
                Assert.DoesNotContain("2D", name);
                Assert.NotEqual(catalog.Get(language, "TabBodies"), name);
                Assert.NotEqual(catalog.Get(language, "TabGalaxy"), name);
                if (!EstLaReference(language))
                    Assert.NotEqual("Orbites", name);
            }
        }

        [Fact]
        public void MissingTranslationFallsBackToEnglish()
        {
            var reference = new Dictionary<string, string>
            {
                { "Close", "Fermer" },
                { "Seen", "vu il y a {0}" },
                { "Empty", "rien" },
            };
            var languages = new Dictionary<GameLanguage, Dictionary<string, string>>
            {
                { GameLanguage.German, new Dictionary<string, string> { { "Close", "Schließen" }, { "Empty", "" } } },
            };
            var catalog = new TextCatalog(reference, languages);

            Assert.Equal("Schließen", catalog.Get(GameLanguage.German, "Close"));
            // Absent en allemand, ou laissé vide : le texte de référence.
            Assert.Equal("vu il y a {0}", catalog.Get(GameLanguage.German, "Seen"));
            Assert.Equal("rien", catalog.Get(GameLanguage.German, "Empty"));
            Assert.False(catalog.Has(GameLanguage.German, "Seen"));
            // Une langue sans aucun texte : la référence partout.
            Assert.Equal("Fermer", catalog.Get(GameLanguage.Japanese, "Close"));
            Assert.Equal("Fermer", catalog.Get(GameLanguage.English, "Close"));
            // Une clé que personne ne connaît se montre telle quelle, jamais un plantage.
            Assert.Equal("Inconnue", catalog.Get(GameLanguage.German, "Inconnue"));

            // Un numéro de langue que le greffon ne connaît pas parle la référence.
            Assert.Equal(GameLanguage.English, Localization.FromGame(200));
            Assert.Equal(GameLanguage.English, Localization.FromGame(-1));
            Assert.Equal(GameLanguage.English, Localization.FromGame(1000));

            // Les textes du greffon, par défaut, sont ceux de la référence, en français.
            Assert.Equal(Texts.EnglishTable()["Title"], Localization.Catalog.Get(GameLanguage.English, "Title"));
            Assert.Equal("Système 2D", Localization.Catalog.Get(GameLanguage.English, "TabBodies"));
        }
    }
}
