using System;
using System.Collections.Generic;

namespace SirHolomap
{
    // Les langues que le jeu offre, dans l'ordre et avec les numéros de la
    // liste du jeu (VRage.MyLanguagesEnum) : le greffon suit la
    // langue réglée dans les options du jeu.
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

    // Chaque texte du greffon dans chaque langue du jeu. Le français est la
    // référence (la langue « English » de l'énumération est celle de la référence) : un texte
    // absent d'une langue, ou une langue que le greffon ne connaît pas, retombe dessus.
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

        // Le texte dans cette langue, ou celui de la référence quand elle n'en a pas.
        public string Get(GameLanguage language, string key)
        {
            Dictionary<string, string> table;
            string text;
            if (language != GameLanguage.English && m_languages.TryGetValue(language, out table)
                && table.TryGetValue(key, out text) && !string.IsNullOrEmpty(text))
                return text;
            return English(key);
        }

        // Vrai quand cette langue a son propre texte pour la clé.
        public bool Has(GameLanguage language, string key)
        {
            if (language == GameLanguage.English)
                return m_english.ContainsKey(key);
            Dictionary<string, string> table;
            string text;
            return m_languages.TryGetValue(language, out table) && table.TryGetValue(key, out text) && !string.IsNullOrEmpty(text);
        }
    }

    // La langue que parle le greffon : celle du jeu, relue à chaque image par le
    // greffon (la référence tant que le jeu ne la dit pas).
    public static class Localization
    {
        private static TextCatalog s_catalog;

        public static GameLanguage Current = GameLanguage.English;

        public static TextCatalog Catalog
        {
            get { return s_catalog ?? (s_catalog = new TextCatalog(Texts.EnglishTable(), Translations.All())); }
        }

        // Un numéro de la liste du jeu ; les numéros inconnus parlent la langue de référence.
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
