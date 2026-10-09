using System;
using System.Collections.Generic;
using VRage.Utils;
using VRageMath;

namespace SirHolomap
{
    // D, the galaxy: where this server lies among the others. Every server
    // has a fixed place, computed from its address: every player sees it at
    // the same spot, and the view always comes to rest on the whole galaxy,
    // so that a server is found at the same spot on the screen from one
    // opening of the map to the next. Icons too close to be told apart
    // spread around the middle of their group, a thin line to their true
    // place: none covers another, each can be hovered and clicked. The
    // current server shines with "You are here". A double click on a server
    // asks, naming it, before leaving for it. Zooming out of the system
    // leads here; zooming in leads back.
    internal sealed class GalaxyView : MapView
    {
        // Rows of the servers shown with their name, and plain dots.
        private const float RowHeight = 38;
        private const float IconHalf = 8;
        private const float DotSpacing = 11;
        private const float NameScale = 0.58f;
        private const float HereScale = 0.56f;
        private const float NameRoom = 260;

        // Up to this many servers shown, every one carries its name.
        private const int NamedWhenFewer = 60;

        private readonly GalaxyZoom m_zoom = new GalaxyZoom();
        private Vector2 m_offset;
        private double m_shownZoom = MapScales.GalaxyHomeZoom;
        private Vector2 m_shownOffset;
        private double m_enteredAt;
        private bool m_arriving;

        private readonly List<GalaxyServer> m_shown = new List<GalaxyServer>();
        private readonly List<GalaxyServer> m_named = new List<GalaxyServer>();
        private readonly List<GalaxyServer> m_plain = new List<GalaxyServer>();
        private readonly List<ScreenIcon> m_icons = new List<ScreenIcon>();
        private PlacedIcon[] m_namedAt = new PlacedIcon[0];
        private PlacedIcon[] m_plainAt = new PlacedIcon[0];
        private readonly List<RectangleF> m_namedRects = new List<RectangleF>();

        // Names cut to their room, and their widths, for the scale and the
        // language they were measured in.
        private readonly Dictionary<string, string> m_fitted = new Dictionary<string, string>();
        private readonly Dictionary<string, float> m_widths = new Dictionary<string, float>();
        private float m_measuredScale;
        private GameLanguage m_measuredLanguage;

        public GalaxyView(MapScreen map) : base(map)
        {
        }

        public override bool Uses3D
        {
            get { return false; }
        }

        public override string Title
        {
            get { return Texts.TabGalaxy; }
        }

        public override string Subtitle
        {
            get { return Map.World.Identity != null ? Map.World.Identity.Name : ""; }
        }

        public override string Help
        {
            get { return Texts.HelpGalaxy; }
        }

        // continuous: zoomed out of the system. The view starts close on the
        // current server and glides out to the whole galaxy, where it rests.
        public void Enter(bool continuous)
        {
            m_zoom.Home();
            m_offset = Vector2.Zero;
            m_enteredAt = Map.Time;
            m_arriving = continuous;
            m_shownZoom = m_zoom.Target;
            m_shownOffset = Vector2.Zero;
            if (!continuous)
                return;
            var current = CurrentServer();
            var place = current != null ? current.Place : new GalaxyPoint(0, 0);
            m_shownZoom = MapScales.GalaxyArrivalZoom;
            var half = SizeAt(m_shownZoom) / 2;
            m_shownOffset = -new Vector2((float)place.X * half, (float)place.Y * half);
        }

        private GalaxyServer CurrentServer()
        {
            foreach (var server in Map.Servers.Servers)
            {
                if (server.IsCurrent)
                    return server;
            }
            return null;
        }

        public override void Update(double dt)
        {
            // Zoomed in past the way back: the system.
            if (m_zoom.BackToSystem)
            {
                Map.GoSystem(Map.Tab, true);
                return;
            }
            var halfLife = Map.Time < m_enteredAt + 1.2 ? MapCamera.TransitionHalfLife : MapCamera.QuickHalfLife;
            m_shownZoom = ZoomSteps.Smooth(m_shownZoom, m_zoom.Target, dt, halfLife);
            var k = (float)(1 - Math.Pow(0.5, dt / Math.Max(halfLife, 1e-4)));
            m_shownOffset += (m_offset - m_shownOffset) * k;
            if ((m_offset - m_shownOffset).LengthSquared() < 0.01f)
                m_shownOffset = m_offset;
        }

        private Vector2 AreaCentre
        {
            get
            {
                var area = Map.MapArea;
                return new Vector2(area.X + area.Width / 2, area.Y + area.Height / 2);
            }
        }

        private float SizeAt(double zoom)
        {
            var area = Map.MapArea;
            return (float)(Math.Min(area.Width, area.Height) * 0.92 * zoom);
        }

        private static Vector2 ToScreen(GalaxyPoint point, Vector2 centre, float size)
        {
            var half = size / 2;
            return centre + new Vector2((float)point.X * half, (float)point.Y * half);
        }

        public override void Wheel(double notches, bool ctrl)
        {
            // Zoom around the cursor, on where the view is going.
            var mouse = Gfx.Mouse;
            var centre = AreaCentre + m_offset;
            var before = (mouse - centre) / SizeAt(m_zoom.Target);
            m_zoom.Wheel(notches);
            if (m_zoom.BackToSystem)
                return;
            var after = centre + before * SizeAt(m_zoom.Target);
            m_offset += mouse - after;
        }

        public override void Rotate(Vector2 delta)
        {
            Pan(delta);
        }

        public override void Pan(Vector2 delta)
        {
            m_offset += delta;
            m_shownOffset += delta;
        }

        public override void Move(Vector2 keys, double dt)
        {
            var step = new Vector2(keys.X, -keys.Y) * (float)(700 * dt * Gfx.Scale);
            m_offset -= step;
            m_shownOffset -= step;
        }

        // Back to the whole galaxy, as it rests.
        public override void Recentre()
        {
            m_zoom.Home();
            m_offset = Vector2.Zero;
        }

        public override void Focus(object target)
        {
            var server = target as GalaxyServer;
            if (server == null)
                return;
            var at = ToScreen(server.Place, AreaCentre + m_offset, SizeAt(m_zoom.Target));
            m_offset += AreaCentre - at;
        }

        public override object Pick(Vector2 mouse)
        {
            // The named rows are drawn over the dots, the last on top.
            for (var i = Math.Min(m_namedRects.Count, m_named.Count) - 1; i >= 0; i--)
            {
                if (m_namedRects[i].Contains(mouse))
                    return m_named[i];
            }
            object best = null;
            var bestDistance = DotSpacing * 0.5f * Gfx.Scale + 2;
            for (var i = 0; i < m_plainAt.Length && i < m_plain.Count; i++)
            {
                var d = (new Vector2((float)m_plainAt[i].X, (float)m_plainAt[i].Y) - mouse).Length();
                if (d < bestDistance)
                {
                    best = m_plain[i];
                    bestDistance = d;
                }
            }
            return best;
        }

        public override void DoubleClick(object target)
        {
            var server = target as GalaxyServer;
            if (server != null)
                ServerJoiner.Ask(server);
        }

        // The names are measured once per scale and language.
        private string Fitted(string name, out float width)
        {
            var s = Gfx.Scale;
            if (s != m_measuredScale || Localization.Current != m_measuredLanguage || m_fitted.Count > 4000)
            {
                m_fitted.Clear();
                m_widths.Clear();
                m_measuredScale = s;
                m_measuredLanguage = Localization.Current;
            }
            string fitted;
            if (!m_fitted.TryGetValue(name, out fitted))
            {
                fitted = Gfx.Fit(name, NameScale, NameRoom * s);
                m_fitted[name] = fitted;
                m_widths[name] = Math.Max(Gfx.Measure(fitted, NameScale).X, Gfx.Measure(Texts.YouAreHere, HereScale).X);
            }
            width = m_widths[name];
            return fitted;
        }

        private float Fade
        {
            get { return m_arriving ? (float)MathHelper.Clamp((Map.Time - m_enteredAt) / 0.45, 0, 1) : 1f; }
        }

        public override void DrawMap()
        {
            var s = Gfx.Scale;
            var area = Map.MapArea;
            var size = SizeAt(m_shownZoom);
            var centre = AreaCentre + m_shownOffset;
            DrawSky();
            if (GameTextures.GalaxyReady())
                Gfx.Sprite(GameTextures.Galaxy, centre.X, centre.Y, size, size, Gfx.Alpha(Color.White, Fade));
            else
                Gfx.Text(Texts.Loading, area.X + area.Width / 2, area.Y + area.Height / 2, 0.7f, Style.Dim, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);

            m_shown.Clear();
            foreach (var server in Map.Servers.Servers)
            {
                if (ServerDirectory.Passes(server, Settings))
                    m_shown.Add(server);
            }
            // The order of the list: the current server, favorites, visited,
            // the rest. The places on the map never depend on it.
            m_shown.Sort(Compare);

            // Servers with their name, and plain dots.
            m_named.Clear();
            m_plain.Clear();
            var everyName = m_shown.Count <= NamedWhenFewer;
            foreach (var server in m_shown)
            {
                if (everyName || server.IsCurrent || server.Favorite || server.Visited)
                    m_named.Add(server);
                else
                    m_plain.Add(server);
            }

            m_icons.Clear();
            for (var i = 0; i < m_plain.Count; i++)
            {
                var at = ToScreen(m_plain[i].Place, centre, size);
                m_icons.Add(new ScreenIcon(i, m_plain[i].Key, at.X, at.Y));
            }
            m_plainAt = GalaxyLayout.Dots(m_icons, DotSpacing * s);

            // Every named row has the same height and the room of its name,
            // whichever server is the current one: the current server never
            // pushes the others around.
            m_icons.Clear();
            for (var i = 0; i < m_named.Count; i++)
            {
                float width;
                Fitted(ServerNames.Name(m_named[i]), out width);
                var at = ToScreen(m_named[i].Place, centre, size);
                m_icons.Add(new ScreenIcon(i, m_named[i].Key, at.X, at.Y, (IconHalf + 11 + 6) * s + width, RowHeight * s));
            }
            m_namedAt = GalaxyLayout.Rows(m_icons, IconHalf * s, 4 * s);

            Vector2 floatingAt;
            var floating = DrawPlain(area, out floatingAt);
            DrawNamed(area);
            if (floating != null)
            {
                // The name of a hovered or selected dot, over everything.
                float width;
                var name = Fitted(ServerNames.Name(floating), out width);
                Gfx.Rect(floatingAt.X + 8 * s, floatingAt.Y - 11 * s, width + 9 * s, 22 * s, new Color(0, 0, 0, 200));
                Gfx.Text(name, floatingAt.X + 11 * s, floatingAt.Y - 10 * s, NameScale, Gfx.Alpha(Style.Text, 0.95f));
            }

            // A filter that hides servers always says so, and names itself.
            var active = Settings.GalaxyFilterActive;
            var notice = FilterNotice();
            Gfx.Rect(area.X + 16 * s, area.Y + 14 * s, Gfx.Measure(notice, 0.62f).X + 20 * s, 28 * s, new Color(0, 0, 0, 170));
            if (active)
                Gfx.Rect(area.X + 16 * s, area.Y + 14 * s, 3 * s, 28 * s, Style.Selection);
            Gfx.Text(notice, area.X + 26 * s, area.Y + 18 * s, 0.62f, active ? Style.Selection : Style.Dim);
        }

        // Plain dots first, beneath the named rows. Returns the hovered or
        // selected dot, whose name shows over everything.
        private GalaxyServer DrawPlain(RectangleF area, out Vector2 floatingAt)
        {
            var s = Gfx.Scale;
            GalaxyServer floating = null;
            floatingAt = Vector2.Zero;
            var color = new Color(200, 205, 215, 200);
            for (var i = 0; i < m_plain.Count && i < m_plainAt.Length; i++)
            {
                var server = m_plain[i];
                var at = new Vector2((float)m_plainAt[i].X, (float)m_plainAt[i].Y);
                if (!area.Contains(at))
                    continue;
                var selected = ReferenceEquals(Map.Selected, server);
                var hover = ReferenceEquals(Map.Hovered, server) || ReferenceEquals(Map.ListHovered, server);
                if (m_plainAt[i].Moved)
                    Leader(m_plainAt[i], 0.35f);
                if (selected || hover)
                {
                    Gfx.Sprite(GameTextures.Shape(Images.Shape.Glow), at, 34 * s, Gfx.Alpha(selected ? Style.Selection : color, 0.5f));
                    floating = server;
                    floatingAt = at;
                }
                Gfx.Sprite(GameTextures.Shape(Images.Shape.Diamond), at, 9 * s, selected ? Style.Selection : color);
                if (selected)
                    Gfx.Brackets(at, 24 * s, Style.Selection);
            }
            return floating;
        }

        // Named rows, each in its own place in its column, its name on a
        // dark band so that it reads over the stars and the dots.
        private void DrawNamed(RectangleF area)
        {
            var s = Gfx.Scale;
            m_namedRects.Clear();
            for (var i = 0; i < m_named.Count && i < m_namedAt.Length; i++)
            {
                var server = m_named[i];
                var placed = m_namedAt[i];
                var at = new Vector2((float)placed.X, (float)placed.Y);
                float width;
                var name = Fitted(ServerNames.Name(server), out width);
                m_namedRects.Add(new RectangleF(at.X - (IconHalf + 2) * s, at.Y - RowHeight * s / 2,
                    (IconHalf + 2 + 11 + 6) * s + width, RowHeight * s));
                if (!area.Contains(at))
                    continue;
                var selected = ReferenceEquals(Map.Selected, server);
                var hover = ReferenceEquals(Map.Hovered, server) || ReferenceEquals(Map.ListHovered, server);
                var color = server.IsCurrent ? Style.Accent : server.Favorite ? Style.Selection : server.Visited ? new Color(150, 200, 255) : new Color(200, 205, 215, 220);
                if (placed.Moved)
                    Leader(placed, 0.6f);
                if (server.IsCurrent)
                    Here(at);
                if (selected || hover)
                    Gfx.Sprite(GameTextures.Shape(Images.Shape.Glow), at, 40 * s, Gfx.Alpha(selected ? Style.Selection : color, 0.5f));
                Gfx.Rect(at.X + 8 * s, at.Y - 18 * s, width + 9 * s, 36 * s,
                    hover ? new Color(20, 50, 70, 190) : new Color(0, 0, 0, server.IsCurrent ? 170 : 130));
                Gfx.Sprite(GameTextures.Shape(Images.Shape.Diamond), at, (server.IsCurrent ? 17 : 13) * s, color);
                if (selected)
                    Gfx.Brackets(at, 30 * s, Style.Selection);
                Gfx.Text(name, at.X + 11 * s, at.Y - 17 * s, NameScale,
                    selected ? Style.Selection : server.IsCurrent ? Color.White : Gfx.Alpha(Style.Text, 0.95f));
                if (server.IsCurrent)
                    Gfx.Text(Texts.YouAreHere, at.X + 11 * s, at.Y + 1 * s, HereScale, Style.Accent);
            }
        }

        // From where an icon is drawn to its true place in the galaxy.
        private static void Leader(PlacedIcon placed, float alpha)
        {
            var s = Gfx.Scale;
            var from = new Vector2((float)placed.TrueX, (float)placed.TrueY);
            var to = new Vector2((float)placed.X, (float)placed.Y);
            Gfx.Line(from, to, 1.2f * s, Gfx.Alpha(Style.Accent, alpha));
            Gfx.Sprite(GameTextures.Shape(Images.Shape.Disc), from, 4 * s, Gfx.Alpha(Style.Accent, alpha));
        }

        // The filter in force, by name, with how many servers it lets through.
        private string FilterNotice()
        {
            if (!Settings.GalaxyFilterActive)
                return Texts.AllServers;
            var names = new List<string>();
            if (Settings.GalaxyFavoritesOnly)
                names.Add(Texts.Favorites);
            if (Settings.GalaxyVisitedOnly)
                names.Add(Texts.Visited);
            if (!string.IsNullOrEmpty(Settings.GalaxySearch))
                names.Add(Texts.Search + ": " + Settings.GalaxySearch);
            return string.Format(Texts.FilterActive, m_shown.Count, Map.Servers.Count) + "  -  " + string.Join(", ", names.ToArray());
        }

        // The night sky the galaxy lies on: black, filled with stars to the
        // edges of the view however far the galaxy is zoomed out. Two layers
        // of the same tile at two sizes, drifting a little with the map so
        // that the sky has depth; the galaxy picture fades into it.
        private void DrawSky()
        {
            var s = Gfx.Scale;
            var top = Map.TopBarHeight;
            Gfx.Rect(0, top, Gfx.Width, Gfx.Height - top, Color.Black);
            var drift = (float)Math.Log(Math.Max(m_shownZoom, 1e-3));
            StarLayer(GameTextures.StarsSize * 0.75f * s, m_shownOffset * 0.08f, new Vector2(37, 11) * drift, 0.55f);
            StarLayer(GameTextures.StarsSize * 1.1f * s, m_shownOffset * 0.2f, new Vector2(-23, 29) * drift, 0.9f);
        }

        private static void StarLayer(float tile, Vector2 shift, Vector2 drift, float alpha)
        {
            var top = MapScreen.Current != null ? MapScreen.Current.TopBarHeight : 0;
            var startX = Wrap(shift.X + drift.X, tile) - tile;
            var startY = top + Wrap(shift.Y + drift.Y, tile) - tile;
            var color = Gfx.Alpha(Color.White, alpha);
            for (var y = startY; y < Gfx.Height; y += tile)
            {
                for (var x = startX; x < Gfx.Width; x += tile)
                    Gfx.Sprite(GameTextures.Stars, x + tile / 2, y + tile / 2, tile, tile, color);
            }
        }

        private static float Wrap(float value, float period)
        {
            var r = value % period;
            return r < 0 ? r + period : r;
        }

        // The current server stands out: a pulsing glow and ring.
        private void Here(Vector2 at)
        {
            var s = Gfx.Scale;
            var pulse = 0.5f + 0.5f * (float)Math.Sin(Map.Time * 3);
            Gfx.Sprite(GameTextures.Shape(Images.Shape.Glow), at, (60 + 14 * pulse) * s, Gfx.Alpha(Style.Accent, 0.6f));
            Gfx.Sprite(GameTextures.Shape(Images.Shape.Ring), at, (30 + 6 * pulse) * s, Gfx.Alpha(Style.Accent, 0.9f));
        }

        private static int Rank(GalaxyServer server)
        {
            if (server.IsCurrent)
                return 0;
            if (server.Favorite)
                return 1;
            if (server.Visited)
                return 2;
            return 3;
        }

        private static int Compare(GalaxyServer a, GalaxyServer b)
        {
            var rank = Rank(a).CompareTo(Rank(b));
            if (rank != 0)
                return rank;
            if (a.Visited && b.Visited)
                return b.Visit.LastVisitUtc.CompareTo(a.Visit.LastVisitUtc);
            return string.Compare(a.Address, b.Address, StringComparison.Ordinal);
        }

        public override void DefaultInfo(Panel panel)
        {
            var current = CurrentServer();
            if (current != null)
            {
                ServerInfo(panel, current);
                return;
            }
            var identity = Map.World.Identity;
            panel.Heading(identity != null ? identity.Name : Texts.TabGalaxy, Texts.YouAreHere);
            panel.Note(Texts.TabGalaxyHelp, Style.Dim);
        }

        public void ServerInfo(Panel panel, GalaxyServer server)
        {
            panel.Heading(ServerNames.Name(server), server.IsCurrent ? Texts.YouAreHere : null);
            if (!string.IsNullOrEmpty(server.ConnectionString))
                panel.Line(Texts.Address, server.ConnectionString);
            var item = server.Item;
            if (item != null)
            {
                panel.Line(Texts.Players, item.Players + " / " + item.MaxPlayers);
                if (item.Ping > 0)
                    panel.Line(Texts.Ping, item.Ping + " ms");
            }
            var status = new List<string>();
            if (server.IsCurrent)
                status.Add(Texts.YouAreHere);
            if (server.Favorite)
                status.Add(Texts.Favorites);
            if (server.Visited)
                status.Add(Texts.Visited);
            if (status.Count > 0)
                panel.Line(Texts.Status, string.Join(", ", status.ToArray()));
            panel.Line(Texts.LastVisit, server.Visited
                ? string.Format(Texts.LastVisitAgo, TimeAgo.Format(DateTime.UtcNow - server.Visit.LastVisitUtc))
                : Texts.NeverVisited, server.Visited ? Style.Text : Style.Dim);
        }

        // The filters, kept between game launches and for every server. The
        // one in force is named under the boxes, and in the corner of the map.
        public override void Filters(Panel panel)
        {
            panel.Checkbox(Texts.Favorites, Texts.FavoritesHelp, Settings.GalaxyFavoritesOnly, v => Settings.GalaxyFavoritesOnly = v, 0);
            panel.Checkbox(Texts.Visited, Texts.VisitedHelp, Settings.GalaxyVisitedOnly, v => Settings.GalaxyVisitedOnly = v, 1);
            panel.TextField("search", Texts.Search, Texts.SearchHelp, Settings.GalaxySearch, v => Settings.GalaxySearch = (v ?? "").Trim());
            panel.Note(FilterNotice(), Settings.GalaxyFilterActive ? Style.Selection : Style.Dim);
            if (Map.Servers.Loading)
                panel.Note(Texts.Loading, Style.Dim);
            panel.Button(Texts.Refresh, Texts.RefreshHelp, () => Map.Servers.Refresh());
        }

        public override void List(Panel panel)
        {
            panel.BeginList(Texts.SectionServers, m_shown.Count);
            var icon = GameTextures.Shape(Images.Shape.Diamond);
            var count = 0;
            foreach (var server in m_shown)
            {
                if (count++ > 400)
                    break;
                Row(panel, server, icon);
            }
            if (m_shown.Count == 0)
                panel.Empty(Map.Servers.Loading ? Texts.Loading : Texts.None);
        }

        private static void Row(Panel panel, GalaxyServer server, string icon)
        {
            var color = server.IsCurrent ? Style.Accent : server.Favorite ? Style.Selection : server.Visited ? new Color(150, 200, 255) : Style.Dim;
            var right = server.IsCurrent ? Texts.YouAreHere
                : server.Visited ? TimeAgo.Format(DateTime.UtcNow - server.Visit.LastVisitUtc)
                : server.Item != null ? server.Item.Players + "/" + server.Item.MaxPlayers : "";
            panel.Row(server, icon, color, ServerNames.Name(server), right, 0, false);
        }
    }

    internal static class ServerNames
    {
        public static string Name(GalaxyServer server)
        {
            return string.IsNullOrEmpty(server.Name) ? server.Address : server.Name;
        }
    }
}
