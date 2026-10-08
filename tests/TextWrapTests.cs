using System.Linq;
using Xunit;

namespace SirHolomap.Tests
{
    // The confirmation to join a server cuts its text at the width of its
    // box, whatever the language and the length of the server name.
    public class TextWrapTests
    {
        private static float Measure(string text)
        {
            return text.Length * 10f;
        }

        [Fact]
        public void EveryLineFitsTheBox()
        {
            var text = "Leave this server and join \"[FR] A very very long server name that goes on and on\"?\n\n"
                + "Address: steam://147.185.221.16:10664\nAVeryLongWordWithoutAnySpaceThatCannotFitOnOneLine";
            var lines = TextWrap.Wrap(text, Measure, 100);
            Assert.All(lines, line => Assert.True(Measure(line) <= 100, "\"" + line + "\" is too wide"));

            // Nothing is lost: every letter is still there, in order.
            Assert.Equal(text.Replace("\n", "").Replace(" ", ""), string.Concat(lines).Replace(" ", ""));

            // Line breaks of the text stay, the empty line too.
            Assert.Contains("", lines);
            Assert.StartsWith("Address:", lines.First(l => l.StartsWith("Address")));
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
