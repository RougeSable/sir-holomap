using System;

namespace SirHolomap
{
    // La caméra de la galaxie, sans l'écran : ce que la vue montre (le
    // point de la galaxie au milieu de la carte et le zoom) et où elle
    // va. Le zoom glisse en espace logarithmique, comme la caméra des autres
    // vues ; le point autour duquel la molette a zoomé reste sous le curseur pendant
    // tout le glissement, si bien que chaque cran fait grandir ou rétrécir l'image
    // autour de lui, sans balancer ni sauter. Un cran donné pendant que la vue
    // glisse encore continue depuis l'endroit où la vue va.
    //
    // Les positions à l'écran sont données depuis le milieu de la carte, en unités de
    // la demi-taille de la galaxie au zoom 1 : un point p de la galaxie se montre à
    // (p - centre) * zoom.
    public sealed class GalaxyCamera
    {
        // Secondes pour la moitié du chemin restant, après un cran. Une image plate
        // qui grandit d'un cran entier d'un coup se lit comme un saut : un peu
        // plus doux que les vues 3D.
        public const double HalfLife = 0.12;

        private readonly GalaxyZoom m_zoom = new GalaxyZoom();
        private double m_centreX;
        private double m_centreY;

        // Le point de la galaxie gardé sous un point de l'écran pendant que la vue glisse,
        // et ce que la vue montrée doit encore rattraper, en plus.
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

        // Où la vue va.
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

        // Ce qu'elle montre maintenant.
        public double ShownZoom { get; private set; }
        public double ShownX { get; private set; }
        public double ShownY { get; private set; }

        // Zoomé au-delà du chemin du retour : le système reprend la main une fois que la vue
        // a glissé jusque-là.
        public bool BackToSystem
        {
            get { return m_zoom.BackToSystem; }
        }

        // Le zoom montré auquel la vue passe la main au système.
        public const double LeaveAt = MapScales.GalaxyBackToSystem * 0.97;

        // Quelle part de la galaxie se montre encore, de 1 à 0, quand la vue glisse
        // vers le système depuis un zoom où elle était ; 1 quand on ne la quitte pas.
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

        // La galaxie entière, d'un coup.
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

        // Entrée depuis le système : la vue commence aussi près sur un lieu
        // (le serveur actuel) et glisse vers l'extérieur jusqu'à la galaxie entière, le lieu
        // glissant vers sa position de repos.
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

        // Des crans de molette autour d'un point de l'écran : le point de la galaxie
        // qui est dessous, dans la vue où la caméra va, y reste.
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

        // L'image tirée sur l'écran : elle suit tout de suite.
        public void Pan(double du, double dv)
        {
            m_centreX -= du / m_zoom.Target;
            m_centreY -= dv / m_zoom.Target;
            ShownX -= du / ShownZoom;
            ShownY -= dv / ShownZoom;
            m_pivotU += du;
            m_pivotV += dv;
        }

        // Un point de la galaxie amené au milieu, en glissant, le zoom gardé.
        public void CentreOn(double x, double y)
        {
            m_centreX = x;
            m_centreY = y;
            Pivot(x, y, 0, 0);
        }

        // Retour à la galaxie entière, en glissant.
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

        // Où un point de la galaxie se montre maintenant, depuis le milieu de la carte.
        public void ToScreen(double x, double y, out double u, out double v)
        {
            u = (x - ShownX) * ShownZoom;
            v = (y - ShownY) * ShownZoom;
        }

        // La même chose, dans la vue où la caméra va.
        public void ToTargetScreen(double x, double y, out double u, out double v)
        {
            u = (x - m_centreX) * m_zoom.Target;
            v = (y - m_centreY) * m_zoom.Target;
        }

        // Le point à garder sous un point de l'écran ; tout ce dont la vue montrée
        // diffère de cela est rattrapé doucement.
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
