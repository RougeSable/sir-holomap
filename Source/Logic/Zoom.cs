using System;

namespace SirHolomap
{
    // La molette de la souris zoome partout de la même façon : chaque cran multiplie
    // la distance de la caméra par le même facteur au lieu de lui ajouter. De
    // quelques mètres au-dessus du sol jusqu'au système entier, il faut un
    // nombre raisonnable de crans.
    public static class ZoomSteps
    {
        // Un cran vers le point de focalisation divise la distance par ce facteur.
        public const double Factor = 1.65;

        // Le jeu rapporte cette valeur de molette par cran.
        public const int WheelUnitsPerNotch = 120;

        // Les crans positifs zooment avant, les négatifs zooment arrière.
        public static double Apply(double distance, double notches)
        {
            return distance * Math.Pow(Factor, -notches);
        }

        public static double NotchesFromWheel(int wheelDelta)
        {
            return wheelDelta / (double)WheelUnitsPerNotch;
        }

        // Combien de crans il faut pour aller d'une distance à une autre.
        public static int NotchesBetween(double from, double to)
        {
            if (from <= 0 || to <= 0)
                return 0;
            return (int)Math.Ceiling(Math.Abs(Math.Log(to / from)) / Math.Log(Factor) - 1e-9);
        }

        // Rapproche une distance de sa cible en espace logarithmique : le mouvement paraît
        // le même à toutes les échelles. halfLife en secondes.
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

    // Un passage entre deux vues qui ne dépend que de l'échelle, avec un écart
    // entre la montée et la descente : arrêter la molette juste à la
    // limite ne fait jamais clignoter la vue entre les deux.
    public sealed class ScaleSwitch
    {
        private readonly double m_upAbove;
        private readonly double m_downBelow;

        public ScaleSwitch(double upAbove, double downBelow)
        {
            if (downBelow >= upAbove)
                throw new ArgumentException("La descente doit commencer sous la montée.");
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

        // Vrai quand la vue change.
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

    // Les échelles de la carte, partagées par les vues et les tests.
    public static class MapScales
    {
        // Espace proche (B) vers système (C) : les grilles font un pixel ou deux au-delà.
        public const double SystemAbove = 60000;
        public const double SystemHysteresis = 0.7;

        // B centré sur une planète passe à C au-delà de quelques rayons de planète.
        public static ScaleSwitch NeighbourhoodToSystem(double focusRadius)
        {
            var up = Math.Max(SystemAbove, focusRadius * 4.5);
            return new ScaleSwitch(up, up * SystemHysteresis);
        }

        // Planète (A) vers Espace proche (B) : la distance de la caméra au centre de
        // la planète, en rayons de planète.
        public const double PlanetLeaveRadii = 2.6;
        public const double PlanetReturnRadii = 2.2;

        public static ScaleSwitch PlanetToNeighbourhood(double planetRadius)
        {
            return new ScaleSwitch(planetRadius * PlanetLeaveRadii, planetRadius * PlanetReturnRadii);
        }

        // Système (C) vers galaxie (D) : au-delà de ce nombre de fois la taille du
        // système, le système entier est un point et la galaxie prend la main. La vue
        // Système s'ouvre à 1,6 fois : à quelques crans de là.
        public const double GalaxySystemSizes = 12;
        public const double GalaxyHysteresis = 0.5;

        // En dessous de cette taille le système compte pour cette taille : une base seule ou
        // un monde sans planètes a quand même une sortie vers la galaxie.
        public const double SmallestSystem = 200000;

        public static ScaleSwitch SystemToGalaxy(double systemSize)
        {
            var up = Math.Max(systemSize, SmallestSystem) * GalaxySystemSizes;
            return new ScaleSwitch(up, up * GalaxyHysteresis);
        }

        // Le zoom propre de la galaxie, 1 montrant toute la galaxie. Elle repose là
        // chaque fois qu'on y entre, pour que chaque serveur se retrouve toujours au
        // même endroit. Zoomé au-delà du chemin du retour, le système revient : à quelques
        // crans.
        public const double GalaxyHomeZoom = 1;
        public const double GalaxySmallestZoom = 0.6;
        public const double GalaxyBackToSystem = 3.2;

        // La galaxie entrée depuis le système commence aussi près sur le
        // serveur actuel, puis glisse vers sa vue entière.
        public const double GalaxyArrivalZoom = 5;

        public static double GalaxyLargestZoom
        {
            get { return GalaxyBackToSystem * ZoomSteps.Factor; }
        }

        // Crans de la vue Système 2D jusqu'à la galaxie : elle n'a pas de zoom
        // propre.
        public const double FlatSystemNotchesOut = 2;

        // Au plus près la caméra vient du sol en A, et d'une grille en B.
        public const double LowestAltitude = 25;
        public const double ClosestToGrid = 4;

        // Au plus haut la caméra monte au-dessus du sol en A : juste au-delà du chemin
        // de sortie vers B.
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

    // Le zoom de la galaxie, sans l'écran : où il repose, jusqu'où il va,
    // et quand la molette ramène la carte au système.
    public sealed class GalaxyZoom
    {
        public double Target { get; private set; }

        public GalaxyZoom()
        {
            Target = MapScales.GalaxyHomeZoom;
        }

        public void Home()
        {
            Target = MapScales.GalaxyHomeZoom;
        }

        // Rend le facteur par lequel le zoom a été multiplié.
        public double Wheel(double notches)
        {
            var before = Target;
            Target = Math.Max(MapScales.GalaxySmallestZoom,
                Math.Min(MapScales.GalaxyLargestZoom, Target * Math.Pow(ZoomSteps.Factor, notches)));
            return Target / before;
        }

        public bool BackToSystem
        {
            get { return Target > MapScales.GalaxyBackToSystem; }
        }
    }

    // Les trois barreaux de la molette : la planète (A), l'Espace proche (B),
    // le système (C). Le barreau ne dépend que de l'échelle : la distance de la
    // caméra à ce sur quoi la vue est centrée (le centre de la planète en
    // A, et en B quand B est centré sur une planète). Chaque passage a un écart
    // entre la montée et la descente, et une vue entrée depuis sa
    // voisine commence dans cet écart, jamais de l'autre côté de la limite
    // qu'elle vient de franchir : une molette qui s'arrête sur une limite, ou une caméra encore
    // en glissement, ne fait jamais alterner deux vues.
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

        // planetRadius : la planète sur laquelle les vues sont centrées, 0 pour aucune.
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

        // La vue cesse d'être centrée sur la planète (la caméra s'est éloignée,
        // ou est revenue sur le joueur) : il n'y a plus de globe où descendre,
        // mais la montée vers le système garde l'échelle de la planète
        // et son état. La caméra, encore loin du centre de la planète,
        // ne se retrouve jamais au-delà d'une limite qu'elle n'a pas franchie.
        public void LeavePlanet()
        {
            m_planet = null;
            if (Rung == ZoomRung.Planet)
                Rung = ZoomRung.Local;
        }

        // La distance à laquelle une vue doit commencer quand on entre sur ce
        // barreau depuis une caméra à cette distance : dans l'écart du passage
        // qu'on vient de franchir.
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

        // Appelé avec la distance où la caméra va (pas celle qu'elle
        // traverse encore). Rend le barreau, changé ou non.
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
