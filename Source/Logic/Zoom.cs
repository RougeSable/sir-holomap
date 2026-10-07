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
        public const double Factor = 1.55;

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

        // Closest the camera comes to the ground in A, and to a grid in B.
        public const double LowestAltitude = 25;
        public const double ClosestToGrid = 4;
    }
}
