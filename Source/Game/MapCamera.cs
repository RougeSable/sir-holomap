using System;
using VRageMath;

namespace SirHolomap
{
    // La caméra de la carte : elle regarde un point de focalisation à distance, et glisse
    // là où les vues lui demandent d'être. La distance se déplace en espace logarithmique, pour qu'une
    // plongée du système entier jusqu'à un globe soit aussi douce qu'un pas de
    // quelques mètres.
    internal sealed class MapCamera
    {
        public Vector3D Focus;
        public double Distance = 1000;
        public QuaternionD Rotation = QuaternionD.Identity;

        public Vector3D TargetFocus;
        public double TargetDistance = 1000;
        public QuaternionD TargetRotation = QuaternionD.Identity;

        // Secondes pour la moitié du chemin restant : court pendant que le joueur
        // glisse, plus long pour un changement de vue.
        public double HalfLife = 0.08;
        public const double QuickHalfLife = 0.06;
        public const double TransitionHalfLife = 0.22;

        // Projection à l'écran de l'image courante.
        public Vector3D Position;
        public Vector3D Forward = Vector3D.Forward;
        public Vector3D Up = Vector3D.Up;
        public Vector3D Right = Vector3D.Right;
        public double TanHalfFov = Math.Tan(MathHelper.ToRadians(35));
        public double Aspect = 16.0 / 9.0;

        public void SetTarget(Vector3D focus, Vector3D forward, Vector3D up, double distance)
        {
            TargetFocus = focus;
            TargetDistance = Math.Max(distance, 0.5);
            if (forward.LengthSquared() > 1e-12)
            {
                forward.Normalize();
                up = Vector3D.Reject(up, forward);
                if (up.LengthSquared() < 1e-10)
                    up = Vector3D.CalculatePerpendicularVector(forward);
                up.Normalize();
                TargetRotation = QuaternionD.CreateFromForwardUp(forward, up);
            }
        }

        // Où la caméra va : les vues prennent la main depuis cela, jamais depuis
        // l'endroit où elle glisse encore.
        public Vector3D TargetForward
        {
            get { return Vector3D.Normalize(Vector3D.Transform(Vector3D.Forward, TargetRotation)); }
        }

        public Vector3D TargetUp
        {
            get { return Vector3D.Normalize(Vector3D.Transform(Vector3D.Up, TargetRotation)); }
        }

        public Vector3D TargetEye
        {
            get { return TargetFocus - TargetForward * TargetDistance; }
        }

        public void Snap()
        {
            Focus = TargetFocus;
            Distance = TargetDistance;
            Rotation = TargetRotation;
            Refresh();
        }

        public void Update(double dt)
        {
            var k = 1 - Math.Pow(0.5, dt / Math.Max(HalfLife, 1e-4));
            Focus = Focus + (TargetFocus - Focus) * k;
            if ((TargetFocus - Focus).LengthSquared() < 1e-6)
                Focus = TargetFocus;
            Distance = ZoomSteps.Smooth(Distance, TargetDistance, dt, HalfLife);
            Rotation = QuaternionD.Slerp(Rotation, TargetRotation, k);
            Rotation.Normalize();
            Refresh();
        }

        // Vrai tant que la caméra est encore loin d'où elle va.
        public bool Moving
        {
            get
            {
                return Math.Abs(Math.Log(Distance / TargetDistance)) > 0.02
                    || (TargetFocus - Focus).Length() > Distance * 0.01;
            }
        }

        private void Refresh()
        {
            var matrix = MatrixD.CreateFromQuaternion(Rotation);
            Forward = Vector3D.Normalize(matrix.Forward);
            Up = Vector3D.Normalize(matrix.Up);
            Right = Vector3D.Normalize(matrix.Right);
            Position = Focus - Forward * Distance;
        }

        public MatrixD View
        {
            get { return MatrixD.CreateLookAt(Position, Position + Forward, Up); }
        }

        public void SetLens(float fov, double aspect)
        {
            TanHalfFov = Math.Tan(Math.Max(0.1, Math.Min(3.0, fov)) / 2);
            Aspect = aspect > 0.1 ? aspect : 16.0 / 9.0;
        }

        // Où un point du monde tombe à l'écran, en pixels. Faux quand il est
        // derrière la caméra.
        public bool Project(Vector3D world, out Vector2 screen, out double depth)
        {
            var rel = world - Position;
            depth = Vector3D.Dot(rel, Forward);
            if (depth <= Distance * 1e-4 + 0.01)
            {
                screen = Vector2.Zero;
                return false;
            }
            var x = Vector3D.Dot(rel, Right);
            var y = Vector3D.Dot(rel, Up);
            screen = ToScreen(x, y, depth);
            return Gfx.IsFinite(screen);
        }

        private Vector2 ToScreen(double x, double y, double depth)
        {
            var nx = x / (depth * TanHalfFov * Aspect);
            var ny = y / (depth * TanHalfFov);
            return new Vector2((float)((nx + 1) * 0.5 * Gfx.Width), (float)((1 - ny) * 0.5 * Gfx.Height));
        }

        // Combien de pixels fait une longueur à une profondeur.
        public double PixelsPerMetre(double depth)
        {
            return Gfx.Height * 0.5 / (Math.Max(depth, 1e-3) * TanHalfFov);
        }

        // Un segment, coupé à ce que la caméra voit (un peu plus large que
        // l'écran) : une ligne qui passe à côté ou derrière la caméra garde le
        // morceau en vue, entier et continu, au lieu de disparaître ou d'être
        // jetée loin de l'écran. Cela garde le plan de B intact, quelle que soit la façon
        // dont la carte est tournée.
        public bool ProjectSegment(Vector3D a, Vector3D b, out Vector2 sa, out Vector2 sb)
        {
            sa = sb = Vector2.Zero;
            var ra = a - Position;
            var rb = b - Position;
            double ax = Vector3D.Dot(ra, Right), ay = Vector3D.Dot(ra, Up), az = Vector3D.Dot(ra, Forward);
            double bx = Vector3D.Dot(rb, Right), by = Vector3D.Dot(rb, Up), bz = Vector3D.Dot(rb, Forward);
            var near = Distance * 1e-3 + 0.05;
            var tx = TanHalfFov * Aspect * 1.25;
            var ty = TanHalfFov * 1.25;
            double t0 = 0, t1 = 1;
            if (!Clip(az - near, bz - near, ref t0, ref t1)
                || !Clip(tx * az - ax, tx * bz - bx, ref t0, ref t1)
                || !Clip(tx * az + ax, tx * bz + bx, ref t0, ref t1)
                || !Clip(ty * az - ay, ty * bz - by, ref t0, ref t1)
                || !Clip(ty * az + ay, ty * bz + by, ref t0, ref t1))
                return false;
            sa = ToScreen(ax + (bx - ax) * t0, ay + (by - ay) * t0, az + (bz - az) * t0);
            sb = ToScreen(ax + (bx - ax) * t1, ay + (by - ay) * t1, az + (bz - az) * t1);
            return Gfx.IsFinite(sa) && Gfx.IsFinite(sb);
        }

        // Garde la partie de [t0, t1] où une fonction linéaire, fa en 0 et fb
        // en 1, n'est pas négative.
        private static bool Clip(double fa, double fb, ref double t0, ref double t1)
        {
            if (fa < 0 && fb < 0)
                return false;
            if (fa < 0)
                t0 = Math.Max(t0, fa / (fa - fb));
            else if (fb < 0)
                t1 = Math.Min(t1, fa / (fa - fb));
            return t0 <= t1;
        }

        // Le point sous un pixel, sur le plan passant par un point avec une normale.
        public bool Unproject(Vector2 pixel, Vector3D planePoint, Vector3D planeNormal, out Vector3D hit)
        {
            var nx = pixel.X / Gfx.Width * 2 - 1;
            var ny = 1 - pixel.Y / Gfx.Height * 2;
            var dir = Forward + Right * (nx * TanHalfFov * Aspect) + Up * (ny * TanHalfFov);
            dir.Normalize();
            var denominator = Vector3D.Dot(dir, planeNormal);
            hit = Vector3D.Zero;
            if (Math.Abs(denominator) < 1e-9)
                return false;
            var t = Vector3D.Dot(planePoint - Position, planeNormal) / denominator;
            if (t <= 0)
                return false;
            hit = Position + dir * t;
            return true;
        }

        // La direction sous un pixel.
        public Vector3D Ray(Vector2 pixel)
        {
            var nx = pixel.X / Gfx.Width * 2 - 1;
            var ny = 1 - pixel.Y / Gfx.Height * 2;
            var dir = Forward + Right * (nx * TanHalfFov * Aspect) + Up * (ny * TanHalfFov);
            return Vector3D.Normalize(dir);
        }
    }
}
