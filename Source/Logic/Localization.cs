using System;
using System.Collections.Generic;

namespace SirHolomap
{
    // The languages the game offers, in the order and with the numbers of the
    // game's own list (VRage.MyLanguagesEnum): the plugin follows the
    // language set in the game's options.
    public enum GameLanguage : byte
    {
        English,
        Czech,
        Slovak,
        German,
        Russian,
        Spanish_Spain,
        French,
        Italian,
        Danish,
        Dutch,
        Icelandic,
        Polish,
        Finnish,
        Hungarian,
        Portuguese_Brazil,
        Estonian,
        Norwegian,
        Spanish_HispanicAmerica,
        Swedish,
        Catalan,
        Croatian,
        Romanian,
        Ukrainian,
        Turkish,
        Latvian,
        ChineseChina,
        Japanese,
    }

    // Every text of the plugin in every language of the game. English is the
    // reference: a text missing in a language, or a language the plugin does
    // not know, falls back to it.
    public sealed class TextCatalog
    {
        private readonly Dictionary<string, string> m_english;
        private readonly Dictionary<GameLanguage, Dictionary<string, string>> m_languages;

        public TextCatalog(Dictionary<string, string> english, Dictionary<GameLanguage, Dictionary<string, string>> languages)
        {
            if (english == null)
                throw new ArgumentNullException("english");
            m_english = english;
            m_languages = languages ?? new Dictionary<GameLanguage, Dictionary<string, string>>();
        }

        public IEnumerable<string> Keys
        {
            get { return m_english.Keys; }
        }

        public string English(string key)
        {
            string text;
            return m_english.TryGetValue(key, out text) ? text : key;
        }

        // The text in this language, or in English when it has none.
        public string Get(GameLanguage language, string key)
        {
            Dictionary<string, string> table;
            string text;
            if (language != GameLanguage.English && m_languages.TryGetValue(language, out table)
                && table.TryGetValue(key, out text) && !string.IsNullOrEmpty(text))
                return text;
            return English(key);
        }

        // True when this language has its own text for the key.
        public bool Has(GameLanguage language, string key)
        {
            if (language == GameLanguage.English)
                return m_english.ContainsKey(key);
            Dictionary<string, string> table;
            string text;
            return m_languages.TryGetValue(language, out table) && table.TryGetValue(key, out text) && !string.IsNullOrEmpty(text);
        }
    }

    // The language the plugin speaks: the game's, read every frame by the
    // plugin (English until the game tells).
    public static class Localization
    {
        private static TextCatalog s_catalog;

        public static GameLanguage Current = GameLanguage.English;

        public static TextCatalog Catalog
        {
            get { return s_catalog ?? (s_catalog = new TextCatalog(Texts.EnglishTable(), Translations.All())); }
        }

        // A number of the game's list; unknown numbers speak English.
        public static GameLanguage FromGame(int value)
        {
            if (value < 0 || value > 255 || !Enum.IsDefined(typeof(GameLanguage), (byte)value))
                return GameLanguage.English;
            return (GameLanguage)(byte)value;
        }

        public static string Get(string key)
        {
            return Catalog.Get(Current, key);
        }
    }
}
