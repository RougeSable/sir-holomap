using System.Linq;
using Xunit;

namespace SirHolomap.Tests
{
    // La confirmation pour rejoindre un serveur coupe son texte à la largeur de
    // sa boîte, quelle que soit la langue et la longueur du nom du serveur.
    public class TextWrapTests
    {
        private static float Measure(string text)
        {
            return text.Length * 10f;
        }

        [Fact]
        public void EveryLineFitsTheBox()
        {
            var text = "Quitter ce serveur et rejoindre \"[FR] Un très très long nom de serveur qui n'en finit plus\" ?\n\n"
                + "Adresse : steam://147.185.221.16:10664\nAVeryLongWordWithoutAnySpaceThatCannotFitOnOneLine";
            var lines = TextWrap.Wrap(text, Measure, 100);
            Assert.All(lines, line => Assert.True(Measure(line) <= 100, "\"" + line + "\" est trop large"));

            // Rien n'est perdu : chaque lettre est encore là, dans l'ordre.
            Assert.Equal(text.Replace("\n", "").Replace(" ", ""), string.Concat(lines).Replace(" ", ""));

            // Les retours à la ligne du texte restent, la ligne vide aussi.
            Assert.Contains("", lines);
            Assert.StartsWith("Adresse", lines.First(l => l.StartsWith("Adresse")));
        }

        [Fact]
        public void TextWithoutSpacesBreaksBetweenLetters()
        {
            var text = "离开此服务器并加入另一个服务器吗";
            var lines = TextWrap.Wrap(text, Measure, 50);
            Assert.True(lines.Count > 1);
            Assert.All(lines, line => Assert.True(Measure(line) <= 50));
            Assert.Equal(text, string.Concat(lines));
        }
    }
}
