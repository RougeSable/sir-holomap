using System.Collections.Generic;

namespace SirHolomap
{
    // Les textes du greffon dans chacune des autres langues que le jeu offre, un
    // fichier par langue. Dans certains fichiers, les lettres accentuées sont
    // écrites en séquences d'échappement (une barre oblique inverse, la lettre u et
    // quatre chiffres hexadécimaux, qui donnent par exemple la lettre é) : le texte
    // obtenu dans le jeu est le même.
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
