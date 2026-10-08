using System;
using System.Collections.Generic;
using VRageMath;

namespace SirHolomap
{
    // A: the globe of a planet, drawn by the game itself from orbit: the real
    // relief and colours of the planet, any planet. The player sees
    // themselves on it from above, with the grids and the bases. The wheel
    // brings the ground closer, and the game sharpens the relief as the
    // camera comes down; out far enough, the view leaves the globe for the
    // neighbourhood (B), or back to the system (C) when it was opened from
    // there.
    //
    // The camera looks at a point: the ground under it, or a grid the camera
    // is fastened to (a double click on a grid, here or in B). Fastened, the
    // left drag turns around the grid as in B; the wheel zooms the same way
    // in both cases, and the tilt fades out as the camera rises, so that
    // far out it looks straight down on the globe.
    internal sealed class PlanetView : MapView
    {
        public Body Body;
        public bool ReturnToSystem;
        private SystemTab m_returnTab;

        // The ground point looked at, seen from the centre of the planet, and
        // which way is up on screen.
        private Vector3D m_direction = Vector3D.Up;
        private Vector3D m_up = Vector3D.Forward;
        private double m_altitude = 100000;
        private double m_tilt;
        private double m_focusRadius;
        private Marker m_grid;
        private long m_gridId;
        private readonly ZoomLadder m_ladder = new ZoomLadder(60000, ZoomRung.Planet);
        private readonly List<Marker> m_visible = new List<Marker>();
        private readonly List<Vector2> m_visibleAt = new List<Vector2>();

        // The other bodies in sight (a moon, a neighbour planet): a double
        // click glides the globe over to them.
        private readonly List<Body> m_others = new List<Body>();
        private readonly List<Vector2> m_othersAt = new List<Vector2>();
        private readonly List<float> m_othersRadius = new List<float>();

        // The corners around the fastened grid: a click inside keeps it.
        private bool m_lockShown;
        private Vector2 m_lockAt;
        private float m_lockSize;

        public PlanetView(MapScreen map) : base(map)
        {
        }

        public Marker LockedGrid
        {
            get { return m_grid; }
        }

        public override string Title
        {
            get { return m_grid != null ? m_grid.Name : (Body != null ? Body.Name : Texts.KindPlanet); }
        }

        public override string Subtitle
        {
            get
            {
                var text = Texts.Altitude + " " + Distance(m_altitude);
                if (m_grid != null)
                    return (Body != null ? Body.Name : Texts.ModePlanet) + "  -  " + Texts.LockedSubtitle + "  -  " + text;
                return Texts.ModePlanet + "  -  " + text;
            }
        }

        public override string Help
        {
            get { return m_grid != null ? Texts.HelpPlanetLocked + "   " + Texts.HelpLetGo : Texts.HelpPlanet; }
        }

        public override double NeededFar
        {
            get { return Camera.Distance + (Body != null ? Body.MaxRadius * 2.5 : 100000); }
        }

        // grid: a grid to keep in the middle, the camera turning around it.
        public void Enter(Body body, bool fromSystem, bool continuous, Marker grid)
        {
            Body = body;
            ReturnToSystem = fromSystem;
            m_returnTab = Map.Tab;
            m_ladder.Configure(body.Radius, ZoomRung.Planet);
            m_grid = grid;
            m_gridId = grid != null ? grid.Id : 0;
            m_tilt = 0;

            if (continuous)
            {
                // From where the camera is going, never from where it still
                // glides: entering from B never lands beyond the way back.
                var forward = Camera.TargetForward;
                var up = Camera.TargetUp;
                var eye = Camera.TargetEye;
                if (grid != null)
                {
                    var n = Vector3D.Normalize(grid.Position - body.Centre);
                    m_direction = n;
                    var offset = -forward;
                    m_tilt = Math.Acos(MathHelper.Clamp(Vector3D.Dot(offset, n), -1, 1));
                    m_tilt = MathHelper.Clamp(m_tilt, 0, MaxTilt);
                    m_up = up;
                    m_altitude = Camera.TargetDistance;
                }
                else
                {
                    var offset = eye - body.Centre;
                    m_direction = Vector3D.Normalize(offset);
                    m_up = up;
                    var distance = m_ladder.Entry(ZoomRung.Planet, offset.Length());
                    m_altitude = Math.Max(MapScales.LowestAltitude, distance - SurfaceRadius(m_direction));
                }
            }
            else
            {
                var onIt = body.Contains(World.PlayerPosition, 3);
                var from = onIt ? World.PlayerPosition : Camera.Position;
                if ((from - body.Centre).LengthSquared() < 1)
                    from = body.Centre + World.DirectionToSun * body.Radius;
                m_direction = Vector3D.Normalize((grid != null ? grid.Position : from) - body.Centre);
                m_up = onIt ? World.PlayerForward : Camera.Up;
                m_altitude = grid != null ? LockedAltitude(grid) : body.Radius * 1.25;
            }
            m_up = Vector3D.Reject(m_up, m_direction);
            if (m_up.LengthSquared() < 1e-8)
                m_up = Vector3D.CalculatePerpendicularVector(m_direction);
            m_up.Normalize();
            Aim();
        }

        private const double MaxTilt = 1.35;

        // Another body seen from this globe: the view glides over to it and
        // stays a planet view, the whole new globe in sight, turned the way
        // the camera came from. The wheel out then leaves it as it would
        // leave any globe.
        private void GlideTo(Body body)
        {
            if (body == null || body.Planet == null || body.Planet.Closed)
                return;
            var from = Camera.TargetEye;
            var up = Camera.TargetUp;
            Body = body;
            m_ladder.Configure(body.Radius, ZoomRung.Planet);
            m_grid = null;
            m_gridId = 0;
            m_tilt = 0;
            var offset = from - body.Centre;
            m_direction = offset.LengthSquared() > 1 ? Vector3D.Normalize(offset) : World.DirectionToSun;
            m_up = Vector3D.Reject(up, m_direction);
            if (m_up.LengthSquared() < 1e-8)
                m_up = Vector3D.CalculatePerpendicularVector(m_direction);
            m_up.Normalize();
            m_altitude = Math.Min(body.Radius * 1.25, MapScales.HighestAltitude(body.Radius));
            Aim();
            Map.Glide();
        }

        private static double LockedAltitude(Marker grid)
        {
            return Math.Max(grid.Radius * 2.4, 40);
        }

        private double SurfaceRadius(Vector3D direction)
        {
            if (Body == null || Body.Planet == null || Body.Planet.Closed)
                return Body != null ? Body.Radius : 0;
            try
            {
                var above = Body.Centre + direction * (Body.MaxRadius + 50);
                var surface = Body.Planet.GetClosestSurfacePointGlobal(ref above);
                return (surface - Body.Centre).Length();
            }
            catch (Exception)
            {
                return Body.Radius;
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

        // The tilt fades out as the camera rises: from high up, the globe is
        // seen from straight above.
        private double EffectiveTilt
        {
            get
            {
                var fade = MathHelper.Clamp(1 - m_altitude / Math.Max(Body.Radius * 0.5, 1), 0, 1);
                return m_tilt * fade;
            }
        }

        // Where the camera goes, and its distance to the centre of the planet.
        private double Aim()
        {
            Vector3D focus;
            if (m_grid != null)
            {
                focus = m_grid.Position;
                m_direction = Vector3D.Normalize(focus - Body.Centre);
                m_focusRadius = (focus - Body.Centre).Length();
            }
            else
            {
                m_focusRadius = SurfaceRadius(m_direction);
                focus = Body.Centre + m_direction * m_focusRadius;
            }
            m_up = Vector3D.Reject(m_up, m_direction);
            if (m_up.LengthSquared() < 1e-8)
                m_up = Vector3D.CalculatePerpendicularVector(m_direction);
            m_up.Normalize();

            var tilt = EffectiveTilt;
            var offset = m_direction * Math.Cos(tilt) - m_up * Math.Sin(tilt);
            var screenUp = m_direction * Math.Sin(tilt) + m_up * Math.Cos(tilt);
            Camera.SetTarget(focus, -offset, screenUp, m_altitude);
            return (focus + offset * m_altitude - Body.Centre).Length();
        }

        public override void Update(double dt)
        {
            if (Body == null || Body.Planet == null || Body.Planet.Closed)
            {
                Map.GoLocal(LocalAnchor.Player, null, null, false, false);
                return;
            }
            var fresh = World.Follow(Body);
            if (fresh != null)
                Body = fresh;
            if (m_grid != null)
            {
                m_grid = FindGrid();
                if (m_grid != null)
                    m_altitude = Math.Max(m_altitude, m_grid.Radius * 1.1 + MapScales.ClosestToGrid);
            }
            var distance = Aim();

            // Zoomed out past the globe: the neighbourhood, or the system the
            // dive started from.
            var before = m_ladder.Rung;
            if (m_ladder.Update(distance) != before && m_ladder.Rung != ZoomRung.Planet)
            {
                if (ReturnToSystem)
                    Map.GoSystem(m_returnTab, true);
                else
                    Map.GoLocal(LocalAnchor.Body, Body, null, false, true);
            }
        }

        public override void Wheel(double notches, bool ctrl)
        {
            var lowest = m_grid != null ? m_grid.Radius * 1.1 + MapScales.ClosestToGrid : MapScales.LowestAltitude;
            m_altitude = MathHelper.Clamp(ZoomSteps.Apply(m_altitude, notches), lowest, MapScales.HighestAltitude(Body.Radius));
        }

        // Left drag: around the fastened grid, as in B; otherwise the ground
        // follows the cursor.
        public override void Rotate(Vector2 delta)
        {
            if (m_grid != null)
            {
                var turn = QuaternionD.CreateFromAxisAngle(m_direction, -delta.X * 0.006);
                m_up = Vector3D.Transform(m_up, turn);
                m_tilt = MathHelper.Clamp(m_tilt + delta.Y * 0.005, 0, MaxTilt);
                return;
            }
            Drag(delta);
        }

        private void Drag(Vector2 delta)
        {
            var radiansPerPixel = 2 * m_altitude * Camera.TanHalfFov / Gfx.Height / Math.Max(Body.Radius, 1);
            radiansPerPixel = Math.Min(radiansPerPixel, 0.01);
            var right = Vector3D.Cross(-m_direction, m_up);
            m_direction = Vector3D.Normalize(m_direction - right * (delta.X * radiansPerPixel) + m_up * (delta.Y * radiansPerPixel));
            m_up = Vector3D.Normalize(Vector3D.Reject(m_up, m_direction));
        }

        // Middle drag or WASD move over the ground, letting go of a grid.
        public override void Pan(Vector2 delta)
        {
            Release();
            Drag(delta);
        }

        public override void Move(Vector2 keys, double dt)
        {
            Release();
            var pixels = 900 * dt;
            Drag(new Vector2(-keys.X, keys.Y) * (float)pixels);
        }

        // A click beside the fastened grid, or a right click: the camera lets
        // go of it and looks at the ground below, the zoom of the globe as
        // usual.
        public override void LetGo()
        {
            Release();
        }

        private void Release()
        {
            if (m_grid == null)
                return;
            m_altitude = Math.Max(m_altitude + (m_focusRadius - SurfaceRadius(m_direction)), MapScales.LowestAltitude);
            m_grid = null;
            m_gridId = 0;
            m_tilt = 0;
        }

        public override void Recentre()
        {
            Release();
            if (Body != null && Body.Contains(World.PlayerPosition, 3))
            {
                m_direction = Vector3D.Normalize(World.PlayerPosition - Body.Centre);
                m_up = Vector3D.Normalize(Vector3D.Reject(m_up, m_direction));
            }
        }

        private bool OnThisBody(Marker marker)
        {
            if (marker.Body == Body.Name)
                return true;
            return Body.Contains(marker.Position, 2);
        }

        // Hidden behind the globe?
        private bool Hidden(Vector3D point)
        {
            var origin = Camera.Position;
            var ray = point - origin;
            var length = ray.Length();
            if (length < 1)
                return false;
            ray /= length;
            var toCentre = Body.Centre - origin;
            var along = Vector3D.Dot(toCentre, ray);
            if (along <= 0 || along >= length)
                return false;
            var closest2 = toCentre.LengthSquared() - along * along;
            var radius = Body.MinRadius * 0.995;
            if (closest2 >= radius * radius)
                return false;
            var entry = along - Math.Sqrt(radius * radius - closest2);
            return entry < length - 5;
        }

        public override void DrawMap()
        {
            if (Body == null)
                return;
            m_lockShown = false;
            DrawOtherBodies();
            if (!RenderHooks.Active)
                DrawOwnGlobe();
            m_visible.Clear();
            m_visibleAt.Clear();
            var threshold = Settings.PlanetBlockThreshold;
            var labelAll = 0;
            foreach (var marker in World.Markers)
            {
                if (!OnThisBody(marker) || !Passes(marker, threshold))
                    continue;
                Vector2 at;
                double depth;
                if (!Camera.Project(marker.Position, out at, out depth) || Hidden(marker.Position))
                    continue;
                m_visible.Add(marker);
                m_visibleAt.Add(at);
                if (marker.Live && marker.IsGrid)
                    labelAll++;
            }

            // Bases first, ships over them, the player on top.
            for (var pass = 0; pass < 3; pass++)
            {
                for (var i = 0; i < m_visible.Count; i++)
                {
                    var marker = m_visible[i];
                    var order = marker.IsSelf ? 2 : (marker.Kind == ContactKind.Station ? 0 : 1);
                    if (order != pass)
                        continue;
                    if (m_grid != null && marker.Id == m_gridId && RenderHooks.Active)
                    {
                        // Fastened: the game draws the grid, thin corners
                        // around it.
                        var size = (float)(marker.Radius * 2 * Camera.PixelsPerMetre(Math.Max(Camera.Distance, 1)));
                        if (size > 40 * Gfx.Scale)
                        {
                            size = Math.Min(size, Gfx.Height * 0.9f);
                            Gfx.Brackets(m_visibleAt[i], size, size, Math.Max(1.5f, 2 * Gfx.Scale), Gfx.Alpha(Style.Selection, 0.8f));
                            m_lockShown = true;
                            m_lockAt = m_visibleAt[i];
                            m_lockSize = size;
                            continue;
                        }
                    }
                    var label = labelAll <= 20 && marker.IsGrid && marker.Live;
                    MarkerPainter.Paint(Map, marker, m_visibleAt[i], label, null, m_visible.Count <= 80);
                    if (marker.IsSelf)
                        DrawHeading(m_visibleAt[i]);
                }
            }
        }

        // When the game's camera cannot be borrowed (another plugin holds it,
        // or it could not be reached), the game does not draw the globe: the
        // map paints it itself, from the same samples of the real planet as
        // the globes of the system view, seen through the map's camera so
        // that it turns and zooms under the markers.
        private void DrawOwnGlobe()
        {
            var toLight = Settings.DaylightGlobe ? -(Vector3D)RenderHooks.LightDirection : World.DirectionToSun;
            var texture = Map.Globes.ViewOf(Body, Camera, toLight);
            if (texture != null)
            {
                Gfx.Sprite(texture, Gfx.Width / 2, Gfx.Height / 2, Gfx.Width, Gfx.Height, Color.White);
                return;
            }

            // While the first picture is painted: the small globe, at the
            // size the planet takes on screen.
            Vector2 at;
            double depth;
            var distance = Vector3D.Distance(Camera.Position, Body.Centre);
            if (distance <= Body.Radius * 1.001 || !Camera.Project(Body.Centre, out at, out depth))
                return;
            var angle = Math.Asin(Body.Radius / distance);
            var r = (float)Math.Min(Math.Tan(angle) / Camera.TanHalfFov * Gfx.Height / 2, Gfx.Width * 4);
            OrreryView.DrawGlobe(Map, Body, at, r, false, false);
        }

        // The other bodies in sight, named under their disc. The game draws
        // them when it draws the scene; otherwise their small painted globe
        // stands in.
        private void DrawOtherBodies()
        {
            m_others.Clear();
            m_othersAt.Clear();
            m_othersRadius.Clear();
            var s = Gfx.Scale;
            var area = Map.MapArea;
            foreach (var body in World.Bodies)
            {
                if (body.Id == Body.Id)
                    continue;
                Vector2 at;
                double depth;
                if (!Camera.Project(body.Centre, out at, out depth) || Hidden(body.Centre))
                    continue;
                var r = (float)(body.Radius * Camera.PixelsPerMetre(depth));
                if (r > Gfx.Height * 2)
                    continue;
                if (at.X + r < area.X || at.Y + r < area.Y || at.X - r > area.Right || at.Y - r > area.Bottom)
                    continue;
                var hovered = Same(Map.Hovered, body);
                var selected = Same(Map.Selected, body);
                var shown = Math.Max(r, 3 * s);
                if (!RenderHooks.Active)
                {
                    OrreryView.DrawGlobe(Map, body, at, shown, hovered, selected);
                }
                else if (hovered || selected)
                {
                    Gfx.Sprite(GameTextures.Shape(Images.Shape.Ring), at, shown * 2.4f + 10 * s,
                        Gfx.Alpha(selected ? Style.Selection : Style.Accent, 0.9f));
                }
                Gfx.Text(body.Name, at.X, at.Y + shown + 6 * s, 0.66f,
                    selected ? Style.Selection : Gfx.Alpha(Style.Text, hovered ? 1f : 0.85f),
                    VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP);
                m_others.Add(body);
                m_othersAt.Add(at);
                m_othersRadius.Add(Math.Max(shown, 12 * s));
            }
        }

        private static bool Same(object item, Body body)
        {
            var other = item as Body;
            return other != null && other.Id == body.Id;
        }

        // Which way the player looks, as a small arrow.
        private void DrawHeading(Vector2 at)
        {
            Vector2 ahead;
            double depth;
            if (!Camera.Project(World.PlayerPosition + World.PlayerForward * Math.Max(20, m_altitude * 0.05), out ahead, out depth))
                return;
            var direction = ahead - at;
            if (direction.LengthSquared() < 1)
                return;
            direction.Normalize();
            Gfx.Rotated(GameTextures.Shape(Images.Shape.Arrow), at + direction * 18 * Gfx.Scale, 12 * Gfx.Scale, direction, Style.Accent);
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
            if (best != null)
                return best;
            if (m_grid != null && m_lockShown)
            {
                var half = m_lockSize / 2 + 6 * Gfx.Scale;
                if (Math.Abs(mouse.X - m_lockAt.X) < half && Math.Abs(mouse.Y - m_lockAt.Y) < half)
                    return m_grid;
            }
            // Markers first; then the other bodies in sight.
            var closest = double.MaxValue;
            for (var i = 0; i < m_others.Count; i++)
            {
                var d = (m_othersAt[i] - mouse).Length();
                if (d < m_othersRadius[i] && d / m_othersRadius[i] < closest)
                {
                    best = m_others[i];
                    closest = d / m_othersRadius[i];
                }
            }
            return best;
        }

        // A double click goes down to what is under the cursor: a grid in
        // range is fastened, the camera then turns around it; a remembered
        // one is only centred, as there is nothing to draw there; another
        // body takes the place of the globe.
        public override void DoubleClick(object target)
        {
            var other = target as Body;
            if (other != null)
            {
                if (Body == null || other.Id != Body.Id)
                    GlideTo(other);
                return;
            }
            var marker = target as Marker;
            if (marker == null)
                return;
            if (marker.IsGrid && marker.Live)
            {
                m_grid = marker;
                m_gridId = marker.Id;
                m_tilt = 0.6;
                m_altitude = LockedAltitude(marker);
                return;
            }
            Focus(marker);
            m_altitude = Math.Max(MapScales.LowestAltitude * 4, marker.Radius * 6 + 120);
        }

        public override void Focus(object target)
        {
            var marker = target as Marker;
            if (marker == null || Body == null)
                return;
            if (m_grid != null && m_grid.Id != marker.Id)
                Release();
            if (m_grid != null)
                return;
            m_direction = Vector3D.Normalize(marker.Position - Body.Centre);
            m_up = Vector3D.Reject(m_up, m_direction);
            if (m_up.LengthSquared() < 1e-8)
                m_up = Vector3D.CalculatePerpendicularVector(m_direction);
            m_up.Normalize();
        }

        public override void DefaultInfo(Panel panel)
        {
            BodyInfo(panel, Body);
        }

        public override void Filters(Panel panel)
        {
            MarkerFilters(panel, true);
        }

        private readonly List<Marker> m_list = new List<Marker>();

        public override void List(Panel panel)
        {
            m_list.Clear();
            var threshold = Settings.PlanetBlockThreshold;
            foreach (var marker in World.Markers)
            {
                if (marker.IsSelf || !OnThisBody(marker) || !Passes(marker, threshold))
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
