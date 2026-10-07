using System;

namespace SirHolomap
{
    // Leaving for another server from the galaxy goes through the main menu,
    // as with the game's own server browser. Most refusals are caught before
    // leaving (no answer, full, password, other version); when the other
    // server still refuses afterwards, the game is left at the main menu. The
    // way back notices it and offers to return to the server the player
    // left.
    public sealed class WayBack
    {
        // The game needs a moment between unloading the world and showing
        // its join screens: no conclusion before that.
        public static readonly TimeSpan Settle = TimeSpan.FromSeconds(10);

        // Idle at the menu this long, with nothing being joined or loaded:
        // the join failed.
        public static readonly TimeSpan Idle = TimeSpan.FromSeconds(4);

        // After this, whatever happens is the player's own doing.
        public static readonly TimeSpan GiveUp = TimeSpan.FromMinutes(15);

        public string BackConnection { get; private set; }
        public string BackName { get; private set; }
        public string TargetName { get; private set; }

        private DateTime m_leftAt;
        private DateTime? m_idleSince;

        public bool Armed { get; private set; }

        public void Leave(string backConnection, string backName, string targetName, DateTime now)
        {
            Armed = !string.IsNullOrEmpty(backConnection);
            BackConnection = backConnection ?? "";
            BackName = string.IsNullOrEmpty(backName) ? BackConnection : backName;
            TargetName = targetName ?? "";
            m_leftAt = now;
            m_idleSince = null;
        }

        public void Disarm()
        {
            Armed = false;
            m_idleSince = null;
        }

        // Called often. inWorld: a world is loaded (the join worked, or the
        // player went elsewhere). busy: the game shows a join or loading
        // screen; null when that cannot be told, and then nothing is offered.
        // True once, when the way back should be offered.
        public bool Update(DateTime now, bool inWorld, bool? busy)
        {
            if (!Armed)
                return false;
            if (now - m_leftAt > GiveUp)
            {
                Disarm();
                return false;
            }
            // A world right after leaving may still be the one being left:
            // only once settled does a world mean the join worked.
            if (inWorld)
            {
                m_idleSince = null;
                if (now - m_leftAt >= Settle)
                    Disarm();
                return false;
            }
            if (busy == null)
                return false;
            if (busy.Value)
            {
                m_idleSince = null;
                return false;
            }
            if (m_idleSince == null)
                m_idleSince = now;
            if (now - m_leftAt >= Settle && now - m_idleSince.Value >= Idle)
            {
                Disarm();
                return true;
            }
            return false;
        }
    }
}
