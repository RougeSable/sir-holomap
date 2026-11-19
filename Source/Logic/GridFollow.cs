using System;

namespace SirHolomap
{
    // Suivre une grille dans la vue Espace proche : les gestes qui y mettent fin.
    // Un simple clic (gauche dans le vide, ou droit n'importe où sur la carte)
    // lâche la grille ; un glissement ne fait que tourner la vue autour d'elle.
    public static class FollowGestures
    {
        // Un appui relâché plus près que cela de son point de départ est un clic ;
        // plus loin, un glissement. En pixels à la résolution de référence.
        public const double ClickSlopPixels = 5;

        // La marge autour du cadre dessiné autour de la grille suivie qui compte
        // encore comme la grille : un clic là garde le suivi.
        public const double FrameMarginPixels = 10;

        public static bool IsClick(double pressX, double pressY, double releaseX, double releaseY, double scale)
        {
            var dx = releaseX - pressX;
            var dy = releaseY - pressY;
            return Math.Sqrt(dx * dx + dy * dy) <= ClickSlopPixels * Math.Max(scale, 0.01);
        }

        // Dans le cadre carré de côté `size` dessiné autour de la grille
        // suivie, centré sur elle à l'écran.
        public static bool InsideFrame(double x, double y, double centreX, double centreY, double size, double scale)
        {
            var half = Math.Max(size, 0) / 2 + FrameMarginPixels * Math.Max(scale, 0.01);
            return Math.Abs(x - centreX) <= half && Math.Abs(y - centreY) <= half;
        }
    }

    // Le plan virtuel de la vue Espace proche. Il passe par le
    // point sur lequel la vue est centrée ; tant que la caméra suit une grille, il reste
    // là où il était et tel qu'il était avant le double clic, pour qu'il ne
    // bouge jamais par rapport aux planètes, et que la ligne de hauteur de la grille suivie
    // grandisse et rétrécisse quand la grille monte ou descend.
    public sealed class FollowPlane
    {
        public bool Kept { get; private set; }

        // Le centre du plan, un point sur lui.
        public Vec3 Origin { get; private set; }

        // La distance de la caméra pour laquelle le plan a été dessiné : elle fixe l'espacement
        // des lignes et leur portée.
        public double Distance { get; private set; }

        // Garde le plan tel qu'il est maintenant. Un plan déjà gardé reste tel
        // qu'il était : suivre une autre grille ne le déplace pas non plus.
        public void Keep(Vec3 origin, double distance)
        {
            if (Kept)
                return;
            Kept = true;
            Origin = origin;
            Distance = Math.Max(distance, 1);
        }

        public void Release()
        {
            Kept = false;
        }

        // Où le plan se trouve cette image : gardé, ou passant par le centre vivant.
        public Vec3 OriginOr(Vec3 live)
        {
            return Kept ? Origin : live;
        }

        public double DistanceOr(double live)
        {
            return Kept ? Distance : live;
        }

        // Le point du plan juste en dessous (ou au-dessus) d'une position, le long
        // de la normale du plan (de longueur un).
        public static Vec3 Foot(Vec3 position, Vec3 planePoint, Vec3 normal)
        {
            var d = position - planePoint;
            var height = d.X * normal.X + d.Y * normal.Y + d.Z * normal.Z;
            return position - normal * height;
        }
    }
}
