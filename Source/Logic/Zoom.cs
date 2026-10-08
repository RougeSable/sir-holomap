using System;

namespace SirHolomap
{
    // The mouse wheel zooms the same way everywhere: each notch multiplies
    // the camera distance by the same factor instead of adding to it. From a
    // few metres above the ground to the whole system takes a reasonable
    // number of notches.
    public static class ZoomSteps
    {
        // One notch towards the focus divides the distance by this factor.
        public const double Factor = 1.65;

        // The game reports this much wheel value per notch.
        public const int WheelUnitsPerNotch = 120;

        // Positive notches zoom in, negative zoom out.
        public static double Apply(double distance, double notches)
        {
            return distance * Math.Pow(Factor, -notches);
        }

        public static double NotchesFromWheel(int wheelDelta)
        {
            return wheelDelta / (double)WheelUnitsPerNotch;
        }

        // How many notches it takes to go from one distance to another.
        public static int NotchesBetween(double from, double to)
        {
            if (from <= 0 || to <= 0)
                return 0;
            return (int)Math.Ceiling(Math.Abs(Math.Log(to / from)) / Math.Log(Factor) - 1e-9);
        }

        // Moves a distance towards its target in log space: the motion looks
        // the same at every scale. halfLife in seconds.
        public static double Smooth(double current, double target, double dt, double halfLife)
        {
            if (current <= 0 || target <= 0)
                return target;
            var k = 1 - Math.Pow(0.5, dt / Math.Max(halfLife, 1e-4));
            var log = Math.Log(current) + (Math.Log(target) - Math.Log(current)) * k;
            var result = Math.Exp(log);
            return Math.Abs(result / target - 1) < 1e-4 ? target : result;
        }
    }

    // A switch between two views that depends on the scale only, with a gap
    // between the way up and the way down: stopping the wheel right at the
    // limit never makes the view flicker between the two.
    public sealed class ScaleSwitch
    {
        private readonly double m_upAbove;
        private readonly double m_downBelow;

        public ScaleSwitch(double upAbove, double downBelow)
        {
            if (downBelow >= upAbove)
                throw new ArgumentException("The way down must start below the way up.");
            m_upAbove = upAbove;
            m_downBelow = downBelow;
        }

        public double UpAbove
        {
            get { return m_upAbove; }
        }

        public double DownBelow
        {
            get { return m_downBelow; }
        }

        public bool IsUp { get; private set; }

        public void Reset(bool up)
        {
            IsUp = up;
        }

        // True when the view changes.
        public bool Update(double distance)
        {
            if (!IsUp && distance > m_upAbove)
            {
                IsUp = true;
                return true;
            }
            if (IsUp && distance < m_downBelow)
            {
                IsUp = false;
                return true;
            }
            return false;
        }
    }

    // The scales of the map, shared by the views and the tests.
    public static class MapScales
    {
        // Neighbourhood (B) to system (C): grids are a pixel or two beyond this.
        public const double SystemAbove = 60000;
        public const double SystemHysteresis = 0.7;

        // B centred on a planet goes to C past a few planet radii.
        public static ScaleSwitch NeighbourhoodToSystem(double focusRadius)
        {
            var up = Math.Max(SystemAbove, focusRadius * 4.5);
            return new ScaleSwitch(up, up * SystemHysteresis);
        }

        // Planet (A) to neighbourhood (B): the camera distance to the centre of
        // the planet, in planet radii.
        public const double PlanetLeaveRadii = 2.6;
        public const double PlanetReturnRadii = 2.2;

        public static ScaleSwitch PlanetToNeighbourhood(double planetRadius)
        {
            return new ScaleSwitch(planetRadius * PlanetLeaveRadii, planetRadius * PlanetReturnRadii);
        }

        // System (C) to galaxy (D): once the whole system is a few times
        // smaller than the view, the galaxy it lies in takes over. The way
        // back starts a little lower, as everywhere.
        public const double GalaxyAboveExtent = 3;
        public const double GalaxyLowest = 2000000;

        public static ScaleSwitch SystemToGalaxy(double systemExtent)
        {
            var up = Math.Max(GalaxyLowest, systemExtent * GalaxyAboveExtent);
            return new ScaleSwitch(up, up * SystemHysteresis);
        }

        // The galaxy's own zoom: 1 shows it whole. The wheel in stops at the
        // closest zoom; one more notch there goes back to the system.
        public const double GalaxyFarthest = 0.6;
        public const double GalaxyClosest = 16;
        public const double GalaxyOverview = 1;

        // Closest the camera comes to the ground in A, and to a grid in B.
        public const double LowestAltitude = 25;
        public const double ClosestToGrid = 4;

        // Highest the camera goes above the ground in A: just past the way
        // out to B.
        public static double HighestAltitude(double planetRadius)
        {
            return planetRadius * (PlanetLeaveRadii + 0.6);
        }
    }

    public enum ZoomRung
    {
        Planet,
        Local,
        System,
    }

    // The three rungs of the wheel: the planet (A), the neighbourhood (B),
    // the system (C). The rung depends on the scale only: the camera
    // distance to what the view is centred on (the centre of the planet in
    // A, and in B when B is centred on a planet). Each switch has a gap
    // between the way up and the way down, and a view entered from its
    // neighbour starts inside that gap, never on the far side of the limit
    // it just crossed: a wheel that stops on a limit, or a camera still
    // gliding, never makes two views take turns.
    public sealed class ZoomLadder
    {
        private ScaleSwitch m_planet;
        private ScaleSwitch m_system;

        public ZoomLadder(double planetRadius, ZoomRung rung)
        {
            Configure(planetRadius, rung);
        }

        public ZoomRung Rung { get; private set; }
        public double PlanetRadius { get; private set; }

        public ScaleSwitch PlanetSwitch
        {
            get { return m_planet; }
        }

        public ScaleSwitch SystemSwitch
        {
            get { return m_system; }
        }

        public bool HasPlanet
        {
            get { return m_planet != null; }
        }

        // planetRadius: the planet the views are centred on, 0 for none.
        public void Configure(double planetRadius, ZoomRung rung)
        {
            PlanetRadius = Math.Max(0, planetRadius);
            m_planet = planetRadius > 0 ? MapScales.PlanetToNeighbourhood(planetRadius) : null;
            m_system = MapScales.NeighbourhoodToSystem(PlanetRadius);
            Rung = rung == ZoomRung.Planet && m_planet == null ? ZoomRung.Local : rung;
            if (m_planet != null)
                m_planet.Reset(Rung != ZoomRung.Planet);
            m_system.Reset(Rung == ZoomRung.System);
        }

        // The view stops being centred on the planet (the camera moved away,
        // or went back over the player): there is no globe to go down to any
        // more, but the way up to the system keeps the scale of the planet
        // and its state. The camera, still far from the planet's centre,
        // never finds itself past a limit it did not cross.
        public void LeavePlanet()
        {
            m_planet = null;
            if (Rung == ZoomRung.Planet)
                Rung = ZoomRung.Local;
        }

        // The distance a view should start at when it is entered on this
        // rung from a camera at this distance: inside the gap of the switch
        // just crossed.
        public double Entry(ZoomRung rung, double distance)
        {
            switch (rung)
            {
                case ZoomRung.Planet:
                    return m_planet == null ? distance : Math.Min(distance, m_planet.DownBelow * 0.98);
                case ZoomRung.System:
                    return Math.Max(distance, m_system.UpAbove * 1.02);
                default:
                    var low = m_planet != null ? m_planet.UpAbove * 1.02 : 0;
                    var high = m_system.DownBelow * 0.98;
                    return Math.Max(low, Math.Min(high, distance));
            }
        }

        // Called with the distance the camera is going to (not the one it is
        // still gliding through). Returns the rung, changed or not.
        public ZoomRung Update(double distance)
        {
            switch (Rung)
            {
                case ZoomRung.Planet:
                    if (m_planet != null && m_planet.Update(distance) && m_planet.IsUp)
                    {
                        Rung = ZoomRung.Local;
                        m_system.Reset(false);
                    }
                    break;
                case ZoomRung.Local:
                    if (m_planet != null && m_planet.Update(distance) && !m_planet.IsUp)
                    {
                        Rung = ZoomRung.Planet;
                        break;
                    }
                    if (m_system.Update(distance) && m_system.IsUp)
                        Rung = ZoomRung.System;
                    break;
                default:
                    if (m_system.Update(distance) && !m_system.IsUp)
                    {
                        Rung = ZoomRung.Local;
                        if (m_planet != null)
                            m_planet.Reset(true);
                    }
                    break;
            }
            return Rung;
        }
    }
}
