using System;
using System.Collections.Generic;
using System.Text;

namespace SirHolomap
{
    // Cuts a text into lines no wider than a width, as measured by the
    // caller (the game's font). Lines break between words; a word wider than
    // the whole width (a long server name, an address, a sentence in a
    // language written without spaces) breaks between its letters. Line
    // breaks of the text are kept, empty lines included.
    public static class TextWrap
    {
        public static List<string> Wrap(string text, Func<string, float> measure, float width)
        {
            if (measure == null)
                throw new ArgumentNullException("measure");
            var lines = new List<string>();
            var paragraphs = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Trim().Length == 0)
                {
                    lines.Add("");
                    continue;
                }
                var line = new StringBuilder();
                foreach (var word in paragraph.Split(' '))
                {
                    if (word.Length == 0)
                        continue;
                    var attempt = line.Length == 0 ? word : line + " " + word;
                    if (measure(attempt) <= width)
                    {
                        line.Clear().Append(attempt);
                        continue;
                    }
                    if (line.Length > 0)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }
                    if (measure(word) <= width)
                    {
                        line.Append(word);
                        continue;
                    }
                    // Too wide on its own: letter by letter.
                    foreach (var letter in word)
                    {
                        if (line.Length > 0 && measure(line.ToString() + letter) > width)
                        {
                            lines.Add(line.ToString());
                            line.Clear();
                        }
                        line.Append(letter);
                    }
                }
                if (line.Length > 0)
                    lines.Add(line.ToString());
            }
            return lines;
        }
    }
}
