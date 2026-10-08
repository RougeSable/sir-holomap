using System;
using System.Text;
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
    internal sealed class JoinDialog : MyGuiScreenBase
    {
        private readonly string m_question;
        private readonly bool m_askPassword;
        private readonly Action<string> m_join;
        private MyGuiControlTextbox m_password;
        private bool m_done;

        public JoinDialog(string question, bool askPassword, Action<string> join)
            : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR,
                new Vector2(0.46f, askPassword ? 0.40f : 0.32f), true, null, 0f, 0f)
        {
            m_question = question ?? "";
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

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            var size = Size ?? new Vector2(0.46f, 0.36f);
            AddCaption(Texts.JoinCaption, null, new Vector2(0f, 0.003f));

            var top = -size.Y / 2 + 0.075f;
            var textHeight = m_askPassword ? 0.14f : 0.13f;
            var text = new MyGuiControlMultilineText(
                position: new Vector2(0f, top + textHeight / 2),
                size: new Vector2(size.X * 0.86f, textHeight),
                font: "White",
                textScale: 0.8f,
                textAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                contents: new StringBuilder(m_question),
                drawScrollbarV: false,
                drawScrollbarH: false);
            Controls.Add(text);
            var y = top + textHeight + 0.02f;

            if (m_askPassword)
            {
                var label = new MyGuiControlLabel(
                    position: new Vector2(-size.X * 0.43f, y),
                    text: Texts.JoinPasswordLabel,
                    originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
                Controls.Add(label);
                y += 0.035f;
                m_password = new MyGuiControlTextbox(new Vector2(0f, y), string.Empty, 256);
                m_password.Type = MyGuiControlTextboxType.Password;
                m_password.Size = new Vector2(size.X * 0.86f, m_password.Size.Y);
                m_password.EnterPressed += box => Join();
                m_password.SetToolTip(Texts.JoinPasswordHelp);
                Controls.Add(m_password);
                FocusedControl = m_password;
                y += 0.05f;
            }

            var buttonsY = size.Y / 2 - 0.05f;
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
