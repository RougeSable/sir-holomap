using System;
using VRage.Utils;
using VRageMath;

namespace SirHolomap
{
    // The menu on the right of the map, the same in every view: a title, the
    // info of what is selected, what to show, and the list of what is in
    // view. Drawn every frame; what can be clicked is registered with the map.
    internal sealed class Panel
    {
        private readonly MapScreen m_map;
        private float m_x;
        private float m_w;
        private float m_y;
        private float m_bottom;
        private int m_column;
        private float m_columnY;

        private const float FooterScale = 0.52f;
        private const float FooterLine = 18;
        private readonly System.Collections.Generic.List<string> m_footerLines = new System.Collections.Generic.List<string>();
        private float m_footerTop;

        public Panel(MapScreen map)
        {
            m_map = map;
        }

        private static float S
        {
            get { return Gfx.Scale; }
        }

        public float Y
        {
            get { return m_y; }
        }

        public void Begin(float x, float y, float w, float bottom)
        {
            m_x = x;
            m_w = w;
            m_y = y;
            m_bottom = bottom;
            m_column = 0;
            m_footerLines.Clear();
            m_footerTop = bottom - 50 * S;
            Gfx.Rect(x, y, w, bottom - y, Style.Background);
            Gfx.Rect(x, y, w, 2 * S, Style.Accent);
            Gfx.Frame(x, y, w, bottom - y, 1, Gfx.Alpha(Style.AccentDim, 0.7f));
            m_y += 12 * S;
        }

        private void CloseColumns()
        {
            if (m_column == 1)
            {
                m_y = m_columnY + 26 * S;
                m_column = 0;
            }
        }

        public void Title(string title, string subtitle)
        {
            CloseColumns();
            Gfx.Text(Gfx.Fit(title, 0.95f, m_w - 28 * S), m_x + 14 * S, m_y, 0.95f, Style.Text);
            m_y += 30 * S;
            if (!string.IsNullOrEmpty(subtitle))
            {
                Gfx.Text(Gfx.Fit(subtitle, 0.62f, m_w - 28 * S), m_x + 14 * S, m_y, 0.62f, Style.Dim);
                m_y += 22 * S;
            }
            m_y += 4 * S;
        }

        public void Tabs(string[] names, string[] helps, int current, Action<int> pick)
        {
            CloseColumns();
            var gap = 6 * S;
            var width = (m_w - 28 * S - gap * (names.Length - 1)) / names.Length;
            var height = 30 * S;
            for (var i = 0; i < names.Length; i++)
            {
                var x = m_x + 14 * S + i * (width + gap);
                var rect = new RectangleF(x, m_y, width, height);
                var index = i;
                var hover = m_map.AddHit(rect, () => pick(index), null, helps[i]);
                var on = i == current;
                Gfx.Rect(x, m_y, width, height, on ? Gfx.Alpha(Style.Accent, 0.35f) : (hover ? Style.RowHover : Style.Row));
                Gfx.Frame(x, m_y, width, height, 1, on ? Style.Accent : Gfx.Alpha(Style.AccentDim, 0.8f));
                Gfx.Text(names[i], x + width / 2, m_y + height / 2, 0.66f, on ? Color.White : Style.Text,
                    MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
            }
            m_y += height + 10 * S;
        }

        public void Section(string name)
        {
            CloseColumns();
            m_y += 6 * S;
            Gfx.Text(name, m_x + 14 * S, m_y, 0.6f, Style.Accent);
            Gfx.Rect(m_x + 14 * S, m_y + 20 * S, m_w - 28 * S, 1, Gfx.Alpha(Style.AccentDim, 0.8f));
            m_y += 26 * S;
        }

        public void Heading(string name, string kind)
        {
            CloseColumns();
            Gfx.Text(Gfx.Fit(name, 0.8f, m_w - 28 * S), m_x + 14 * S, m_y, 0.8f, Color.White);
            m_y += 24 * S;
            if (!string.IsNullOrEmpty(kind))
            {
                Gfx.Text(kind, m_x + 14 * S, m_y, 0.58f, Style.Dim);
                m_y += 20 * S;
            }
        }

        public void Line(string key, string value)
        {
            Line(key, value, Style.Text, false);
        }

        public void Line(string key, string value, Color color)
        {
            Line(key, value, color, false);
        }

        // Key on the left, value on the right; long values wrap under.
        public void Line(string key, string value, Color color, bool wrap)
        {
            CloseColumns();
            if (m_y > m_footerTop - 10 * S)
                return;
            const float scale = 0.6f;
            var keyWidth = 120 * S;
            Gfx.Text(key, m_x + 14 * S, m_y, scale, Style.Dim);
            var room = m_w - 28 * S - keyWidth;
            value = value ?? "";
            if (!wrap || Gfx.Measure(value, scale).X <= room)
            {
                Gfx.Text(Gfx.Fit(value, scale, room), m_x + m_w - 14 * S, m_y, scale, color,
                    MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP);
                m_y += 21 * S;
                return;
            }

            // Wrapped, aligned right, at most three lines.
            var words = value.Split(' ');
            var line = "";
            var lines = 0;
            foreach (var word in words)
            {
                var attempt = line.Length == 0 ? word : line + " " + word;
                if (Gfx.Measure(attempt, scale).X > room && line.Length > 0)
                {
                    Gfx.Text(line, m_x + m_w - 14 * S, m_y, scale, color, MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP);
                    m_y += 19 * S;
                    line = word;
                    if (++lines >= 2)
                    {
                        line = Gfx.Fit(value.Substring(Math.Max(0, value.IndexOf(word, StringComparison.Ordinal))), scale, room);
                        break;
                    }
                }
                else
                {
                    line = attempt;
                }
            }
            Gfx.Text(line, m_x + m_w - 14 * S, m_y, scale, color, MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP);
            m_y += 21 * S;
        }

        public void Note(string text, Color color)
        {
            CloseColumns();
            Gfx.Text(Gfx.Fit(text, 0.58f, m_w - 28 * S), m_x + 14 * S, m_y, 0.58f, color);
            m_y += 21 * S;
        }

        // Two boxes per line.
        public void Checkbox(string label, string help, bool value, Action<bool> set, int column)
        {
            if (column == 0)
                CloseColumns();
            var y = column == 1 && m_column == 1 ? m_columnY : m_y;
            var x = m_x + 14 * S + (column == 1 ? (m_w - 28 * S) / 2 : 0);
            var box = 16 * S;
            var rect = new RectangleF(x, y, (m_w - 28 * S) / 2 - 4 * S, 22 * S);
            var hover = m_map.AddHit(rect, () =>
            {
                set(!value);
                m_map.SettingsChanged();
            }, null, help ?? label);
            Gfx.Frame(x, y + 3 * S, box, box, 1, hover ? Style.Accent : Style.Dim);
            if (value)
                Gfx.Rect(x + 3 * S, y + 6 * S, box - 6 * S, box - 6 * S, Style.Accent);
            Gfx.Text(label, x + box + 8 * S, y + 2 * S, 0.6f, hover ? Color.White : Style.Text);

            if (column == 0)
            {
                m_column = 1;
                m_columnY = m_y;
            }
            else
            {
                m_column = 0;
                m_y = y + 26 * S;
            }
        }

        public void NumberField(string id, string label, string help, int value, Action<int> set)
        {
            CloseColumns();
            Gfx.Text(label, m_x + 14 * S, m_y + 3 * S, 0.6f, Style.Text);
            var w = 110 * S;
            var x = m_x + m_w - 14 * S - w;
            Field(id, x, m_y, w, help, value.ToString(), text =>
            {
                int parsed;
                if (int.TryParse(text.Trim(), out parsed))
                    set(MapSettings.Clamp(parsed));
            }, true);
            m_y += 30 * S;
        }

        public void TextField(string id, string placeholder, string help, string value, Action<string> set)
        {
            CloseColumns();
            Field(id, m_x + 14 * S, m_y, m_w - 28 * S, help, value ?? "", set, false, placeholder);
            m_y += 32 * S;
        }

        private void Field(string id, float x, float y, float w, string help, string value, Action<string> commit,
            bool digits, string placeholder = null)
        {
            var h = 24 * S;
            var focused = m_map.FocusedField == id;
            var rect = new RectangleF(x, y, w, h);
            var hover = m_map.AddHit(rect, () => m_map.FocusField(id, value, commit, digits), null, help);
            Gfx.Rect(x, y, w, h, new Color(0, 0, 0, 160));
            Gfx.Frame(x, y, w, h, 1, focused ? Style.Selection : (hover ? Style.Accent : Style.AccentDim));
            var text = focused ? m_map.FieldText : value;
            if (string.IsNullOrEmpty(text) && !focused && !string.IsNullOrEmpty(placeholder))
                Gfx.Text(placeholder, x + 6 * S, y + 3 * S, 0.6f, Style.Dim);
            else
                Gfx.Text(Gfx.Fit(text + (focused && (int)(m_map.Time * 2) % 2 == 0 ? "|" : ""), 0.6f, w - 12 * S),
                    x + 6 * S, y + 3 * S, 0.6f, Color.White);
        }

        public void Button(string label, string help, Action click)
        {
            CloseColumns();
            var w = m_w - 28 * S;
            var h = 26 * S;
            var x = m_x + 14 * S;
            var hover = m_map.AddHit(new RectangleF(x, m_y, w, h), click, null, help);
            Gfx.Rect(x, m_y, w, h, hover ? Style.RowHover : Style.Row);
            Gfx.Frame(x, m_y, w, h, 1, hover ? Style.Accent : Style.AccentDim);
            Gfx.Text(label, x + w / 2, m_y + h / 2, 0.62f, Style.Text, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
            m_y += h + 8 * S;
        }

        // The list fills what is left above the help lines. Rows are added in
        // order; the map scrolls them with the wheel.
        private float m_listTop;
        private float m_listBottom;
        private int m_row;
        private int m_rowsShown;

        public float RowHeight
        {
            get { return 25 * S; }
        }

        public void BeginList(string title, int total)
        {
            Section(title + (total > 0 ? "  (" + total + ")" : ""));
            m_listTop = m_y;
            m_listBottom = m_footerTop - 8 * S;
            m_row = 0;
            m_rowsShown = Math.Max(1, (int)((m_listBottom - m_listTop) / RowHeight));
            m_map.SetListArea(new RectangleF(m_x, m_listTop, m_w, Math.Max(0, m_listBottom - m_listTop)), total, m_rowsShown);
        }

        public void Row(object item, string icon, Color iconColor, string text, string right, int indent, bool dim)
        {
            var index = m_row++ - m_map.ListScroll;
            if (index < 0 || index >= m_rowsShown)
                return;
            var y = m_listTop + index * RowHeight;
            var x = m_x + 10 * S;
            var w = m_w - 20 * S;
            var rect = new RectangleF(x, y, w, RowHeight - 2 * S);
            var hover = m_map.AddHit(rect, () => m_map.ListClick(item), item, null);
            var selected = item != null && ReferenceEquals(item, m_map.Selected);
            Gfx.Rect(x, y, w, RowHeight - 2 * S, selected ? Style.RowSelected : (hover ? Style.RowHover : (index % 2 == 0 ? Style.Row : Color.Transparent)));
            var ix = x + 10 * S + indent * 16 * S;
            if (icon != null)
                Gfx.Sprite(icon, ix + 5 * S, y + RowHeight / 2 - 1 * S, 11 * S, 11 * S, iconColor);
            var rightWidth = string.IsNullOrEmpty(right) ? 0 : Gfx.Measure(right, 0.56f).X + 8 * S;
            var room = w - (ix - x) - 22 * S - rightWidth;
            Gfx.Text(Gfx.Fit(text, 0.6f, room), ix + 16 * S, y + 3 * S, 0.6f, dim ? Style.Memory : (selected ? Color.White : Style.Text));
            if (!string.IsNullOrEmpty(right))
                Gfx.Text(right, x + w - 8 * S, y + 4 * S, 0.56f, dim ? Style.Memory : Style.Dim, MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP);
        }

        public void Empty(string text)
        {
            Gfx.Text(text, m_x + 14 * S, m_listTop + 4 * S, 0.58f, Style.Dim);
        }

        // The help at the bottom of the menu, wrapped on as many lines as it
        // needs. Laid out first, so that the list above stops before it and
        // its last line never leaves the frame.
        public void PrepareFooter(string help)
        {
            m_footerLines.Clear();
            var words = (help ?? "").Split(new[] { "   " }, StringSplitOptions.RemoveEmptyEntries);
            var line = "";
            foreach (var word in words)
            {
                var attempt = line.Length == 0 ? word : line + "   " + word;
                if (Gfx.Measure(attempt, FooterScale).X > m_w - 28 * S && line.Length > 0)
                {
                    m_footerLines.Add(Gfx.Fit(line, FooterScale, m_w - 28 * S));
                    line = word;
                }
                else
                {
                    line = attempt;
                }
            }
            if (line.Length > 0)
                m_footerLines.Add(Gfx.Fit(line, FooterScale, m_w - 28 * S));
            m_footerTop = m_bottom - (14 + FooterLine * m_footerLines.Count) * S;
        }

        public void Footer()
        {
            Gfx.Rect(m_x + 14 * S, m_footerTop, m_w - 28 * S, 1, Gfx.Alpha(Style.AccentDim, 0.8f));
            var lineY = m_footerTop + 6 * S;
            foreach (var line in m_footerLines)
            {
                Gfx.Text(line, m_x + 14 * S, lineY, FooterScale, Style.Dim);
                lineY += FooterLine * S;
            }
        }
    }
}
