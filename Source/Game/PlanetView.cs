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
    internal sealed class PlanetView : MapView
    {
        public Body Body;
        public bool ReturnToSystem;
        private SystemTab m_returnTab;

        // Where the camera is, seen from the centre of the planet, and which
        // way is up on screen.
        private Vector3D m_direction = Vector3D.Up;
        private Vector3D m_up = Vector3D.Forward;
        private double m_altitude = 100000;
        private double m_surface;
        private ScaleSwitch m_leave = MapScales.PlanetToNeighbourhood(60000);
        private readonly List<Marker> m_visible = new List<Marker>();
        private readonly List<Vector2> m_visibleAt = new List<Vector2>();

        public PlanetView(MapScreen map) : base(map)
        {
        }

        public override string Title
        {
            get { return Body != null ? Body.Name : Texts.KindPlanet; }
        }

        public override string Subtitle
        {
            get { return Texts.ModePlanet.Substring(3) + "  -  " + Texts.Altitude.ToLowerInvariant() + " " + Distance(m_altitude); }
        }

        public override string Help
        {
            get { return Texts.HelpPlanet; }
        }

        public override double NeededFar
        {
            get { return Camera.Distance + (Body != null ? Body.MaxRadius * 2.5 : 100000); }
        }

        public void Enter(Body body, bool fromSystem, bool continuous)
        {
            Body = body;
            ReturnToSystem = fromSystem;
            m_returnTab = Map.Tab;
            m_leave = MapScales.PlanetToNeighbourhood(body.Radius);
            m_leave.Reset(false);

            if (continuous)
            {
                var offset = Camera.Position - body.Centre;
                m_direction = Vector3D.Normalize(offset);
                m_up = Camera.Up;
                m_altitude = Math.Max(MapScales.LowestAltitude, offset.Length() - SurfaceRadius(m_direction));
            }
            else
            {
                var onIt = body.Contains(World.PlayerPosition, 3);
                var from = onIt ? World.PlayerPosition : Camera.Position;
                if ((from - body.Centre).LengthSquared() < 1)
                    from = body.Centre + World.DirectionToSun * body.Radius;
                m_direction = Vector3D.Normalize(from - body.Centre);
                m_up = onIt ? World.PlayerForward : Camera.Up;
                m_altitude = body.Radius * 1.25;
            }
            m_up = Vector3D.Reject(m_up, m_direction);
            if (m_up.LengthSquared() < 1e-8)
                m_up = Vector3D.CalculatePerpendicularVector(m_direction);
            m_up.Normalize();
            Aim();
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

        private void Aim()
        {
            m_surface = SurfaceRadius(m_direction);
            Camera.SetTarget(Body.Centre, -m_direction, m_up, m_surface + m_altitude);
        }

        public override void Update(double dt)
        {
            if (Body == null || Body.Planet == null || Body.Planet.Closed)
            {
                Map.GoLocal(LocalAnchor.Player, null, null, false, false);
                return;
            }
            var fresh = World.FindBody(Body.Id);
            if (fresh != null)
                Body = fresh;
            Aim();

            // Zoomed out past the globe: the neighbourhood, or the system the
            // dive started from.
            if (m_leave.Update(m_surface + m_altitude) && m_leave.IsUp)
            {
                if (ReturnToSystem)
                    Map.GoSystem(m_returnTab, true);
                else
                    Map.GoLocal(LocalAnchor.Body, Body, null, false, true);
            }
        }

        public override void Wheel(double notches, bool ctrl)
        {
            var highest = Body.Radius * (MapScales.PlanetLeaveRadii + 0.6);
            m_altitude = MathHelper.Clamp(ZoomSteps.Apply(m_altitude, notches), MapScales.LowestAltitude, highest);
        }

        // Dragging the ground: the point under the cursor follows it.
        public override void Rotate(Vector2 delta)
        {
            var radiansPerPixel = 2 * m_altitude * Camera.TanHalfFov / Gfx.Height / Math.Max(Body.Radius, 1);
            radiansPerPixel = Math.Min(radiansPerPixel, 0.01);
            var right = Vector3D.Cross(-m_direction, m_up);
            m_direction = Vector3D.Normalize(m_direction - right * (delta.X * radiansPerPixel) + m_up * (delta.Y * radiansPerPixel));
            m_up = Vector3D.Normalize(Vector3D.Reject(m_up, m_direction));
        }

        public override void Pan(Vector2 delta)
        {
            Rotate(delta);
        }

        public override void Move(Vector2 keys, double dt)
        {
            var pixels = 900 * dt;
            Rotate(new Vector2(-keys.X, keys.Y) * (float)pixels);
        }

        public override void Recentre()
        {
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
            return best;
        }

        // A double click goes down to what is under the cursor.
        public override void DoubleClick(object target)
        {
            var marker = target as Marker;
            if (marker == null)
                return;
            Focus(marker);
            m_altitude = Math.Max(MapScales.LowestAltitude * 4, marker.Radius * 6 + 120);
        }

        public override void Focus(object target)
        {
            var marker = target as Marker;
            if (marker == null || Body == null)
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
