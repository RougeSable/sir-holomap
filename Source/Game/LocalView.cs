using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace SirHolomap
{
    public enum LocalAnchor
    {
        Player,
        Body,
        Free,
        // La caméra attachée à une grille que le jeu envoie maintenant : elle remplit
        // l'écran en 3D et la caméra tourne autour d'elle.
        Grid,
        // Centrée sur une grille connue de la mémoire seulement : le jeu n'a rien à
        // y dessiner, la vue montre donc son repère à sa dernière place connue.
        Memory,
    }

    // B : le voisinage, vu d'au-dessus d'un plan virtuel posé à travers le
    // centre de la vue, sur le ciel du monde. Chaque grille pend au-dessus du
    // plan par une ligne qui dit sa hauteur ; les grilles bougent comme elles bougent dans
    // le jeu. Un double clic sur une grille à portée attache la caméra à elle,
    // assez près pour que la grille remplisse l'écran ; sur une grille retenue, il
    // centre la vue sur son repère. Tant qu'une grille est suivie, le plan
    // reste où il était ; un simple clic dans le vide, un clic droit ou
    // la molette la lâche, de retour au-dessus du joueur.
    internal sealed class LocalView : MapView
    {
        // Le plan est le même partout dans le système : l'horizontale
        // du monde.
        public static readonly Vector3D Normal = Vector3D.Up;
        private static readonly Vector3D Reference = Vector3D.Forward;

        private LocalAnchor m_anchor = LocalAnchor.Player;
        private Body m_body;
        private long m_gridId;
        private Marker m_grid;
        private Vector3D m_free;
        private double m_yaw;
        private double m_pitch = 0.9;
        private double m_distance = 4000;
        private bool m_returnToSystem;
        private SystemTab m_returnTab;
        private readonly ZoomLadder m_ladder = new ZoomLadder(0, ZoomRung.Local);
        private Vector3D m_focus;

        // Le plan reste en place tant qu'une grille est suivie.
        private readonly FollowPlane m_plane = new FollowPlane();
        // Le suivi est venu d'une plongée en C : la molette vers l'extérieur y retourne.
        private bool m_followFromSystem;
        // La vue au-dessus du joueur telle qu'elle était avant le double clic, rendue
        // quand le suivi se termine.
        private bool m_hasBefore;
        private double m_beforeYaw;
        private double m_beforePitch;
        private double m_beforeDistance;

        // Le cadre dessiné autour de la grille suivie, à l'écran.
        private bool m_frameShown;
        private Vector2 m_frameAt;
        private float m_frameSize;

        private readonly List<Marker> m_visible = new List<Marker>();
        private readonly List<Vector2> m_visibleAt = new List<Vector2>();

        // Les planètes et les lunes en vue : un clic en montre une dans le menu, un
        // double clic glisse vers son globe, dans la vue Planète.
        private readonly List<Body> m_bodies = new List<Body>();
        private readonly List<Vector2> m_bodiesAt = new List<Vector2>();
        private readonly List<float> m_bodiesRadius = new List<float>();

        public LocalView(MapScreen map) : base(map)
        {
        }

        public LocalAnchor Anchor
        {
            get { return m_anchor; }
        }

        // La grille à laquelle la caméra est attachée, s'il y en a une.
        public Marker LockedGrid
        {
            get { return m_anchor == LocalAnchor.Grid ? m_grid : null; }
        }

        public bool ReturnsToSystem
        {
            get { return m_returnToSystem; }
        }

        // La caméra suit une grille, vivante ou retenue.
        public bool Following
        {
            get { return m_anchor == LocalAnchor.Grid || m_anchor == LocalAnchor.Memory; }
        }

        public override string Title
        {
            get
            {
                if ((m_anchor == LocalAnchor.Grid || m_anchor == LocalAnchor.Memory) && m_grid != null)
                    return m_grid.Name;
                if (m_anchor == LocalAnchor.Body && m_body != null)
                    return m_body.Name;
                return Texts.ModeLocal;
            }
        }

        public override string Subtitle
        {
            get
            {
                var text = Texts.Distance + " " + Distance(m_distance);
                if (m_anchor == LocalAnchor.Grid)
                    return Texts.LockedSubtitle + "  -  " + text;
                if (m_anchor == LocalAnchor.Memory)
                    return Texts.OutOfRange + "  -  " + text;
                return text;
            }
        }

        public override string Help
        {
            get { return m_anchor == LocalAnchor.Grid || m_anchor == LocalAnchor.Memory ? Texts.HelpLocked : Texts.HelpLocal; }
        }

        public override double NeededFar
        {
            get { return m_distance * 4 + 60000 + (m_body != null ? m_body.MaxRadius * 2 : 0); }
        }

        private void Configure(ZoomRung rung)
        {
            m_ladder.Configure(m_anchor == LocalAnchor.Body && m_body != null ? m_body.Radius : 0, rung);
        }

        public void Enter(LocalAnchor anchor, Body body, Marker grid, bool fromSystem, bool continuous)
        {
            // Rien à survoler sur une grille retenue : centrée dessus.
            if (anchor == LocalAnchor.Grid && grid != null && !grid.Live)
                anchor = LocalAnchor.Memory;
            m_plane.Release();
            m_hasBefore = false;
            m_followFromSystem = fromSystem && (anchor == LocalAnchor.Grid || anchor == LocalAnchor.Memory);
            m_anchor = anchor;
            m_body = body;
            m_grid = grid;
            m_gridId = grid != null ? grid.Id : 0;
            if (fromSystem)
            {
                m_returnToSystem = true;
                m_returnTab = Map.Tab;
            }
            else if (anchor != LocalAnchor.Grid && anchor != LocalAnchor.Memory)
            {
                m_returnToSystem = false;
            }
            Configure(ZoomRung.Local);

            if (continuous)
            {
                // Depuis la caméra telle qu'elle va être, jamais depuis l'endroit où elle
                // glisse encore : la vue commence dans l'écart du passage qu'elle
                // vient de franchir.
                FromCamera();
                if (anchor == LocalAnchor.Free)
                    m_free = Camera.TargetFocus;
                var eye = Camera.TargetEye;
                m_distance = m_ladder.Entry(ZoomRung.Local, Math.Max(Vector3D.Distance(eye, AnchorPoint()), 2));
            }
            else
            {
                var heading = Vector3D.Reject(World.PlayerForward, Normal);
                if (heading.LengthSquared() > 1e-6)
                {
                    heading.Normalize();
                    m_yaw = Math.Atan2(Vector3D.Dot(heading, Side), Vector3D.Dot(heading, Reference)) + Math.PI;
                }
                m_pitch = 0.85;
                switch (anchor)
                {
                    case LocalAnchor.Body:
                        m_distance = m_ladder.Entry(ZoomRung.Local, body != null ? body.Radius * (MapScales.PlanetLeaveRadii + 0.3) : 100000);
                        break;
                    case LocalAnchor.Grid:
                        m_distance = LockDistance(grid);
                        m_pitch = 0.5;
                        break;
                    case LocalAnchor.Memory:
                        m_distance = MemoryDistance(grid);
                        break;
                    default:
                        m_distance = NeighbourhoodDistance();
                        break;
                }
            }
            m_focus = AnchorPoint();
            // On arrive sur une grille depuis une autre vue : le plan est posé à travers
            // la grille telle qu'elle est maintenant, à l'échelle du voisinage, et
            // reste là tant que la caméra la suit.
            if (anchor == LocalAnchor.Grid)
                m_plane.Keep(MapWorld.ToVec(m_focus), NeighbourhoodDistance());
        }

        private double NeighbourhoodDistance()
        {
            var sync = World.SyncRadius < double.MaxValue ? World.SyncRadius : 5000;
            return MathHelper.Clamp(sync * 1.1, 1500, 15000);
        }

        // Le plan tel qu'il est à l'écran maintenant : à travers le point que la caméra
        // regarde, à la hauteur du centre de la vue.
        private void KeepPlaneAsShown()
        {
            m_plane.Keep(MapWorld.ToVec(LivePlaneOrigin()), Camera.Distance);
        }

        private Vector3D LivePlaneOrigin()
        {
            var focus = Camera.Focus;
            return focus - Normal * Vector3D.Dot(focus - m_focus, Normal);
        }

        private Vector3D PlaneOrigin
        {
            get { return m_plane.Kept ? MapWorld.ToVector(m_plane.Origin) : LivePlaneOrigin(); }
        }

        private double PlaneDistance
        {
            get { return m_plane.DistanceOr(Camera.Distance); }
        }

        private static double LockDistance(Marker grid)
        {
            return Math.Max(grid != null ? grid.Radius * 1.9 : 50, 12);
        }

        private static double MemoryDistance(Marker grid)
        {
            return MathHelper.Clamp(grid != null ? grid.Radius * 10 : 300, 150, 3000);
        }

        private static Vector3D Side
        {
            get { return Vector3D.Cross(Normal, Reference); }
        }

        private void FromCamera()
        {
            var offset = -Camera.TargetForward;
            m_pitch = MathHelper.Clamp(Math.Asin(MathHelper.Clamp(Vector3D.Dot(offset, Normal), -1, 1)), 0.1, 1.5);
            var flat = Vector3D.Reject(offset, Normal);
            if (flat.LengthSquared() > 1e-8)
            {
                flat.Normalize();
                m_yaw = Math.Atan2(Vector3D.Dot(flat, Side), Vector3D.Dot(flat, Reference));
            }
        }

        private Vector3D AnchorPoint()
        {
            switch (m_anchor)
            {
                case LocalAnchor.Body:
                    return m_body != null ? m_body.Centre : World.PlayerPosition;
                case LocalAnchor.Free:
                    return m_free;
                case LocalAnchor.Grid:
                case LocalAnchor.Memory:
                    m_grid = FindGrid();
                    return m_grid != null ? m_grid.Position : m_focus;
                default:
                    return World.PlayerPosition;
            }
        }

        private Marker FindGrid()
        {
            foreach (var marker in World.Markers)
            {
                if (marker.Id == m_gridId)
                    return marker;
            }
            return m_grid;
        }

        private Vector3D Offset
        {
            get
            {
                var flat = Reference * Math.Cos(m_yaw) + Side * Math.Sin(m_yaw);
                return flat * Math.Cos(m_pitch) + Normal * Math.Sin(m_pitch);
            }
        }

        private Vector3D ScreenUp
        {
            get
            {
                var flat = Reference * Math.Cos(m_yaw) + Side * Math.Sin(m_yaw);
                return -flat * Math.Sin(m_pitch) + Normal * Math.Cos(m_pitch);
            }
        }

        public override void Update(double dt)
        {
            m_focus = AnchorPoint();

            if (m_anchor == LocalAnchor.Grid)
            {
                var radius = m_grid != null ? Math.Max(m_grid.Radius, 3) : 20;
                m_distance = Math.Max(m_distance, radius * 1.1 + MapScales.ClosestToGrid);
                // La grille est sortie de la portée du jeu : sa mémoire reste, et le plan
                // avec elle.
                if (m_grid != null && !m_grid.Live)
                    m_anchor = LocalAnchor.Memory;
            }
            else if (m_anchor == LocalAnchor.Memory)
            {
                // Une grille retenue revue : la caméra s'y attache, le
                // plan restant où il est maintenant.
                if (m_grid != null && m_grid.Live)
                {
                    KeepPlaneAsShown();
                    m_anchor = LocalAnchor.Grid;
                    m_distance = Math.Min(m_distance, LockDistance(m_grid) * 2);
                }
            }
            else
            {
                m_plane.Release();
                Camera.SetTarget(m_focus, -Offset, ScreenUp, m_distance);
                var before = m_ladder.Rung;
                var rung = m_ladder.Update(m_distance);
                if (rung != before)
                {
                    if (rung == ZoomRung.Planet && m_body != null)
                    {
                        Map.GoPlanet(m_body, m_returnToSystem, true, null);
                        return;
                    }
                    if (rung == ZoomRung.System)
                    {
                        Map.GoSystem(m_returnToSystem ? m_returnTab : SystemTab.Orrery, true);
                        return;
                    }
                }
            }

            Camera.SetTarget(m_focus, -Offset, ScreenUp, m_distance);
        }

        // La fin d'un suivi : le voisinage centré sur le joueur, tel qu'il
        // était avant le double clic quand le suivi a commencé ici. La
        // caméra ne fait que glisser ; rien n'atteint le personnage.
        private void BackToPlayer()
        {
            m_anchor = LocalAnchor.Player;
            m_returnToSystem = false;
            m_followFromSystem = false;
            m_plane.Release();
            Configure(ZoomRung.Local);
            if (m_hasBefore)
            {
                m_yaw = m_beforeYaw;
                m_pitch = m_beforePitch;
                m_distance = m_ladder.Entry(ZoomRung.Local, m_beforeDistance);
            }
            else
            {
                var sync = World.SyncRadius < double.MaxValue ? World.SyncRadius : 5000;
                m_pitch = MathHelper.Clamp(m_pitch, 0.08, 1.55);
                m_distance = m_ladder.Entry(ZoomRung.Local, Math.Max(m_distance, MathHelper.Clamp(sync * 0.6, 800, 15000)));
            }
            m_hasBefore = false;
            m_focus = AnchorPoint();
            Camera.SetTarget(m_focus, -Offset, ScreenUp, m_distance);
        }

        // Un simple clic dans le vide, un clic droit ou la molette met fin au
        // suivi. Faux quand aucune grille n'était suivie.
        public bool StopFollowing()
        {
            if (!Following)
                return false;
            BackToPlayer();
            return true;
        }

        public override void Wheel(double notches, bool ctrl)
        {
            if (Following)
            {
                // Une plongée depuis C y retourne quand la molette tourne vers l'extérieur ;
                // sinon la molette ramène toujours le voisinage du
                // joueur.
                if (notches < 0 && m_followFromSystem && m_returnToSystem)
                {
                    m_followFromSystem = false;
                    m_returnToSystem = false;
                    m_plane.Release();
                    Map.GoSystem(m_returnTab, true);
                    return;
                }
                StopFollowing();
                Map.Glide();
                return;
            }
            m_distance = MathHelper.Clamp(ZoomSteps.Apply(m_distance, notches), 2, 1e9);
        }

        public override void Rotate(Vector2 delta)
        {
            m_yaw -= delta.X * 0.006;
            m_pitch = MathHelper.Clamp(m_pitch + delta.Y * 0.005, m_anchor == LocalAnchor.Grid ? -1.4 : 0.08, 1.55);
        }

        // La carte suit le curseur à travers le plan.
        public override void Pan(Vector2 delta)
        {
            if (m_anchor == LocalAnchor.Grid)
            {
                Rotate(delta);
                return;
            }
            var metresPerPixel = 1 / Camera.PixelsPerMetre(m_distance);
            var right = Vector3D.Normalize(Vector3D.Reject(Camera.Right, Normal));
            var ahead = Vector3D.Cross(Normal, right);
            SetFree(m_focus - right * (delta.X * metresPerPixel) + ahead * (delta.Y * metresPerPixel));
        }

        public override void Move(Vector2 keys, double dt)
        {
            if (m_anchor == LocalAnchor.Grid)
                return;
            var right = Vector3D.Normalize(Vector3D.Reject(Camera.Right, Normal));
            var ahead = Vector3D.Cross(Normal, right);
            SetFree(m_focus + (right * keys.X + ahead * keys.Y) * (m_distance * 0.9 * dt));
        }

        private void SetFree(Vector3D point)
        {
            if (m_anchor == LocalAnchor.Body || m_anchor == LocalAnchor.Memory)
            {
                // Loin du centre de la planète : pas de globe où descendre, mais
                // la montée vers le système garde son échelle, si bien que bouger la
                // caméra ne saute jamais à C toute seule.
                m_ladder.LeavePlanet();
                m_distance = m_ladder.Entry(ZoomRung.Local, m_distance);
            }
            m_free = point;
            m_anchor = LocalAnchor.Free;
        }

        public override void Recentre()
        {
            m_anchor = LocalAnchor.Player;
            m_returnToSystem = false;
            m_ladder.LeavePlanet();
            m_distance = m_ladder.Entry(ZoomRung.Local, m_distance);
        }

        public void Lock(Marker grid)
        {
            if (grid == null)
                return;
            PointsDeFonctionnalites.Point(Fonctionnalites.SuiviGrille, Map.World);
            if (!Following)
            {
                // Retenu pour être rendu quand le suivi se termine.
                m_hasBefore = true;
                m_beforeYaw = m_yaw;
                m_beforePitch = m_pitch;
                m_beforeDistance = m_distance;
                m_followFromSystem = false;
                m_plane.Release();
            }
            m_grid = grid;
            m_gridId = grid.Id;
            if (grid.Live)
            {
                // Le plan reste tel qu'il est à l'écran maintenant, où que la grille
                // aille ; suivre une autre grille l'y garde aussi.
                KeepPlaneAsShown();
                m_anchor = LocalAnchor.Grid;
                m_distance = LockDistance(grid);
                m_pitch = 0.45;
            }
            else
            {
                // Une grille retenue ne bouge pas : le plan est posé à travers
                // son repère, qui se montre avec sa fiche.
                m_plane.Release();
                m_anchor = LocalAnchor.Memory;
                m_distance = MemoryDistance(grid);
            }
        }

        public override void DoubleClick(object target)
        {
            var marker = target as Marker;
            if (marker != null)
            {
                if (marker.IsGps || marker.IsSelf)
                {
                    SetFree(marker.Position);
                    return;
                }
                Lock(marker);
                return;
            }
            // Une planète ou une lune en vue : la caméra glisse vers son
            // globe, dans la vue Planète, et le menu montre cet astre.
            var body = target as Body;
            if (body != null && body.Planet != null && !body.Planet.Closed)
                Map.GoPlanet(body, m_returnToSystem, false, null);
        }

        public override void Focus(object target)
        {
        }

        public override object Pick(Vector2 mouse)
        {
            object best = null;
            var bestDistance = 14 * Gfx.Scale;
            for (var i = 0; i < m_visible.Count; i++)
            {
                var d = (m_visibleAt[i] - mouse).Length();
                if (d < bestDistance)
                {
                    best = m_visible[i];
                    bestDistance = d;
                }
            }
            // N'importe où dans le cadre de la grille suivie, c'est la grille : seul un
            // clic en dehors, dans le vide, met fin au suivi.
            if (best == null && m_anchor == LocalAnchor.Grid && m_frameShown && m_grid != null
                && FollowGestures.InsideFrame(mouse.X, mouse.Y, m_frameAt.X, m_frameAt.Y, m_frameSize, Gfx.Scale))
                best = m_grid;
            if (best != null)
                return best;
            // Les repères d'abord ; puis les planètes et les lunes en vue.
            var closest = double.MaxValue;
            for (var i = 0; i < m_bodies.Count; i++)
            {
                var d = (m_bodiesAt[i] - mouse).Length();
                if (d < m_bodiesRadius[i] && d / m_bodiesRadius[i] < closest)
                {
                    best = m_bodies[i];
                    closest = d / m_bodiesRadius[i];
                }
            }
            return best;
        }

        // Juste en dessous (ou au-dessus) d'une position, sur le plan.
        private Vector3D Foot(Vector3D position)
        {
            return position - Normal * Vector3D.Dot(position - PlaneOrigin, Normal);
        }

        public override void DrawMap()
        {
            var focus = Camera.Focus;
            var planeOrigin = PlaneOrigin;
            PrepareOccluders(planeOrigin);
            if (!RenderHooks.Active)
                DrawPaintedBodies();
            DrawPlane(planeOrigin, PlaneDistance);
            DrawSyncRange();
            DrawBodies();

            m_visible.Clear();
            m_visibleAt.Clear();
            var threshold = Settings.SpaceBlockThreshold;
            var reach = Camera.Distance * 8;
            foreach (var marker in World.Markers)
            {
                if (!Passes(marker, threshold))
                    continue;
                if (Vector3D.DistanceSquared(marker.Position, focus) > reach * reach)
                    continue;
                Vector2 at;
                double depth;
                if (!Camera.Project(marker.Position, out at, out depth))
                    continue;
                m_visible.Add(marker);
                m_visibleAt.Add(at);
            }

            // Les lignes de hauteur d'abord, sous chaque repère. La grille suivie
            // a la sienne sur le plan gardé : elle grandit et rétrécit quand la
            // grille monte ou descend.
            var s = Gfx.Scale;
            for (var i = 0; i < m_visible.Count; i++)
            {
                var marker = m_visible[i];
                if (m_anchor == LocalAnchor.Grid && marker.Id == m_gridId && !m_plane.Kept)
                    continue;
                var foot = Foot(marker.Position);
                var color = Gfx.Alpha(Style.MarkerColor(marker), marker.Live ? 0.75f : 0.4f);
                Segment(marker.Position, foot, 1.5f * s, color);
                Vector2 b;
                double depth;
                if (!Occluded(foot) && Camera.Project(foot, out b, out depth))
                    Gfx.Sprite(GameTextures.Shape(Images.Shape.Hexagon), b, 14 * s, color);
            }

            var crowded = m_visible.Count > 30;
            m_frameShown = false;
            for (var i = 0; i < m_visible.Count; i++)
            {
                var marker = m_visible[i];
                if (m_anchor == LocalAnchor.Grid && marker.Id == m_gridId)
                {
                    // La grille remplit l'écran, dessinée par le jeu : seulement de fins
                    // coins autour d'elle, de la taille de la grille à l'écran.
                    Vector2 at;
                    double depth;
                    if (Camera.Project(marker.Position, out at, out depth))
                    {
                        var size = (float)(marker.Radius * 2 * Camera.PixelsPerMetre(depth));
                        Gfx.Brackets(at, size, size, Math.Max(1.5f, 2 * s), Gfx.Alpha(Style.Selection, 0.8f));
                        m_frameShown = true;
                        m_frameAt = at;
                        m_frameSize = size;
                    }
                    continue;
                }
                var extra = marker.IsSelf ? null : Distance(Vector3D.Distance(marker.Position, World.PlayerPosition));
                MarkerPainter.Paint(Map, marker, m_visibleAt[i], !crowded && marker.Live, extra, m_visible.Count <= 80);
            }
        }

        // Le plan virtuel : des lignes à chaque puissance de dix, les plus fines apparaissant
        // en fondu quand la caméra se rapproche, et s'effaçant avec la distance.
        private void DrawPlane(Vector3D focus, double distance)
        {
            var origin = Foot(focus);
            var step = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(distance / 3, 1))));
            var fine = 1 - (Math.Log10(Math.Max(distance / 3, 1)) - Math.Log10(step));
            var range = distance * 3.2;
            DrawLines(origin, step, range, (float)(0.10 + 0.2 * fine));
            DrawLines(origin, step * 10, range, 0.32f);
        }

        private void DrawLines(Vector3D origin, double step, double range, float alpha)
        {
            var count = (int)Math.Ceiling(range / step);
            if (count > 60)
                return;
            var side = Side;
            var height = Vector3D.Dot(origin, Normal);
            var s = Gfx.Scale;
            for (var axis = 0; axis < 2; axis++)
            {
                // Les lignes courent le long d'un axe, une tous les pas en travers de l'autre.
                var along = axis == 0 ? Reference : side;
                var across = axis == 0 ? side : Reference;
                var centreAlong = Vector3D.Dot(origin, along);
                var centreAcross = Vector3D.Dot(origin, across);
                var first = Math.Floor(centreAcross / step) * step;
                for (var i = -count; i <= count; i++)
                {
                    var fixedValue = first + i * step;
                    var offset = fixedValue - centreAcross;
                    if (Math.Abs(offset) >= range)
                        continue;
                    // Segments qui s'effacent avec la distance au centre.
                    const int pieces = 6;
                    for (var p = 0; p < pieces; p++)
                    {
                        var t0 = -range + 2 * range * p / pieces;
                        var t1 = -range + 2 * range * (p + 1) / pieces;
                        var mid = (t0 + t1) / 2;
                        var edge = Math.Sqrt(mid * mid + offset * offset) / range;
                        var fade = (float)Math.Max(0, 1 - edge);
                        if (fade <= 0.02f)
                            continue;
                        var p0 = Normal * height + across * fixedValue + along * (centreAlong + t0);
                        var p1 = Normal * height + across * fixedValue + along * (centreAlong + t1);
                        Segment(p0, p1, 1.2f * s, Gfx.Alpha(Style.Grid, alpha * fade * fade));
                    }
                }
            }
        }

        // Les planètes et les lunes cachent le plan : le jeu les dessine dans la
        // scène, le plan est dessiné par-dessus la scène, donc chaque morceau de ligne
        // qui passe à l'intérieur d'un astre, ou derrière lui vu de la caméra,
        // est laissé de côté. Les astres sont des sphères de leur rayon moyen ; le plan
        // passe par le point sur lequel la vue est centrée, qui reste donc
        // toujours en vue, même dans une vallée sous le rayon moyen.
        private struct Occluder
        {
            public Vector3D Centre;
            public double Radius;
            // Un personnage : une capsule de Bottom à Top, de ce rayon.
            public bool Capsule;
            public Vec3 Bottom;
            public Vec3 Top;
            // Son disque à l'écran, pour sauter les lignes loin de lui ; Wide quand
            // l'astre est trop près de la caméra pour le borner ainsi.
            public Vector2 At;
            public float ScreenRadius;
            public bool Wide;
        }

        private readonly List<Occluder> m_occluders = new List<Occluder>();

        private void PrepareOccluders(Vector3D focus)
        {
            m_occluders.Clear();
            var eye = Camera.Position;
            foreach (var body in World.Bodies)
            {
                var radius = body.Radius;
                var focusDistance = Vector3D.Distance(focus, body.Centre);
                if (focusDistance > radius * 0.5 && focusDistance < radius * 1.002)
                    radius = focusDistance * 0.998;
                var toBody = body.Centre - eye;
                var distance = toBody.Length();
                // Une caméra basse dans une vallée, sous le rayon moyen : l'astre
                // cache encore ce qui se trouve au-delà de sa courbure.
                radius = Math.Min(radius, distance * 0.995);
                if (radius <= 1)
                    continue;
                var occluder = new Occluder { Centre = body.Centre, Radius = radius };
                // L'astre couvre les directions dans un angle de celle
                // vers son centre ; à l'écran, au plus à cette distance d'où son
                // centre tombe, étiré quand il s'éloigne du milieu.
                var spread = Math.Asin(MathHelper.Clamp(radius / distance, 0, 1));
                var off = Math.Acos(MathHelper.Clamp(Vector3D.Dot(toBody / distance, Camera.Forward), -1, 1));
                Vector2 at;
                double projected;
                if (off + spread < 1.45 && Camera.Project(body.Centre, out at, out projected))
                {
                    var pixels = Gfx.Height * 0.5 / Camera.TanHalfFov;
                    var outward = Math.Tan(off + spread) - Math.Tan(off);
                    var inward = Math.Tan(off) - Math.Tan(Math.Max(off - spread, -1.45));
                    var across = Math.Tan(spread) / Math.Max(Math.Cos(off), 0.1);
                    occluder.At = at;
                    occluder.ScreenRadius = (float)(Math.Max(outward, Math.Max(inward, across)) * pixels) + 4;
                }
                else
                {
                    occluder.Wide = true;
                }
                m_occluders.Add(occluder);
            }
            PrepareCharacters();
        }

        // Les personnages cachent le plan comme les astres : celui du joueur,
        // et chaque autre joueur que le jeu envoie maintenant. Chacun est une capsule de
        // ses pieds à sa tête, tournée comme il se tient.
        private readonly HashSet<long> m_characters = new HashSet<long>();

        private void PrepareCharacters()
        {
            m_characters.Clear();
            // Seulement quand le jeu dessine la scène depuis la caméra de la carte :
            // sinon aucun personnage ne se montre derrière le plan.
            if (!RenderHooks.Active)
                return;
            var session = MyAPIGateway.Session;
            var player = session != null ? session.Player : null;
            if (player != null && player.Character != null)
                AddCharacter(player.Character);
            foreach (var marker in World.Markers)
            {
                if (marker.Kind != ContactKind.Character || !marker.Live || marker.IsSelf || marker.IsGps)
                    continue;
                IMyEntity entity;
                if (MyAPIGateway.Entities.TryGetEntityById(marker.Id, out entity))
                    AddCharacter(entity as IMyCharacter);
            }
        }

        private void AddCharacter(IMyCharacter character)
        {
            if (character == null || character.MarkedForClose || character.Closed || !m_characters.Add(character.EntityId))
                return;
            var matrix = character.WorldMatrix;
            var feet = matrix.Translation;
            // Trop loin pour couvrir un pixel : rien à cacher.
            var toFeet = feet - Camera.Position;
            var distance = toFeet.Length();
            if (Occlusion.CharacterHeight * Camera.PixelsPerMetre(Math.Max(distance, 0.01)) < 1.5)
                return;
            double height = Occlusion.CharacterHeight;
            double width = Occlusion.CharacterRadius * 2;
            var box = character.PositionComp != null ? character.PositionComp.LocalAABB : default(BoundingBox);
            if (box.Max.Y > box.Min.Y)
            {
                height = box.Max.Y - Math.Max(0, box.Min.Y);
                width = Math.Min(box.Max.X - box.Min.X, box.Max.Z - box.Min.Z);
            }
            Vec3 bottom, top;
            double radius;
            Occlusion.CharacterCapsule(MapWorld.ToVec(feet), MapWorld.ToVec(matrix.Up), height, width, out bottom, out top, out radius);
            var centre = (MapWorld.ToVector(bottom) + MapWorld.ToVector(top)) / 2;
            var occluder = new Occluder
            {
                Centre = centre,
                Radius = radius,
                Capsule = true,
                Bottom = bottom,
                Top = top,
            };
            Vector2 at;
            double depth;
            var reach = (top - bottom).Length / 2 + radius;
            if (Camera.Project(centre, out at, out depth) && depth > reach * 1.5)
            {
                occluder.At = at;
                occluder.ScreenRadius = (float)(reach * Camera.PixelsPerMetre(depth - reach)) + 4;
            }
            else
            {
                occluder.Wide = true;
            }
            m_occluders.Add(occluder);
        }

        // À l'intérieur d'un astre, ou derrière lui depuis la caméra.
        private bool Occluded(Vector3D point)
        {
            var eye = Camera.Position;
            for (var i = 0; i < m_occluders.Count; i++)
            {
                var o = m_occluders[i];
                if (o.Capsule)
                {
                    if (Occlusion.HiddenByCapsule(MapWorld.ToVec(eye), MapWorld.ToVec(point), o.Bottom, o.Top, o.Radius))
                        return true;
                    continue;
                }
                var r2 = o.Radius * o.Radius;
                if (Vector3D.DistanceSquared(point, o.Centre) < r2)
                    return true;
                var ray = point - eye;
                var length2 = ray.LengthSquared();
                if (length2 < 1e-9)
                    continue;
                var t = MathHelper.Clamp(Vector3D.Dot(o.Centre - eye, ray) / length2, 0, 1);
                if (Vector3D.DistanceSquared(eye + ray * t, o.Centre) < r2)
                    return true;
            }
            return false;
        }

        // Un astre peut-il cacher une partie de ce morceau ? Alors seulement il est découpé.
        private bool NearOccluder(Vector3D a, Vector3D b)
        {
            if (m_occluders.Count == 0)
                return false;
            Vector2 sa, sb;
            var projected = Camera.ProjectSegment(a, b, out sa, out sb);
            for (var i = 0; i < m_occluders.Count; i++)
            {
                var o = m_occluders[i];
                if (o.Wide || !projected)
                    return true;
                if (DistanceToSegment(o.At, sa, sb) < o.ScreenRadius)
                    return true;
                // Un morceau qui passe à l'intérieur de l'astre est coupé, où qu'il se montre.
                if (SegmentInside(a, b, o))
                    return true;
            }
            return false;
        }

        private static bool SegmentInside(Vector3D a, Vector3D b, Occluder o)
        {
            if (o.Capsule)
                return Occlusion.SegmentDistanceSquared(MapWorld.ToVec(a), MapWorld.ToVec(b), o.Bottom, o.Top) < o.Radius * o.Radius;
            var ab = b - a;
            var length2 = ab.LengthSquared();
            var t = length2 > 1e-9 ? MathHelper.Clamp(Vector3D.Dot(o.Centre - a, ab) / length2, 0, 1) : 0;
            return Vector3D.DistanceSquared(a + ab * t, o.Centre) < o.Radius * o.Radius;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var length2 = ab.LengthSquared();
            var t = length2 > 1e-6f ? MathHelper.Clamp(Vector2.Dot(p - a, ab) / length2, 0, 1) : 0;
            return (a + ab * t - p).Length();
        }

        // Une ligne de la carte dans le monde, avec ce que les astres cachent laissé
        // de côté : le bord d'un astre la coupe net.
        private readonly List<double> m_samples = new List<double>();

        private void Segment(Vector3D a, Vector3D b, float thickness, Color color)
        {
            if (!NearOccluder(a, b))
            {
                DrawPiece(a, b, thickness, color);
                return;
            }
            // Régulièrement le long du morceau, et de près là où un personnage pourrait
            // le cacher : une personne est petite à côté d'un morceau du plan.
            const int samples = 24;
            m_samples.Clear();
            for (var i = 0; i <= samples; i++)
                m_samples.Add((double)i / samples);
            var eye = MapWorld.ToVec(Camera.Position);
            for (var i = 0; i < m_occluders.Count; i++)
            {
                var o = m_occluders[i];
                if (!o.Capsule)
                    continue;
                var toCentre = MapWorld.ToVec(o.Centre) - eye;
                var distance = toCentre.Length;
                var reach = (o.Top - o.Bottom).Length / 2 + o.Radius;
                double t0 = 0, t1 = 1;
                if (distance > reach * 1.05
                    && !Occlusion.ConeInterval(eye, MapWorld.ToVec(a), MapWorld.ToVec(b), toCentre,
                        Math.Cos(Math.Asin(reach / distance)), out t0, out t1))
                    continue;
                const int close = 40;
                for (var k = 0; k <= close; k++)
                    m_samples.Add(t0 + (t1 - t0) * k / close);
            }
            m_samples.Sort();

            var previous = !Occluded(a);
            var previousT = 0.0;
            var start = previous ? 0.0 : -1.0;
            for (var i = 1; i < m_samples.Count; i++)
            {
                var t = m_samples[i];
                if (t - previousT < 1e-9)
                    continue;
                var visible = !Occluded(a + (b - a) * t);
                if (visible != previous)
                {
                    var edge = Edge(a, b, previousT, t, previous);
                    if (previous)
                        DrawPiece(a + (b - a) * start, a + (b - a) * edge, thickness, color);
                    else
                        start = edge;
                }
                previous = visible;
                previousT = t;
            }
            if (previous && start >= 0)
                DrawPiece(a + (b - a) * start, b, thickness, color);
        }

        // Où entre deux échantillons la ligne entre ou sort de la vue.
        private double Edge(Vector3D a, Vector3D b, double t0, double t1, bool visibleAtStart)
        {
            for (var i = 0; i < 10; i++)
            {
                var middle = (t0 + t1) / 2;
                if (!Occluded(a + (b - a) * middle) == visibleAtStart)
                    t0 = middle;
                else
                    t1 = middle;
            }
            return visibleAtStart ? t0 : t1;
        }

        private void DrawPiece(Vector3D a, Vector3D b, float thickness, Color color)
        {
            Vector2 sa, sb;
            if (Camera.ProjectSegment(a, b, out sa, out sb))
                Gfx.Line(sa, sb, thickness, color);
        }

        // Là où le jeu cesse d'envoyer : au-delà, la carte montre des souvenirs.
        private void DrawSyncRange()
        {
            if (World.SyncRadius >= double.MaxValue || World.SyncRadius > Camera.Distance * 6)
                return;
            Circle(Foot(World.PlayerPosition), World.SyncRadius, Gfx.Alpha(Style.Live, 0.35f), true);
        }

        private void Circle(Vector3D centre, double radius, Color color, bool dashed)
        {
            const int segments = 96;
            var side = Side;
            var s = Gfx.Scale;
            for (var i = 0; i < segments; i++)
            {
                if (dashed && i % 2 == 1)
                    continue;
                var a0 = 2 * Math.PI * i / segments;
                var a1 = 2 * Math.PI * (i + 1) / segments;
                var p0 = centre + (Reference * Math.Cos(a0) + side * Math.Sin(a0)) * radius;
                var p1 = centre + (Reference * Math.Cos(a1) + side * Math.Sin(a1)) * radius;
                Segment(p0, p1, 1.5f * s, color);
            }
        }

        // Quand le jeu ne dessine pas la scène depuis la caméra de la carte, les
        // astres sont peints par la carte, à leur vraie taille, sous le plan
        // qu'ils cachent.
        private void DrawPaintedBodies()
        {
            var order = new List<Body>(World.Bodies);
            order.Sort((a, b) => Vector3D.DistanceSquared(b.Centre, Camera.Position).CompareTo(Vector3D.DistanceSquared(a.Centre, Camera.Position)));
            foreach (var body in order)
            {
                Vector2 at;
                double depth;
                if (!Camera.Project(body.Centre, out at, out depth))
                    continue;
                var radius = (float)(body.Radius * Camera.PixelsPerMetre(depth));
                if (radius > Gfx.Height * 2)
                    continue;
                OrreryView.DrawGlobe(Map, body, at, Math.Max(radius, 2 * Gfx.Scale), false, false);
            }
        }

        // Les noms des astres en vue, et ce qu'un clic y trouve. Un
        // astre caché derrière un autre n'est pas proposé.
        private void DrawBodies()
        {
            m_bodies.Clear();
            m_bodiesAt.Clear();
            m_bodiesRadius.Clear();
            var s = Gfx.Scale;
            var area = Map.MapArea;
            foreach (var body in World.Bodies)
            {
                Vector2 at;
                double depth;
                if (!Camera.Project(body.Centre, out at, out depth))
                    continue;
                var radius = (float)(body.Radius * Camera.PixelsPerMetre(depth));
                if (radius > Gfx.Height * 2)
                    continue;
                if (at.X + radius < area.X || at.Y + radius < area.Y || at.X - radius > area.Right || at.Y - radius > area.Bottom)
                    continue;
                if (BehindAnotherBody(body))
                    continue;
                var hovered = Same(Map.Hovered, body) || Same(Map.ListHovered, body);
                var selected = Same(Map.Selected, body);
                var shown = Math.Max(radius, 3 * s);
                if (hovered || selected)
                    Gfx.Sprite(GameTextures.Shape(Images.Shape.Ring), at, Math.Min(shown * 2.4f + 10 * s, Gfx.Height * 3),
                        Gfx.Alpha(selected ? Style.Selection : Style.Accent, 0.9f));
                Gfx.Text(body.Name, at.X, at.Y + shown + 6 * s, 0.7f,
                    selected ? Style.Selection : Gfx.Alpha(Style.Text, hovered ? 1f : 0.85f),
                    VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP);
                m_bodies.Add(body);
                m_bodiesAt.Add(at);
                m_bodiesRadius.Add(Math.Max(shown, 12 * s));
            }
        }

        private static bool Same(object item, Body body)
        {
            var other = item as Body;
            return other != null && other.Id == body.Id;
        }

        // Le côté de l'astre tourné vers la caméra est derrière un autre astre.
        private bool BehindAnotherBody(Body body)
        {
            var eye = Camera.Position;
            var toEye = eye - body.Centre;
            var length = toEye.Length();
            if (length <= body.Radius)
                return false;
            var near = body.Centre + toEye / length * body.Radius * 1.001;
            for (var i = 0; i < m_occluders.Count; i++)
            {
                var o = m_occluders[i];
                if (o.Capsule || Vector3D.DistanceSquared(o.Centre, body.Centre) < 1)
                    continue;
                if (Occlusion.HiddenBySphere(MapWorld.ToVec(eye), MapWorld.ToVec(near), MapWorld.ToVec(o.Centre), o.Radius))
                    return true;
            }
            return false;
        }

        public override void DefaultInfo(Panel panel)
        {
            var live = 0;
            var remembered = 0;
            foreach (var marker in World.Markers)
            {
                if (marker.IsSelf || marker.IsGps)
                    continue;
                if (marker.Live)
                    live++;
                else
                    remembered++;
            }
            panel.Heading(Texts.ModeLocal, null);
            panel.Line(Texts.Live, live.ToString(), Style.Live);
            panel.Line(Texts.ShowMemories, remembered.ToString(), Style.Memory);
            if (World.SyncRadius < double.MaxValue)
                panel.Line(Texts.SyncRange, Distance(World.SyncRadius));
            var near = World.NearestBody(World.PlayerPosition);
            if (near != null)
                panel.Line(near.Name, Distance(Math.Max(0, Vector3D.Distance(near.Centre, World.PlayerPosition) - near.Radius)));
        }

        public override void Filters(Panel panel)
        {
            MarkerFilters(panel, false);
        }

        private readonly List<Marker> m_list = new List<Marker>();

        // Les grilles à l'écran, les plus proches du joueur d'abord. En cliquer une
        // la montre sur la carte.
        public override void List(Panel panel)
        {
            m_list.Clear();
            for (var i = 0; i < m_visible.Count; i++)
            {
                var marker = m_visible[i];
                var at = m_visibleAt[i];
                if (marker.IsSelf || at.X < 0 || at.Y < 0 || at.X > Map.PanelLeft || at.Y > Gfx.Height)
                    continue;
                m_list.Add(marker);
            }
            var player = World.PlayerPosition;
            m_list.Sort((a, b) => Vector3D.DistanceSquared(a.Position, player).CompareTo(Vector3D.DistanceSquared(b.Position, player)));
            panel.BeginList(Texts.SectionGrids, m_list.Count);
            if (m_list.Count == 0)
                panel.Empty(Texts.None);
            foreach (var marker in m_list)
            {
                var right = Distance(Vector3D.Distance(marker.Position, player));
                if (!marker.Live && !marker.IsGps)
                    right = TimeAgo.Format(DateTime.UtcNow - marker.LastSeenUtc) + "  " + right;
                panel.Row(marker, Style.Icon(marker), Style.MarkerColor(marker), marker.Name, right, 0, !marker.Live);
            }
        }
    }
}
