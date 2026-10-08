using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace SirHolomap.Tests
{
    // The plugin speaks the language set in the game, for every language the
    // game offers; a text missing in a language shows in English.
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

        [Fact]
        public void EveryTextExistsInEveryGameLanguage()
        {
            var keys = TextKeys();
            Assert.True(keys.Count > 100, keys.Count + " texts");
            var english = Texts.EnglishTable();
            var catalog = new TextCatalog(english, Translations.All());

            // Every text the plugin shows has its English reference.
            foreach (var key in keys)
            {
                Assert.True(english.ContainsKey(key), "no English text for " + key);
                Assert.False(string.IsNullOrWhiteSpace(english[key]), "empty English text for " + key);
            }
            Assert.Equal(keys.Count, english.Count);

            // Every language of the game's list has its own text for each of
            // them, with the same blanks to fill in.
            var languages = Enum.GetValues(typeof(GameLanguage)).Cast<GameLanguage>().ToList();
            Assert.Equal(27, languages.Count);
            var tables = Translations.All();
            foreach (var language in languages)
            {
                if (language == GameLanguage.English)
                    continue;
                Assert.True(tables.ContainsKey(language), "no texts in " + language);
                foreach (var key in keys)
                {
                    Assert.True(catalog.Has(language, key), key + " missing in " + language);
                    Assert.Equal(Placeholders(english[key]), Placeholders(catalog.Get(language, key)));
                }
                foreach (var key in tables[language].Keys)
                    Assert.True(english.ContainsKey(key), language + " has an unknown text " + key);
            }

            // The numbers of the game's own list (VRage.MyLanguagesEnum).
            Assert.Equal(GameLanguage.French, Localization.FromGame(6));
            Assert.Equal(GameLanguage.German, Localization.FromGame(3));
            Assert.Equal(GameLanguage.Japanese, Localization.FromGame(26));

            // Translated, not copied: a few texts that differ in every
            // language with Latin letters.
            Assert.NotEqual(english["JoinPasswordLabel"], catalog.Get(GameLanguage.French, "JoinPasswordLabel"));
            Assert.NotEqual(english["StayButton"], catalog.Get(GameLanguage.German, "StayButton"));
        }

        [Fact]
        public void MissingTranslationFallsBackToEnglish()
        {
            var english = new Dictionary<string, string>
            {
                { "Close", "Close" },
                { "Seen", "seen {0} ago" },
                { "Empty", "nothing" },
            };
            var languages = new Dictionary<GameLanguage, Dictionary<string, string>>
            {
                { GameLanguage.French, new Dictionary<string, string> { { "Close", "Fermer" }, { "Empty", "" } } },
            };
            var catalog = new TextCatalog(english, languages);

            Assert.Equal("Fermer", catalog.Get(GameLanguage.French, "Close"));
            // Missing in French, or left empty: the English text.
            Assert.Equal("seen {0} ago", catalog.Get(GameLanguage.French, "Seen"));
            Assert.Equal("nothing", catalog.Get(GameLanguage.French, "Empty"));
            Assert.False(catalog.Has(GameLanguage.French, "Seen"));
            // A language without any text: English everywhere.
            Assert.Equal("Close", catalog.Get(GameLanguage.Japanese, "Close"));
            Assert.Equal("Close", catalog.Get(GameLanguage.English, "Close"));
            // A key nobody knows shows as itself, never a crash.
            Assert.Equal("Unknown", catalog.Get(GameLanguage.French, "Unknown"));

            // A language number the plugin does not know speaks English.
            Assert.Equal(GameLanguage.English, Localization.FromGame(200));
            Assert.Equal(GameLanguage.English, Localization.FromGame(-1));
            Assert.Equal(GameLanguage.English, Localization.FromGame(1000));

            // The plugin's own texts, by default, are the English ones.
            Assert.Equal(Texts.EnglishTable()["Title"], Localization.Catalog.Get(GameLanguage.English, "Title"));
        }
    }
}
