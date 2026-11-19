using System;

namespace SirHolomap
{
    public enum ClickKind
    {
        None,
        Single,
        Double,
    }

    // Un simple clic attend de savoir s'il devient un double clic : le
    // double clic plonge dans ce qui est sous le curseur (une planète, une grille, un
    // serveur), donc un simple clic ne doit pas agir le premier.
    public sealed class ClickTracker
    {
        public const double DoubleClickSeconds = 0.32;
        public const double SlopPixels = 8;

        private bool m_pending;
        private double m_time;
        private double m_x;
        private double m_y;
        private object m_target;

        public object PendingTarget
        {
            get { return m_pending ? m_target : null; }
        }

        // Un clic (appuyé et relâché sans glisser) au temps t, sur la cible.
        // Rend Double tout de suite quand il complète un double clic au
        // même endroit ; le simple clic ne part alors jamais.
        public ClickKind Click(double x, double y, double t, object target, out object clicked)
        {
            if (m_pending && t - m_time <= DoubleClickSeconds
                && Math.Abs(x - m_x) <= SlopPixels && Math.Abs(y - m_y) <= SlopPixels)
            {
                m_pending = false;
                clicked = m_target ?? target;
                m_target = null;
                return ClickKind.Double;
            }

            m_pending = true;
            m_time = t;
            m_x = x;
            m_y = y;
            m_target = target;
            clicked = null;
            return ClickKind.None;
        }

        // Appelé à chaque image : le simple clic part une fois la fenêtre du
        // double clic passée.
        public ClickKind Poll(double t, out object clicked)
        {
            if (m_pending && t - m_time > DoubleClickSeconds)
            {
                m_pending = false;
                clicked = m_target;
                m_target = null;
                return ClickKind.Single;
            }
            clicked = null;
            return ClickKind.None;
        }

        public void Cancel()
        {
            m_pending = false;
            m_target = null;
        }
    }

    // « 3 h », « 12 jours » : depuis combien de temps, comme un joueur le lit.
    public static class TimeAgo
    {
        public static string Format(TimeSpan span)
        {
            if (span < TimeSpan.Zero)
                span = TimeSpan.Zero;
            if (span.TotalSeconds < 45)
                return Texts.AgoFewSeconds;
            if (span.TotalMinutes < 1.5)
                return string.Format(Texts.AgoMinutes, 1);
            if (span.TotalMinutes < 60)
                return string.Format(Texts.AgoMinutes, (int)Math.Round(span.TotalMinutes));
            if (span.TotalHours < 1.5)
                return string.Format(Texts.AgoHours, 1);
            if (span.TotalHours < 36)
                return string.Format(Texts.AgoHours, (int)Math.Round(span.TotalHours));
            if (span.TotalDays < 60)
                return string.Format(Texts.AgoDays, (int)Math.Round(span.TotalDays));
            if (span.TotalDays < 730)
                return string.Format(Texts.AgoMonths, (int)Math.Round(span.TotalDays / 30.4));
            return string.Format(Texts.AgoYears, (int)Math.Round(span.TotalDays / 365.25));
        }
    }
}
