using System;

namespace SirHolomap
{
    // The galaxy's camera, without the screen: what the view shows (the
    // galaxy point in the middle of the map and the zoom) and where it is
    // going. The zoom glides in log space, like the camera of the other
    // views; the point the wheel zoomed around stays under the cursor during
    // the whole glide, so that each notch makes the picture grow or shrink
    // around it, never swing nor jump. A notch given while the view still
    // glides goes on from where the view is going.
    //
    // Screen positions are given from the middle of the map, in units of the
    // galaxy's half size at zoom 1: a galaxy point p shows at
    // (p - centre) * zoom.
    public sealed class GalaxyCamera
    {
        // Seconds for half of the remaining way, after a notch. A flat picture
        // that grows by a whole notch at once reads as a jump: a little
        // softer than the 3D views.
        public const double HalfLife = 0.12;

        private readonly GalaxyZoom m_zoom = new GalaxyZoom();
        private double m_centreX;
        private double m_centreY;

        // The galaxy point kept under a screen point while the view glides,
        // and what the shown view still has to catch up, on top of that.
        private double m_pivotX;
        private double m_pivotY;
        private double m_pivotU;
        private double m_pivotV;
        private double m_errorX;
        private double m_errorY;

        public GalaxyCamera()
        {
            Rest();
        }

        // Where the view is going.
        public double TargetZoom
        {
            get { return m_zoom.Target; }
        }

        public double TargetX
        {
            get { return m_centreX; }
        }

        public double TargetY
        {
            get { return m_centreY; }
        }

        // What it shows now.
        public double ShownZoom { get; private set; }
        public double ShownX { get; private set; }
        public double ShownY { get; private set; }

        // Zoomed in past the way back: the system takes over once the view
        // has glided there.
        public bool BackToSystem
        {
            get { return m_zoom.BackToSystem; }
        }

        // The shown zoom at which the view hands over to the system.
        public const double LeaveAt = MapScales.GalaxyBackToSystem * 0.97;

        // How much of the galaxy still shows, from 1 to 0, as the view glides
        // in towards the system from a zoom it was at; 1 when not leaving.
        public double LeaveFade(double from)
        {
            if (!BackToSystem)
                return 1;
            from = Math.Min(from, LeaveAt * 0.98);
            var span = Math.Log(LeaveAt / from);
            var done = Math.Log(Math.Max(ShownZoom, from) / from) / span;
            return done <= 0 ? 1 : (done >= 1 ? 0 : 1 - done);
        }

        public bool Gliding
        {
            get
            {
                return ShownZoom != m_zoom.Target || ShownX != m_centreX || ShownY != m_centreY;
            }
        }

        // The whole galaxy, at once.
        public void Rest()
        {
            m_zoom.Home();
            m_centreX = 0;
            m_centreY = 0;
            ShownZoom = m_zoom.Target;
            ShownX = 0;
            ShownY = 0;
            Pivot(0, 0, 0, 0);
        }

        // Entered from the system: the view starts this close on a place
        // (the current server) and glides out to the whole galaxy, the place
        // gliding to where it rests.
        public void Arrive(double fromZoom, double placeX, double placeY)
        {
            m_zoom.Home();
            m_centreX = 0;
            m_centreY = 0;
            ShownZoom = Math.Max(fromZoom, 1e-3);
            ShownX = placeX;
            ShownY = placeY;
            var zoom = m_zoom.Target;
            Pivot(placeX, placeY, (placeX - m_centreX) * zoom, (placeY - m_centreY) * zoom);
        }

        // Notches of the wheel around a point of the screen: the galaxy point
        // under it in the view the camera is going to stays there.
        public void Wheel(double notches, double u, double v)
        {
            var before = m_zoom.Target;
            var x = m_centreX + u / before;
            var y = m_centreY + v / before;
            m_zoom.Wheel(notches);
            var after = m_zoom.Target;
            m_centreX = x - u / after;
            m_centreY = y - v / after;
            Pivot(x, y, u, v);
        }

        // The picture dragged on the screen: it follows at once.
        public void Pan(double du, double dv)
        {
            m_centreX -= du / m_zoom.Target;
            m_centreY -= dv / m_zoom.Target;
            ShownX -= du / ShownZoom;
            ShownY -= dv / ShownZoom;
            m_pivotU += du;
            m_pivotV += dv;
        }

        // A galaxy point brought to the middle, gliding, the zoom kept.
        public void CentreOn(double x, double y)
        {
            m_centreX = x;
            m_centreY = y;
            Pivot(x, y, 0, 0);
        }

        // Back to the whole galaxy, gliding.
        public void Home()
        {
            m_zoom.Home();
            CentreOn(0, 0);
        }

        public void Update(double dt, double halfLife)
        {
            if (dt <= 0)
                return;
            ShownZoom = ZoomSteps.Smooth(ShownZoom, m_zoom.Target, dt, halfLife);
            var k = 1 - Math.Pow(0.5, dt / Math.Max(halfLife, 1e-4));
            m_errorX *= 1 - k;
            m_errorY *= 1 - k;
            if (Math.Abs(m_errorX) < 1e-7 && Math.Abs(m_errorY) < 1e-7)
            {
                m_errorX = 0;
                m_errorY = 0;
            }
            if (ShownZoom == m_zoom.Target && m_errorX == 0 && m_errorY == 0)
            {
                ShownX = m_centreX;
                ShownY = m_centreY;
                return;
            }
            ShownX = m_pivotX - m_pivotU / ShownZoom + m_errorX;
            ShownY = m_pivotY - m_pivotV / ShownZoom + m_errorY;
        }

        // Where a galaxy point shows now, from the middle of the map.
        public void ToScreen(double x, double y, out double u, out double v)
        {
            u = (x - ShownX) * ShownZoom;
            v = (y - ShownY) * ShownZoom;
        }

        // The same, in the view the camera is going to.
        public void ToTargetScreen(double x, double y, out double u, out double v)
        {
            u = (x - m_centreX) * m_zoom.Target;
            v = (y - m_centreY) * m_zoom.Target;
        }

        // The point to keep under a screen point; whatever the shown view
        // differs from that is caught up softly.
        private void Pivot(double x, double y, double u, double v)
        {
            m_pivotX = x;
            m_pivotY = y;
            m_pivotU = u;
            m_pivotV = v;
            m_errorX = ShownX - (x - u / ShownZoom);
            m_errorY = ShownY - (y - v / ShownZoom);
        }
    }
}
