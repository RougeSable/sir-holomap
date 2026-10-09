using System;
using System.Collections.Generic;
using VRage.Utils;
using VRageMath;

namespace SirHolomap
{
    // C, second tab: the system in 3D. Distances are true; planets and moons
    // are enlarged to a readable size, never smaller than the real thing.
    // Distance rings around the centre of the world on the system plane, a
    // line from every body down to the plane, and the sun as a bright mark on
    // the edge of the screen, in its true direction. Centred on the zone
    // closest to the player.
    internal sealed class OrreryView : MapView
    {
        private Vector3D m_focus;
        private Body m_zone;
        private double m_yaw;
        private double m_pitch = 0.75;
        private double m_distance = 2e6;
        private readonly ZoomLadder m_ladder = new ZoomLadder(0, ZoomRung.System);
        private ScaleSwitch m_galaxy = MapScales.SystemToGalaxy(0);
        private int m_lastMarkers;

        private readonly List<object> m_items = new List<object>();
        private readonly List<Vector2> m_itemsAt = new List<Vector2>();
        private readonly List<float> m_itemsRadius = new List<float>();

        public OrreryView(MapScreen map) : base(map)
        {
        }

        public override string Title
        {
            get { return Texts.ModeSystem; }
        }

        public override string Subtitle
        {
            get { return (m_zone != null ? m_zone.Name + "  -  " : "") + Texts.Distance + " " + Distance(m_distance); }
        }

        public override string Help
        {
            get { return Texts.HelpOrrery; }
        }

        public override double NeededFar
        {
            // The camera may still glide in from the galaxy, farther out.
            get { return Math.Max(m_distance, Camera.Distance) * 3 + Extent() * 2; }
        }

        private double Extent()
        {
            var extent = 0.0;
            foreach (var body in World.Bodies)
                extent = Math.Max(extent, Vector3D.Distance(body.Centre, m_focus) + body.Radius);
            return extent;
        }

        // The zone closest to the player: the planet they are near, or the
        // player themselves out in deep space.
        private void FindZone()
        {
            var near = World.NearestBody(World.PlayerPosition);
            if (near != null && Vector3D.Distance(near.Centre, World.PlayerPosition) < near.Radius * 12)
            {
                m_zone = near.Parent ?? near;
                m_focus = m_zone.Centre;
            }
            else
            {
                m_zone = null;
                m_focus = World.PlayerPosition;
            }
        }

        // The way out to the galaxy, measured on the whole system once, when
        // the view is entered: it never moves while the wheel turns.
        private void PrepareGalaxy()
        {
            var size = 0.0;
            foreach (var body in World.Bodies)
                size = Math.Max(size, body.Centre.Length() + body.Radius);
            m_galaxy = MapScales.SystemToGalaxy(Math.Max(size, Extent()));
            m_galaxy.Reset(false);
        }

        public void Enter(bool continuous)
        {
            FindZone();
            m_ladder.Configure(m_zone != null ? m_zone.Radius : 0, ZoomRung.System);
            PrepareGalaxy();
            if (continuous)
            {
                var offset = -Camera.TargetForward;
                m_pitch = MathHelper.Clamp(Math.Asin(MathHelper.Clamp(Vector3D.Dot(offset, LocalView.Normal), -1, 1)), 0.1, 1.5);
                var flat = Vector3D.Reject(offset, LocalView.Normal);
                if (flat.LengthSquared() > 1e-8)
                {
                    flat.Normalize();
                    m_yaw = Math.Atan2(Vector3D.Dot(flat, Vector3D.Cross(LocalView.Normal, Vector3D.Forward)), Vector3D.Dot(flat, Vector3D.Forward));
                }
                m_distance = m_ladder.Entry(ZoomRung.System, Vector3D.Distance(Camera.TargetEye, m_focus));
            }
            else
            {
                m_pitch = 0.75;
                var extent = Math.Max(Extent(), 200000);
                m_distance = m_ladder.Entry(ZoomRung.System, extent * 1.6);
            }
            m_distance = Math.Min(m_distance, m_galaxy.DownBelow * 0.9);
        }

        // Back from the galaxy, zooming in: the camera comes from far out and
        // glides in to the system, inside the gap of the way out so that the
        // galaxy does not come straight back.
        public void EnterFromGalaxy()
        {
            FindZone();
            m_ladder.Configure(m_zone != null ? m_zone.Radius : 0, ZoomRung.System);
            PrepareGalaxy();
            m_distance = Math.Max(m_ladder.Entry(ZoomRung.System, m_galaxy.DownBelow * 0.9), 1000);
            Camera.SetTarget(m_focus, -Offset, ScreenUp, m_galaxy.UpAbove * 1.5);
            Camera.Snap();
        }

        private Vector3D Offset
        {
            get
            {
                var reference = Vector3D.Forward;
                var side = Vector3D.Cross(LocalView.Normal, reference);
                var flat = reference * Math.Cos(m_yaw) + side * Math.Sin(m_yaw);
                return flat * Math.Cos(m_pitch) + LocalView.Normal * Math.Sin(m_pitch);
            }
        }

        private Vector3D ScreenUp
        {
            get
            {
                var reference = Vector3D.Forward;
                var side = Vector3D.Cross(LocalView.Normal, reference);
                var flat = reference * Math.Cos(m_yaw) + side * Math.Sin(m_yaw);
                return -flat * Math.Sin(m_pitch) + LocalView.Normal * Math.Cos(m_pitch);
            }
        }

        public override void Update(double dt)
        {
            Camera.SetTarget(m_focus, -Offset, ScreenUp, m_distance);
            // Zoomed in far enough: the neighbourhood of the zone.
            var before = m_ladder.Rung;
            if (m_ladder.Update(m_distance) != before && m_ladder.Rung != ZoomRung.System)
            {
                if (m_zone != null)
                    Map.GoLocal(LocalAnchor.Body, m_zone, null, false, true);
                else
                    Map.GoLocal(LocalAnchor.Player, null, null, false, true);
                return;
            }
            // Zoomed out far enough: the whole system is a dot, the galaxy.
            if (m_galaxy.Update(m_distance) && m_galaxy.IsUp)
                Map.GoGalaxy(true);
        }

        public override void Wheel(double notches, bool ctrl)
        {
            if (ctrl)
            {
                // Ctrl and the wheel fly the camera forward instead.
                m_focus += Camera.Forward * (m_distance * 0.25 * notches);
                return;
            }
            m_distance = MathHelper.Clamp(ZoomSteps.Apply(m_distance, notches), 1000, m_galaxy.UpAbove * ZoomSteps.Factor);
        }

        public override void Rotate(Vector2 delta)
        {
            m_yaw -= delta.X * 0.006;
            m_pitch = MathHelper.Clamp(m_pitch + delta.Y * 0.005, 0.05, 1.55);
        }

        public override void Pan(Vector2 delta)
        {
            var metresPerPixel = 1 / Camera.PixelsPerMetre(m_distance);
            var right = Vector3D.Normalize(Vector3D.Reject(Camera.Right, LocalView.Normal));
            var ahead = Vector3D.Cross(LocalView.Normal, right);
            m_focus += -right * (delta.X * metresPerPixel) + ahead * (delta.Y * metresPerPixel);
        }

        public override void Move(Vector2 keys, double dt)
        {
            var right = Vector3D.Normalize(Vector3D.Reject(Camera.Right, LocalView.Normal));
            var ahead = Vector3D.Cross(LocalView.Normal, right);
            m_focus += (right * keys.X + ahead * keys.Y) * (m_distance * 0.9 * dt);
        }

        public override void Recentre()
        {
            FindZone();
        }

        public override void Focus(object target)
        {
            var body = target as Body;
            if (body != null)
            {
                m_focus = body.Centre;
                return;
            }
            var marker = target as Marker;
            if (marker != null)
                m_focus = marker.Position;
        }

        public override void DoubleClick(object target)
        {
            var body = target as Body;
            if (body != null)
            {
                Map.GoPlanet(body, true, false, null);
                return;
            }
            var marker = target as Marker;
            if (marker == null)
                return;
            if (marker.IsGrid || marker.Kind == ContactKind.Character)
            {
                Map.GoLocal(marker.Live ? LocalAnchor.Grid : LocalAnchor.Memory, null, marker, true, false);
                Map.Selected = marker;
            }
        }

        public override object Pick(Vector2 mouse)
        {
            object best = null;
            var bestDistance = double.MaxValue;
            for (var i = 0; i < m_items.Count; i++)
            {
                var d = (m_itemsAt[i] - mouse).Length() - m_itemsRadius[i];
                if (d < 8 * Gfx.Scale && d < bestDistance)
                {
                    best = m_items[i];
                    bestDistance = d;
                }
            }
            return best;
        }

        // Enlarged, but never smaller than the real thing.
        public static float BodyRadius(Body body, MapCamera camera, double depth)
        {
            var real = (float)(body.Radius * camera.PixelsPerMetre(depth));
            var readable = (float)(7 + 5 * Math.Log10(Math.Max(body.Radius, 1000) / 1000)) * Gfx.Scale;
            return Math.Max(real, readable);
        }

        public override void DrawMap()
        {
            m_items.Clear();
            m_itemsAt.Clear();
            m_itemsRadius.Clear();
            var s = Gfx.Scale;
            var centre = Vector3D.Zero;

            // Rings around the centre of the world, through every planet.
            foreach (var body in World.Bodies)
            {
                if (body.IsMoon)
                    continue;
                var flat = Vector3D.Reject(body.Centre - centre, LocalView.Normal);
                var radius = flat.Length();
                if (radius > 1000)
                    Ring(centre, radius, Gfx.Alpha(Style.Orbit, 0.28f));
            }

            // Bodies, farthest first so that near ones cover them.
            var order = new List<Body>(World.Bodies);
            order.Sort((a, b) => Vector3D.DistanceSquared(b.Centre, Camera.Position).CompareTo(Vector3D.DistanceSquared(a.Centre, Camera.Position)));
            foreach (var body in order)
            {
                Vector2 at;
                double depth;
                if (!Camera.Project(body.Centre, out at, out depth))
                    continue;
                var foot = body.Centre - LocalView.Normal * Vector3D.Dot(body.Centre - centre, LocalView.Normal);
                Vector2 a, b;
                if (Camera.ProjectSegment(body.Centre, foot, out a, out b))
                {
                    Gfx.Line(a, b, 1.2f * s, Gfx.Alpha(Style.Orbit, 0.45f));
                    Gfx.Sprite(GameTextures.Shape(Images.Shape.Disc), b, 5 * s, Gfx.Alpha(Style.Orbit, 0.6f));
                }

                var r = BodyRadius(body, Camera, depth);
                var hovered = ReferenceEquals(Map.Hovered, body) || ReferenceEquals(Map.ListHovered, body);
                var selected = ReferenceEquals(Map.Selected, body);
                DrawGlobe(Map, body, at, r, hovered, selected);
                Gfx.Text(body.Name, at.X, at.Y + r + 4 * s, body.IsMoon ? 0.56f : 0.68f,
                    selected ? Style.Selection : Gfx.Alpha(Style.Text, body.IsMoon ? 0.75f : 0.95f), MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP);
                m_items.Add(body);
                m_itemsAt.Add(at);
                m_itemsRadius.Add(r);
            }

            // Grids, players and GPS points.
            var threshold = Settings.SpaceBlockThreshold;
            var markers = 0;
            foreach (var marker in World.Markers)
            {
                if (!Passes(marker, threshold))
                    continue;
                Vector2 at;
                double depth;
                if (!Camera.Project(marker.Position, out at, out depth))
                    continue;
                if (at.X < -20 || at.Y < -20 || at.X > Gfx.Width + 20 || at.Y > Gfx.Height + 20)
                    continue;
                markers++;
                MarkerPainter.Paint(Map, marker, at, false, null, m_lastMarkers <= 60);
                m_items.Add(marker);
                m_itemsAt.Add(at);
                m_itemsRadius.Add(4 * s);
            }

            m_lastMarkers = markers;
            DrawSun(Camera, World.DirectionToSun, Map.MapArea);
        }

        public static void DrawGlobe(MapScreen map, Body body, Vector2 at, float r, bool hovered, bool selected)
        {
            var s = Gfx.Scale;
            if (hovered || selected)
                Gfx.Sprite(GameTextures.Shape(Images.Shape.Glow), at, r * 3.2f, Gfx.Alpha(selected ? Style.Selection : Style.Accent, 0.4f));
            var texture = map.Globes.TextureOf(body);
            if (texture != null)
                Gfx.Sprite(texture, at, r * 2 / 0.94f, Color.White);
            else
                Gfx.Sprite(GameTextures.Shape(Images.Shape.Disc), at, r * 2 / 0.92f, new Color(120, 125, 130));
            if (selected)
                Gfx.Sprite(GameTextures.Shape(Images.Shape.Ring), at, r * 2.6f + 8 * s, Style.Selection);
        }

        private void Ring(Vector3D centre, double radius, Color color)
        {
            const int segments = 128;
            var reference = Vector3D.Forward;
            var side = Vector3D.Cross(LocalView.Normal, reference);
            for (var i = 0; i < segments; i++)
            {
                var a0 = 2 * Math.PI * i / segments;
                var a1 = 2 * Math.PI * (i + 1) / segments;
                var p0 = centre + (reference * Math.Cos(a0) + side * Math.Sin(a0)) * radius;
                var p1 = centre + (reference * Math.Cos(a1) + side * Math.Sin(a1)) * radius;
                Vector2 sa, sb;
                if (Camera.ProjectSegment(p0, p1, out sa, out sb))
                    Gfx.Line(sa, sb, 1.2f * Gfx.Scale, color);
            }
        }

        // The sun of Space Engineers is a direction: its mark sits where that
        // direction meets the screen, or on the edge pointing to it.
        public static void DrawSun(MapCamera camera, Vector3D toSun, RectangleF area)
        {
            var s = Gfx.Scale;
            var x = Vector3D.Dot(toSun, camera.Right);
            var y = Vector3D.Dot(toSun, camera.Up);
            var z = Vector3D.Dot(toSun, camera.Forward);
            var centre = new Vector2(area.X + area.Width / 2, area.Y + area.Height / 2);
            Vector2 at;
            var onScreen = false;
            if (z > 0.05)
            {
                at = new Vector2((float)((x / (z * camera.TanHalfFov * camera.Aspect) + 1) * 0.5 * Gfx.Width),
                    (float)((1 - y / (z * camera.TanHalfFov)) * 0.5 * Gfx.Height));
                onScreen = area.Contains(at);
            }
            else
            {
                at = centre;
            }

            if (onScreen)
            {
                Gfx.Sprite(GameTextures.Sun, at, 110 * s, Color.White);
                Gfx.Text(Texts.Sun, at.X, at.Y + 30 * s, 0.62f, new Color(255, 230, 170), MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP);
                return;
            }

            // Off screen: on the edge, in the direction of the sun.
            var direction = new Vector2((float)x, (float)-y);
            if (direction.LengthSquared() < 1e-8f)
                direction = new Vector2(0, -1);
            direction.Normalize();
            var margin = 40 * s;
            var halfW = area.Width / 2 - margin;
            var halfH = area.Height / 2 - margin;
            var t = Math.Min(Math.Abs(direction.X) > 1e-6f ? halfW / Math.Abs(direction.X) : float.MaxValue,
                Math.Abs(direction.Y) > 1e-6f ? halfH / Math.Abs(direction.Y) : float.MaxValue);
            var edge = centre + direction * t;
            Gfx.Sprite(GameTextures.Sun, edge, 70 * s, Color.White);
            Gfx.Rotated(GameTextures.Shape(Images.Shape.Arrow), edge + direction * 30 * s, 16 * s, direction, new Color(255, 225, 150));
            Gfx.Text(Texts.Sun, edge.X - direction.X * 34 * s, edge.Y - direction.Y * 34 * s, 0.6f, new Color(255, 230, 170),
                MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
        }

        public override void DefaultInfo(Panel panel)
        {
            panel.Heading(Texts.ModeSystem, m_zone != null ? m_zone.Name : null);
            var planets = 0;
            var moons = 0;
            foreach (var body in World.Bodies)
            {
                if (body.IsMoon)
                    moons++;
                else
                    planets++;
            }
            panel.Line(Texts.Planets, planets.ToString());
            panel.Line(Texts.Moons, moons.ToString());
            var near = World.NearestBody(World.PlayerPosition);
            if (near != null)
                panel.Line(near.Name, Distance(Math.Max(0, Vector3D.Distance(near.Centre, World.PlayerPosition) - near.Radius)));
            panel.Line(Texts.SystemSize, Distance(Extent()));
        }

        public override void Filters(Panel panel)
        {
            MarkerFilters(panel, false);
        }

        private readonly List<Marker> m_list = new List<Marker>();

        // The sector list: bodies with their moons, then the grids.
        public override void List(Panel panel)
        {
            var player = World.PlayerPosition;
            var rows = new List<Body>();
            foreach (var body in World.Bodies)
            {
                if (!body.IsMoon)
                    rows.Add(body);
            }
            rows.Sort((a, b) => a.Centre.Length().CompareTo(b.Centre.Length()));

            m_list.Clear();
            var threshold = Settings.SpaceBlockThreshold;
            foreach (var marker in World.Markers)
            {
                if (!marker.IsSelf && Passes(marker, threshold) && marker.IsGrid)
                    m_list.Add(marker);
            }
            m_list.Sort((a, b) => Vector3D.DistanceSquared(a.Position, player).CompareTo(Vector3D.DistanceSquared(b.Position, player)));
            if (m_list.Count > 300)
                m_list.RemoveRange(300, m_list.Count - 300);

            var total = World.Bodies.Count + m_list.Count;
            panel.BeginList(Texts.SectionBodies, total);
            var disc = GameTextures.Shape(Images.Shape.Disc);
            foreach (var body in rows)
            {
                panel.Row(body, disc, Style.Orbit, body.Name, Distance(Math.Max(0, Vector3D.Distance(body.Centre, player) - body.Radius)), 0, false);
                foreach (var moon in body.Moons)
                    panel.Row(moon, disc, Gfx.Alpha(Style.Orbit, 0.7f), moon.Name, Distance(Math.Max(0, Vector3D.Distance(moon.Centre, player) - moon.Radius)), 1, false);
            }
            foreach (var marker in m_list)
            {
                var right = Distance(Vector3D.Distance(marker.Position, player));
                if (!marker.Live)
                    right = TimeAgo.Format(DateTime.UtcNow - marker.LastSeenUtc) + "  " + right;
                panel.Row(marker, Style.Icon(marker), Style.MarkerColor(marker), marker.Name, right, 0, !marker.Live);
            }
        }
    }
}
