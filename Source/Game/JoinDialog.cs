using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Utils;
using VRageMath;

namespace SirHolomap
{
    // The confirmation before leaving for another server: it names the
    // server, tells what is known of it, and, when the server asks for a
    // password, holds a box to type it before leaving. Nothing happens until
    // the player presses the join button; Esc or the other button keeps them
    // where they are.
    //
    // The text is cut into lines at the width of the box with the game's own
    // font, in the language set in the game (whose letters may be drawn
    // larger), and the box grows to hold every line: a long server name or a
    // longer language never spills over the frame.
    internal sealed class JoinDialog : MyGuiScreenBase
    {
        private const string Font = "White";
        private const string CaptionFont = "ScreenCaption";
        private const float Width = 0.56f;
        private const float TextWidth = Width * 0.84f;
        private const float MaxHeight = 0.9f;
        private const float TextTop = 0.095f;
        private const float PasswordBlock = 0.115f;
        private const float ButtonsBlock = 0.105f;

        private sealed class Layout
        {
            public List<string> Lines;
            public float Scale;
            public float LineHeight;
            public float CaptionScale;
            public Vector2 Size;
        }

        private readonly Layout m_layout;
        private readonly bool m_askPassword;
        private readonly Action<string> m_join;
        private MyGuiControlTextbox m_password;
        private bool m_done;

        public JoinDialog(string question, bool askPassword, Action<string> join)
            : this(Measure(question ?? "", askPassword), askPassword, join)
        {
        }

        private JoinDialog(Layout layout, bool askPassword, Action<string> join)
            : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, layout.Size, true, null, 0f, 0f)
        {
            m_layout = layout;
            m_askPassword = askPassword;
            m_join = join;
            CanHideOthers = false;
            EnabledBackgroundFade = true;
            CloseButtonEnabled = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName()
        {
            return "SirHolomapJoinDialog";
        }

        // The game draws a label's text this much larger than asked in some
        // languages: the lines are measured the way they will be drawn.
        private static float DrawnScale(float scale)
        {
            var probe = new MyGuiControlLabel(text: "", textScale: scale, font: Font);
            return probe.TextScaleWithLanguage;
        }

        private static Layout Measure(string question, bool askPassword)
        {
            var layout = new Layout();
            var fixedHeight = TextTop + 0.02f + (askPassword ? PasswordBlock : 0) + ButtonsBlock;
            foreach (var scale in new[] { 0.8f, 0.72f, 0.64f, 0.56f })
            {
                var drawn = DrawnScale(scale);
                layout.Scale = scale;
                layout.LineHeight = MyGuiManager.MeasureString(Font, "Ag", drawn).Y;
                layout.Lines = TextWrap.Wrap(question, text => MyGuiManager.MeasureString(Font, text, drawn).X, TextWidth);
                if (fixedHeight + layout.Lines.Count * layout.LineHeight <= MaxHeight)
                    break;
            }
            layout.Size = new Vector2(Width, Math.Min(MaxHeight, Math.Max(0.3f, fixedHeight + layout.Lines.Count * layout.LineHeight)));

            layout.CaptionScale = 0.8f;
            var caption = Texts.JoinCaption;
            while (layout.CaptionScale > 0.5f
                && MyGuiManager.MeasureString(CaptionFont, caption, DrawnScale(layout.CaptionScale)).X > Width * 0.8f)
                layout.CaptionScale -= 0.05f;
            return layout;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            var size = m_layout.Size;
            AddCaption(Texts.JoinCaption, null, null, m_layout.CaptionScale);

            var left = -TextWidth / 2;
            var y = -size.Y / 2 + TextTop;
            foreach (var line in m_layout.Lines)
            {
                if (line.Length > 0)
                {
                    Controls.Add(new MyGuiControlLabel(
                        position: new Vector2(left, y),
                        text: line,
                        textScale: m_layout.Scale,
                        font: Font,
                        originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP));
                }
                y += m_layout.LineHeight;
            }
            y += 0.02f;

            if (m_askPassword)
            {
                Controls.Add(new MyGuiControlLabel(
                    position: new Vector2(left, y),
                    text: Texts.JoinPasswordLabel,
                    textScale: m_layout.Scale,
                    font: Font,
                    originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP));
                y += 0.04f;
                m_password = new MyGuiControlTextbox(new Vector2(0f, y), string.Empty, 256);
                m_password.Type = MyGuiControlTextboxType.Password;
                m_password.Size = new Vector2(TextWidth, m_password.Size.Y);
                m_password.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP;
                m_password.EnterPressed += box => Join();
                m_password.SetToolTip(Texts.JoinPasswordHelp);
                Controls.Add(m_password);
                FocusedControl = m_password;
            }

            var buttonsY = size.Y / 2 - 0.055f;
            var join = new MyGuiControlButton(
                position: new Vector2(-0.01f, buttonsY),
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER,
                text: new StringBuilder(Texts.JoinButton),
                onButtonClick: button => Join());
            var stay = new MyGuiControlButton(
                position: new Vector2(0.01f, buttonsY),
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                text: new StringBuilder(Texts.StayButton),
                onButtonClick: button => CloseScreen());
            Controls.Add(join);
            Controls.Add(stay);
            if (!m_askPassword)
                FocusedControl = stay;
        }

        private void Join()
        {
            if (m_done)
                return;
            m_done = true;
            var password = m_password != null ? m_password.Text : null;
            CloseScreen();
            if (m_join != null)
                m_join(password);
        }
    }
}
