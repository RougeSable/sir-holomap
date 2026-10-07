using System;

namespace SirHolomap
{
    public enum ClickKind
    {
        None,
        Single,
        Double,
    }

    // A single click waits to know whether it becomes a double click: the
    // double click dives into what is under the cursor (a planet, a grid, a
    // server), so a single click must not act first.
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

        // A click (press and release without dragging) at time t, over target.
        // Returns Double at once when it completes a double click on the
        // same spot; the single click then never fires.
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

        // Called every frame: the single click fires once the double click
        // window has passed.
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

    // "3 h", "12 days": how long ago, the way a player reads it.
    public static class TimeAgo
    {
        public static string Format(TimeSpan span)
        {
            if (span < TimeSpan.Zero)
                span = TimeSpan.Zero;
            if (span.TotalSeconds < 45)
                return "a few seconds";
            if (span.TotalMinutes < 1.5)
                return "1 min";
            if (span.TotalMinutes < 60)
                return ((int)Math.Round(span.TotalMinutes)) + " min";
            if (span.TotalHours < 1.5)
                return "1 h";
            if (span.TotalHours < 36)
                return ((int)Math.Round(span.TotalHours)) + " h";
            if (span.TotalDays < 60)
                return ((int)Math.Round(span.TotalDays)) + " days";
            if (span.TotalDays < 730)
                return ((int)Math.Round(span.TotalDays / 30.4)) + " months";
            return ((int)Math.Round(span.TotalDays / 365.25)) + " years";
        }
    }
}
