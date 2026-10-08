using System;
using System.Collections.Generic;
using VRage.Utils;
using VRageMath;

namespace SirHolomap
{
    // D: where this server lies in the galaxy, past the system: the wheel
    // out of the system leads here, the wheel in leads back. Every server has a
    // fixed place, computed from its address: every player sees it at the
    // same spot. Icons too close to be told apart gather into a badge with a
    // count, which opens up as the player zooms in. The current server shines
    // with "You are here". A double click on a server asks, naming it, before
    // leaving for it.
    internal sealed class GalaxyView : MapView
    {
        // Where the view goes (zoom, and the point of the galaxy in the middle
        // of the map, -1 to 1), and where it is on its way there: the view
        // glides, it never jumps.
        private double m_zoom = MapScales.GalaxyOverview;
        private Vector2D m_centre;
        private double m_shownZoom = MapScales.GalaxyOverview;
        private Vector2D m_shownCentre;
        private double m_slowUntil;
        private SystemTab m_returnTab = SystemTab.Orrery;
        private readonly List<GalaxyServer> m_shown = new List<GalaxyServer>();
        private readonly List<IconCluster> m_clusters = new List<IconCluster>();
        private readonly List<ScreenIcon> m_icons = new List<ScreenIcon>();
        private readonly List<Vector2> m_clusterAt = new List<Vector2>();

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
            get { return Texts.HelpGalaxy + "   " + Texts.HelpWheelInSystem; }
        }

        // fromSystem: the wheel went out of the system. The view opens close
        // on the place of this server, the system just left, and draws back
        // until the whole galaxy shows. tab: the tab of the system the wheel
        // in leads back to.
        public void Enter(bool fromSystem, SystemTab tab)
        {
            m_returnTab = tab;
            var here = CurrentPlace();
            m_zoom = MapScales.GalaxyOverview;
            m_centre = Vector2D.Zero;
            if (fromSystem)
            {
                m_shownZoom = MapScales.GalaxyClosest;
                m_shownCentre = here;
                m_slowUntil = Map.Time + 1.6;
            }
            else
            {
                m_shownZoom = m_zoom;
                m_shownCentre = m_centre;
                m_slowUntil = 0;
            }
        }

        private Vector2D CurrentPlace()
        {
            foreach (var server in Map.Servers.Servers)
            {
                if (server.IsCurrent)
                    return new Vector2D(server.Place.X, server.Place.Y);
            }
            return Vector2D.Zero;
        }

        public override void Update(double dt)
        {
            var halfLife = Map.Time < m_slowUntil ? 0.3 : 0.1;
            m_shownZoom = ZoomSteps.Smooth(m_shownZoom, m_zoom, dt, halfLife);
            var k = 1 - Math.Pow(0.5, dt / halfLife);
            m_shownCentre += (m_centre - m_shownCentre) * k;
            if ((m_centre - m_shownCentre).LengthSquared() < 1e-12)
                m_shownCentre = m_centre;
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

        private float Size
        {
            get { return SizeAt(m_shownZoom); }
        }

        // The middle of the galaxy on screen.
        private Vector2 Centre
        {
            get { return ToScreen(new GalaxyPoint(0, 0)); }
        }

        private Vector2 ToScreen(GalaxyPoint point)
        {
            var half = Size / 2;
            return AreaCentre + new Vector2((float)((point.X - m_shownCentre.X) * half), (float)((point.Y - m_shownCentre.Y) * half));
        }

        private Vector2D ToGalaxy(Vector2 screen, double zoom, Vector2D centre)
        {
            var half = SizeAt(zoom) / 2.0;
            var d = screen - AreaCentre;
            return centre + new Vector2D(d.X / half, d.Y / half);
        }

        // The wheel zooms around the cursor, gliding. At the closest zoom,
        // one more notch in goes back to the system.
        public override void Wheel(double notches, bool ctrl)
        {
            if (notches > 0 && m_zoom >= MapScales.GalaxyClosest * 0.999)
            {
                Map.GoSystem(m_returnTab, true);
                return;
            }
            var mouse = Gfx.Mouse;
            var under = ToGalaxy(mouse, m_zoom, m_centre);
            m_zoom = MathHelper.Clamp(m_zoom * Math.Pow(ZoomSteps.Factor, notches), MapScales.GalaxyFarthest, MapScales.GalaxyClosest);
            var d = mouse - AreaCentre;
            var half = SizeAt(m_zoom) / 2.0;
            m_centre = under - new Vector2D(d.X / half, d.Y / half);
            m_slowUntil = 0;
        }

        public override void Rotate(Vector2 delta)
        {
            Pan(delta);
        }

        // The galaxy follows the cursor at once.
        public override void Pan(Vector2 delta)
        {
            var half = Math.Max(Size / 2.0, 1);
            var shift = new Vector2D(delta.X / half, delta.Y / half);
            m_centre -= shift;
            m_shownCentre -= shift;
        }

        public override void Move(Vector2 keys, double dt)
        {
            Pan(-new Vector2(keys.X, -keys.Y) * (float)(700 * dt));
        }

        public override void Recentre()
        {
            m_centre = CurrentPlace();
        }

        public override void Focus(object target)
        {
            var server = target as GalaxyServer;
            if (server == null)
                return;
            m_centre = new Vector2D(server.Place.X, server.Place.Y);
        }

        public override object Pick(Vector2 mouse)
        {
            var radius = 12 * Gfx.Scale;
            for (var i = 0; i < m_clusters.Count; i++)
            {
                var cluster = m_clusters[i];
                var size = cluster.Count > 1 ? radius * 1.4f : radius;
                if ((m_clusterAt[i] - mouse).Length() < size)
                    return cluster.Count == 1 ? (object)m_shown[cluster.Members[0]] : cluster;
            }
            return null;
        }

        public override void DoubleClick(object target)
        {
            var server = target as GalaxyServer;
            if (server != null)
            {
                ServerJoiner.Ask(server);
                return;
            }
            var cluster = target as IconCluster;
            if (cluster != null)
            {
                // Open the badge: glide in on it.
                var at = new Vector2((float)cluster.X, (float)cluster.Y);
                m_centre = ToGalaxy(at, m_shownZoom, m_shownCentre);
                m_zoom = Math.Min(MapScales.GalaxyClosest, m_zoom * 3);
            }
        }

        public override void DrawMap()
        {
            var s = Gfx.Scale;
            var area = Map.MapArea;
            var size = Size;
            DrawSky();
            if (GameTextures.GalaxyReady())
                Gfx.Sprite(GameTextures.Galaxy, Centre.X, Centre.Y, size, size, Color.White);
            else
                Gfx.Text(Texts.Loading, area.X + area.Width / 2, area.Y + area.Height / 2, 0.7f, Style.Dim, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);

            m_shown.Clear();
            foreach (var server in Map.Servers.Servers)
            {
                if (ServerDirectory.Passes(server, Settings))
                    m_shown.Add(server);
            }
            // Stable order: the current server, favorites, visited, the rest.
            m_shown.Sort(Compare);

            m_icons.Clear();
            for (var i = 0; i < m_shown.Count; i++)
            {
                var at = ToScreen(m_shown[i].Place);
                m_icons.Add(new ScreenIcon(i, at.X, at.Y));
            }
            m_clusters.Clear();
            m_clusters.AddRange(IconClustering.Cluster(m_icons, 16 * s));
            m_clusterAt.Clear();

            var singles = 0;
            foreach (var cluster in m_clusters)
            {
                if (cluster.Count == 1)
                    singles++;
            }

            foreach (var cluster in m_clusters)
            {
                var at = new Vector2((float)cluster.X, (float)cluster.Y);
                m_clusterAt.Add(at);
                if (!area.Contains(at))
                    continue;
                var hovered = ReferenceEquals(Map.Hovered, cluster) || ReferenceEquals(Map.Selected, cluster);
                if (cluster.Count > 1)
                {
                    var current = false;
                    foreach (var index in cluster.Members)
                        current |= m_shown[index].IsCurrent;
                    if (current)
                        Here(at);
                    Gfx.Sprite(GameTextures.Shape(Images.Shape.Disc), at, 24 * s, hovered ? Style.Accent : new Color(30, 60, 80, 230));
                    Gfx.Sprite(GameTextures.Shape(Images.Shape.Ring), at, 26 * s, Style.Accent);
                    Gfx.Text(cluster.Count.ToString(), at.X, at.Y, 0.58f, Color.White, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                    continue;
                }

                var server = m_shown[cluster.Members[0]];
                var selected = ReferenceEquals(Map.Selected, server);
                var hover = ReferenceEquals(Map.Hovered, server) || ReferenceEquals(Map.ListHovered, server);
                var color = server.IsCurrent ? Style.Accent : server.Favorite ? Style.Selection : server.Visited ? new Color(150, 200, 255) : new Color(200, 205, 215, 200);
                if (server.IsCurrent)
                    Here(at);
                if (selected || hover)
                    Gfx.Sprite(GameTextures.Shape(Images.Shape.Glow), at, 40 * s, Gfx.Alpha(selected ? Style.Selection : color, 0.5f));
                Gfx.Sprite(GameTextures.Shape(Images.Shape.Diamond), at, (server.IsCurrent || server.Favorite || server.Visited ? 13 : 9) * s, color);
                if (selected)
                    Gfx.Brackets(at, 30 * s, Style.Selection);

                var label = server.IsCurrent || server.Favorite || server.Visited || selected || hover || singles <= 60;
                if (label)
                {
                    var name = ServerNames.Name(server);
                    Gfx.Text(Gfx.Fit(name, 0.58f, 260 * s), at.X + 11 * s, at.Y - 9 * s, 0.58f, selected ? Style.Selection : Gfx.Alpha(Style.Text, 0.95f));
                }
                if (server.IsCurrent)
                    Gfx.Text(Texts.YouAreHere, at.X + 11 * s, at.Y + 7 * s, 0.56f, Style.Accent);
            }

            // A filter that hides servers always says so.
            var notice = Settings.GalaxyFilterActive
                ? string.Format(Texts.FilterActive, m_shown.Count, Map.Servers.Count)
                : Texts.AllServers;
            Gfx.Rect(area.X + 16 * s, area.Y + 14 * s, Gfx.Measure(notice, 0.62f).X + 20 * s, 28 * s, new Color(0, 0, 0, 170));
            Gfx.Text(notice, area.X + 26 * s, area.Y + 18 * s, 0.62f, Settings.GalaxyFilterActive ? Style.Selection : Style.Dim);
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
            var drift = (float)Math.Log(m_shownZoom);
            var span = (float)(Math.Min(Map.MapArea.Width, Map.MapArea.Height) * 0.46);
            var along = new Vector2((float)-m_shownCentre.X, (float)-m_shownCentre.Y) * span;
            StarLayer(GameTextures.StarsSize * 1.3f * s, along * 0.08f, new Vector2(37, 11) * drift, 0.6f);
            StarLayer(GameTextures.StarsSize * 2.1f * s, along * 0.2f, new Vector2(-23, 29) * drift, 0.85f);
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
            foreach (var server in Map.Servers.Servers)
            {
                if (server.IsCurrent)
                {
                    ServerInfo(panel, server);
                    return;
                }
            }
            var identity = Map.World.Identity;
            panel.Heading(identity != null ? identity.Name : Texts.TabGalaxy, Texts.YouAreHere);
            panel.Note(Texts.TabGalaxyHelp, Style.Dim);
        }

        public void ServerInfo(Panel panel, GalaxyServer server)
        {
            panel.Heading(ServerNames.Name(server), server.IsCurrent ? Texts.YouAreHere : null);
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

        public override void Filters(Panel panel)
        {
            panel.Checkbox(Texts.Favorites, Texts.FavoritesHelp, Settings.GalaxyFavoritesOnly, v => Settings.GalaxyFavoritesOnly = v, 0);
            panel.Checkbox(Texts.Visited, Texts.VisitedHelp, Settings.GalaxyVisitedOnly, v => Settings.GalaxyVisitedOnly = v, 1);
            panel.TextField("search", Texts.Search, Texts.SearchHelp, Settings.GalaxySearch, v => Settings.GalaxySearch = (v ?? "").Trim());
            panel.Note(Settings.GalaxyFilterActive
                ? string.Format(Texts.FilterActive, m_shown.Count, Map.Servers.Count)
                : Texts.AllServers, Settings.GalaxyFilterActive ? Style.Selection : Style.Dim);
            if (Map.Servers.Loading)
                panel.Note(Texts.Loading, Style.Dim);
            panel.Button(Texts.Refresh, Texts.RefreshHelp, () => Map.Servers.Refresh());
        }

        public override void List(Panel panel)
        {
            var cluster = Map.Selected as IconCluster;
            panel.BeginList(Texts.SectionServers, cluster != null ? cluster.Count : m_shown.Count);
            var icon = GameTextures.Shape(Images.Shape.Diamond);
            var count = 0;
            if (cluster != null)
            {
                foreach (var index in cluster.Members)
                {
                    if (index < m_shown.Count)
                        Row(panel, m_shown[index], icon);
                }
                return;
            }
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
