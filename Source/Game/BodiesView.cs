using System;
using System.Collections.Generic;
using VRage.Utils;
using VRageMath;

namespace SirHolomap
{
    // C, first tab: the planets laid flat in a row, by distance to the centre
    // of the world, each with its moons below it, next to the sun. A double
    // click opens the globe of a body (live, with memories greyed when it is
    // out of reach); zooming out of that globe comes straight back here.
    internal sealed class BodiesView : MapView
    {
        private readonly List<Body> m_drawn = new List<Body>();
        private readonly List<Vector2> m_drawnAt = new List<Vector2>();
        private readonly List<float> m_drawnRadius = new List<float>();
        private float m_scroll;
        private float m_contentWidth;

        public BodiesView(MapScreen map) : base(map)
        {
        }

        public override bool Uses3D
        {
            get { return false; }
        }

        public override string Title
        {
            get { return Texts.ModeSystem; }
        }

        public override string Subtitle
        {
            get { return Texts.TabBodies; }
        }

        public override string Help
        {
            get { return Texts.HelpBodies; }
        }

        public override void Update(double dt)
        {
        }

        public override void Rotate(Vector2 delta)
        {
            Pan(delta);
        }

        public override void Pan(Vector2 delta)
        {
            var area = Map.MapArea;
            m_scroll = MathHelper.Clamp(m_scroll - delta.X, 0, Math.Max(0, m_contentWidth - area.Width));
        }

        public override void Move(Vector2 keys, double dt)
        {
            Pan(new Vector2((float)(-keys.X * 900 * dt), 0));
        }

        // The wheel zooms into the body under the cursor: its globe.
        public override void Wheel(double notches, bool ctrl)
        {
            if (notches <= 0)
                return;
            var body = Pick(Gfx.Mouse) as Body;
            if (body != null)
                Map.GoPlanet(body, true, false, null);
        }

        public override void DoubleClick(object target)
        {
            var body = target as Body;
            if (body != null)
                Map.GoPlanet(body, true, false, null);
        }

        public override object Pick(Vector2 mouse)
        {
            for (var i = 0; i < m_drawn.Count; i++)
            {
                if ((m_drawnAt[i] - mouse).Length() < m_drawnRadius[i] + 6 * Gfx.Scale)
                    return m_drawn[i];
            }
            return null;
        }

        private List<Body> Planets()
        {
            var planets = new List<Body>();
            foreach (var body in World.Bodies)
            {
                if (!body.IsMoon)
                    planets.Add(body);
            }
            planets.Sort((a, b) => a.Centre.Length().CompareTo(b.Centre.Length()));
            return planets;
        }

        public override void DrawMap()
        {
            m_drawn.Clear();
            m_drawnAt.Clear();
            m_drawnRadius.Clear();
            var s = Gfx.Scale;
            var area = Map.MapArea;
            var middle = area.Y + area.Height * 0.38f;

            // Faint stars behind.
            if (GameTextures.GalaxyReady())
                Gfx.Sprite(GameTextures.Galaxy, area.X + area.Width * 0.7f, area.Y + area.Height * 0.5f, area.Height * 1.6f, area.Height * 1.6f,
                    new Color(255, 255, 255, 28));

            // The sun, half out of the left edge.
            var sunSize = area.Height * 1.1f;
            Gfx.Sprite(GameTextures.Sun, area.X - sunSize * 0.18f - m_scroll, middle, sunSize, sunSize, Color.White);
            Gfx.Text(Texts.Sun, area.X + 30 * s - m_scroll, area.Y + area.Height - 70 * s, 0.7f, new Color(255, 230, 170));

            var planets = Planets();
            if (planets.Count == 0)
                return;
            var largest = 1.0;
            foreach (var body in World.Bodies)
                largest = Math.Max(largest, body.Radius);

            var left = area.X + sunSize * 0.42f;
            var spacing = Math.Max(150 * s, (area.Width - (left - area.X) - 80 * s) / Math.Max(planets.Count, 1));
            m_contentWidth = left - area.X + spacing * planets.Count + 80 * s;

            var player = World.PlanetUnderPlayer() ?? World.NearestBody(World.PlayerPosition);
            for (var i = 0; i < planets.Count; i++)
            {
                var planet = planets[i];
                var x = left + spacing * (i + 0.5f) - m_scroll;
                var r = Radius(planet, largest);
                var at = new Vector2(x, middle);
                DrawBody(planet, at, r, player);
                Gfx.Text(Distance(planet.Centre.Length()), x, middle + r + 30 * s, 0.52f, Style.Dim, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP);

                // Moons in a column below their planet.
                var y = middle + r + 64 * s;
                foreach (var moon in planet.Moons)
                {
                    var mr = Radius(moon, largest);
                    y += mr;
                    DrawBody(moon, new Vector2(x, y), mr, player);
                    y += mr + 34 * s;
                }
            }
        }

        private static float Radius(Body body, double largest)
        {
            return (float)((10 + 52 * Math.Pow(body.Radius / largest, 0.7)) * Gfx.Scale);
        }

        private void DrawBody(Body body, Vector2 at, float r, Body player)
        {
            var s = Gfx.Scale;
            var hovered = ReferenceEquals(Map.Hovered, body) || ReferenceEquals(Map.ListHovered, body);
            var selected = ReferenceEquals(Map.Selected, body);
            OrreryView.DrawGlobe(Map, body, at, r, hovered, selected);
            Gfx.Text(body.Name, at.X, at.Y + r + 8 * s, body.IsMoon ? 0.58f : 0.72f, selected ? Style.Selection : Style.Text,
                MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP);
            if (ReferenceEquals(body, player))
            {
                Gfx.Sprite(GameTextures.Shape(Images.Shape.Arrow), at.X, at.Y - r - 16 * s, 14 * s, 14 * s, Style.Accent);
                Gfx.Text(Texts.You, at.X, at.Y - r - 26 * s, 0.56f, Style.Accent, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM);
            }
            m_drawn.Add(body);
            m_drawnAt.Add(at);
            m_drawnRadius.Add(r);
        }

        public override void DefaultInfo(Panel panel)
        {
            panel.Heading(Texts.TabBodies, Texts.TabBodiesHelp);
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
        }

        public override void Filters(Panel panel)
        {
            panel.Note(Texts.TabBodiesHelp, Style.Dim);
        }

        public override void List(Panel panel)
        {
            var planets = Planets();
            panel.BeginList(Texts.SectionBodies, World.Bodies.Count);
            var disc = GameTextures.Shape(Images.Shape.Disc);
            foreach (var planet in planets)
            {
                panel.Row(planet, disc, Style.Orbit, planet.Name, Distance(planet.Centre.Length()), 0, false);
                foreach (var moon in planet.Moons)
                    panel.Row(moon, disc, Gfx.Alpha(Style.Orbit, 0.7f), moon.Name, Distance(Vector3D.Distance(moon.Centre, planet.Centre)), 1, false);
            }
        }
    }
}
