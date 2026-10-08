using System;
using System.Collections.Generic;
using VRageMath;

namespace SirHolomap
{
    public enum LocalAnchor
    {
        Player,
        Body,
        Free,
        // The camera fastened to a grid the game sends now: it fills the
        // screen in 3D and the camera turns around it.
        Grid,
        // Centred on a grid known from memory only: the game has nothing to
        // draw there, so the view shows its marker at its last known place.
        Memory,
    }

    // B: the neighbourhood, seen from above a virtual plane laid through the
    // centre of the view, over the sky of the world. Every grid hangs over
    // the plane by a line that tells its height; grids move as they move in
    // the game. A double click on a grid in range fastens the camera to it,
    // close enough for the grid to fill the screen; on a remembered grid it
    // centres the view on its marker. The wheel out lets go: over the planet
    // the grid stands on (A, the grid still in the middle), or back over the
    // player.
    internal sealed class LocalView : MapView
    {
        // The plane is the same everywhere in the system: the world's
        // horizontal.
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
        private double m_memoryStart = 300;
        private bool m_returnToSystem;
        private SystemTab m_returnTab;
        private readonly ZoomLadder m_ladder = new ZoomLadder(0, ZoomRung.Local);
        private Vector3D m_focus;

        private readonly List<Marker> m_visible = new List<Marker>();
        private readonly List<Vector2> m_visibleAt = new List<Vector2>();

        public LocalView(MapScreen map) : base(map)
        {
        }

        public LocalAnchor Anchor
        {
            get { return m_anchor; }
        }

        // The grid the camera is fastened to, if any.
        public Marker LockedGrid
        {
            get { return m_anchor == LocalAnchor.Grid ? m_grid : null; }
        }

        public bool ReturnsToSystem
        {
            get { return m_returnToSystem; }
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
            // Nothing to fly around on a remembered grid: centred on it.
            if (anchor == LocalAnchor.Grid && grid != null && !grid.Live)
                anchor = LocalAnchor.Memory;
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
                // From the camera as it is going to be, never from where it
                // still glides: the view starts inside the gap of the switch
                // it just crossed.
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
                        m_memoryStart = m_distance;
                        break;
                    default:
                        var sync = World.SyncRadius < double.MaxValue ? World.SyncRadius : 5000;
                        m_distance = MathHelper.Clamp(sync * 1.1, 1500, 15000);
                        break;
                }
            }
            if (anchor == LocalAnchor.Memory)
                m_memoryStart = Math.Max(m_distance, 50);
            m_focus = AnchorPoint();
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
                // Out far enough: let go.
                if (m_distance > Math.Max(radius * 9, 400))
                {
                    LetGo();
                    return;
                }
                // The grid left the range of the game: its memory stays.
                if (m_grid != null && !m_grid.Live)
                {
                    m_anchor = LocalAnchor.Memory;
                    m_memoryStart = Math.Max(m_distance, 50);
                }
            }
            else if (m_anchor == LocalAnchor.Memory)
            {
                // A remembered grid seen again: the camera fastens to it.
                if (m_grid != null && m_grid.Live)
                {
                    m_anchor = LocalAnchor.Grid;
                    m_distance = Math.Min(m_distance, LockDistance(m_grid) * 2);
                }
                else if (m_distance > m_memoryStart * 5)
                {
                    if (m_returnToSystem)
                    {
                        m_returnToSystem = false;
                        Map.GoSystem(m_returnTab, true);
                        return;
                    }
                    BackToPlayer();
                    return;
                }
            }
            else
            {
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

        // The wheel out of a fastened grid: back to the system the dive came
        // from, else over the planet the grid stands on (the grid still in
        // the middle), else over the player.
        private void LetGo()
        {
            var grid = m_grid;
            if (m_returnToSystem)
            {
                m_returnToSystem = false;
                Map.GoSystem(m_returnTab, true);
                return;
            }
            var planet = grid != null ? World.BodyAt(grid.Position, 1.6) : null;
            if (planet != null)
            {
                Camera.SetTarget(m_focus, -Offset, ScreenUp, m_distance);
                Map.GoPlanet(planet, false, true, grid);
                return;
            }
            BackToPlayer();
        }

        private void BackToPlayer()
        {
            var grid = m_grid;
            m_anchor = LocalAnchor.Player;
            m_returnToSystem = false;
            Configure(ZoomRung.Local);
            var sync = World.SyncRadius < double.MaxValue ? World.SyncRadius : 5000;
            m_distance = m_ladder.Entry(ZoomRung.Local, Math.Max(m_distance, MathHelper.Clamp(sync * 0.6, 800, 15000)));
            Map.Selected = grid;
            m_focus = AnchorPoint();
            Camera.SetTarget(m_focus, -Offset, ScreenUp, m_distance);
        }

        public override void Wheel(double notches, bool ctrl)
        {
            m_distance = MathHelper.Clamp(ZoomSteps.Apply(m_distance, notches), 2, 1e9);
        }

        public override void Rotate(Vector2 delta)
        {
            m_yaw -= delta.X * 0.006;
            m_pitch = MathHelper.Clamp(m_pitch + delta.Y * 0.005, m_anchor == LocalAnchor.Grid ? -1.4 : 0.08, 1.55);
        }

        // The map follows the cursor across the plane.
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
                // Away from the planet's centre: no globe to go down to, but
                // the way up to the system keeps its scale, so moving the
                // camera never jumps to C by itself.
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
            m_grid = grid;
            m_gridId = grid.Id;
            if (grid.Live)
            {
                m_anchor = LocalAnchor.Grid;
                m_distance = LockDistance(grid);
                m_pitch = 0.45;
            }
            else
            {
                m_anchor = LocalAnchor.Memory;
                m_distance = MemoryDistance(grid);
                m_memoryStart = m_distance;
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
            var body = target as Body;
            if (body != null)
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
            return best;
        }

        private Vector3D Foot(Vector3D position)
        {
            return position - Normal * Vector3D.Dot(position - m_focus, Normal);
        }

        public override void DrawMap()
        {
            var focus = Camera.Focus;
            DrawPlane(focus, Camera.Distance);
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

            // The height lines first, under every marker.
            var s = Gfx.Scale;
            for (var i = 0; i < m_visible.Count; i++)
            {
                var marker = m_visible[i];
                if (m_anchor == LocalAnchor.Grid && marker.Id == m_gridId)
                    continue;
                Vector2 a, b;
                var foot = Foot(marker.Position);
                if (!Camera.ProjectSegment(marker.Position, foot, out a, out b))
                    continue;
                var color = Gfx.Alpha(Style.MarkerColor(marker), marker.Live ? 0.75f : 0.4f);
                Gfx.Line(a, b, 1.5f * s, color);
                Gfx.Sprite(GameTextures.Shape(Images.Shape.Hexagon), b, 14 * s, color);
            }

            var crowded = m_visible.Count > 30;
            for (var i = 0; i < m_visible.Count; i++)
            {
                var marker = m_visible[i];
                if (m_anchor == LocalAnchor.Grid && marker.Id == m_gridId)
                {
                    // The grid fills the screen, drawn by the game: only thin
                    // corners around it, the size of the grid on screen.
                    Vector2 at;
                    double depth;
                    if (Camera.Project(marker.Position, out at, out depth))
                    {
                        var size = (float)(marker.Radius * 2 * Camera.PixelsPerMetre(depth));
                        Gfx.Brackets(at, size, size, Math.Max(1.5f, 2 * s), Gfx.Alpha(Style.Selection, 0.8f));
                    }
                    continue;
                }
                var extra = marker.IsSelf ? null : Distance(Vector3D.Distance(marker.Position, World.PlayerPosition));
                MarkerPainter.Paint(Map, marker, m_visibleAt[i], !crowded && marker.Live, extra, m_visible.Count <= 80);
            }
        }

        // The virtual plane: lines every power of ten, the finer ones fading
        // in as the camera comes closer, fading out with distance.
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
                // Lines run along one axis, one every step across the other.
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
                    // Segments fading with the distance from the centre.
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
                        Vector2 sa, sb;
                        if (Camera.ProjectSegment(p0, p1, out sa, out sb))
                            Gfx.Line(sa, sb, 1.2f * s, Gfx.Alpha(Style.Grid, alpha * fade * fade));
                    }
                }
            }
        }

        // Where the game stops sending: beyond, the map shows memories.
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
                Vector2 sa, sb;
                if (Camera.ProjectSegment(p0, p1, out sa, out sb))
                    Gfx.Line(sa, sb, 1.5f * s, color);
            }
        }

        private void DrawBodies()
        {
            foreach (var body in World.Bodies)
            {
                Vector2 at;
                double depth;
                if (!Camera.Project(body.Centre, out at, out depth))
                    continue;
                var radius = (float)(body.Radius * Camera.PixelsPerMetre(depth));
                if (radius > Gfx.Height * 2)
                    continue;
                Gfx.Text(body.Name, at.X, at.Y + radius + 6 * Gfx.Scale, 0.7f, Gfx.Alpha(Style.Text, 0.85f),
                    VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP);
            }
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

        // The grids on screen, closest to the player first. Clicking one
        // shows it on the map.
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
