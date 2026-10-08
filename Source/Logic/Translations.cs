using System.Collections.Generic;

namespace SirHolomap
{
    // The texts of the plugin in every other language the game offers, one
    // file per language. Letters with accents are written as escapes
    // (\u00e9 and the like) in some of them: the same text in the game.
    public static partial class Translations
    {
        public static Dictionary<GameLanguage, Dictionary<string, string>> All()
        {
            return new Dictionary<GameLanguage, Dictionary<string, string>>
            {
                { GameLanguage.Czech, Czech() },
                { GameLanguage.Slovak, Slovak() },
                { GameLanguage.German, German() },
                { GameLanguage.Russian, Russian() },
                { GameLanguage.Spanish_Spain, SpanishSpain() },
                { GameLanguage.French, French() },
                { GameLanguage.Italian, Italian() },
                { GameLanguage.Danish, Danish() },
                { GameLanguage.Dutch, Dutch() },
                { GameLanguage.Icelandic, Icelandic() },
                { GameLanguage.Polish, Polish() },
                { GameLanguage.Finnish, Finnish() },
                { GameLanguage.Hungarian, Hungarian() },
                { GameLanguage.Portuguese_Brazil, PortugueseBrazil() },
                { GameLanguage.Estonian, Estonian() },
                { GameLanguage.Norwegian, Norwegian() },
                { GameLanguage.Spanish_HispanicAmerica, SpanishHispanicAmerica() },
                { GameLanguage.Swedish, Swedish() },
                { GameLanguage.Catalan, Catalan() },
                { GameLanguage.Croatian, Croatian() },
                { GameLanguage.Romanian, Romanian() },
                { GameLanguage.Ukrainian, Ukrainian() },
                { GameLanguage.Turkish, Turkish() },
                { GameLanguage.Latvian, Latvian() },
                { GameLanguage.ChineseChina, ChineseChina() },
                { GameLanguage.Japanese, Japanese() },
            };
        }
    }
}
