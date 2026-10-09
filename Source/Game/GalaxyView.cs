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

        // What the view shows and where it glides to; the zoom grows and
        // shrinks around the cursor, softly, at every notch.
        private readonly GalaxyCamera m_camera = new GalaxyCamera();
        private double m_enteredAt;
        private bool m_arriving;
        // Zoomed in past the way back: the galaxy fades as the view glides on
        // to the system, from this zoom.
        private bool m_leaving;
        private double m_leaveFrom;

        // The sky: a far layer of small dim stars and a near one of a few
        // larger ones, drifting at their own pace for depth.
        private static readonly SkyStar[] FarSky = StarSky.Layer(StarSky.FarStars, 1);
        private static readonly SkyStar[] NearSky = StarSky.Layer(StarSky.NearStars, 2);

        // How far each icon is drawn from its true place, as shown: when the
        // zoom gathers or spreads a group, its icons glide to their new
        // places instead of jumping there.
        private Dictionary<ulong, Vector2> m_shifts = new Dictionary<ulong, Vector2>();
        private Dictionary<ulong, Vector2> m_nextShifts = new Dictionary<ulong, Vector2>();
        private double m_lastDraw;

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
            m_enteredAt = Map.Time;
            m_arriving = continuous;
            m_leaving = false;
            m_shifts.Clear();
            m_camera.Rest();
            if (!continuous)
                return;
            var current = CurrentServer();
            var place = current != null ? current.Place : new GalaxyPoint(0, 0);
            m_camera.Arrive(MapScales.GalaxyArrivalZoom, place.X, place.Y);
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
            var halfLife = Map.Time < m_enteredAt + 1.2 ? MapCamera.TransitionHalfLife : GalaxyCamera.HalfLife;
            m_camera.Update(dt, halfLife);
            // Zoomed in past the way back: the view glides on, the galaxy
            // fading, then the system takes over. A notch back out before
            // that keeps the galaxy.
            if (!m_camera.BackToSystem)
            {
                m_leaving = false;
                return;
            }
            if (!m_leaving)
            {
                m_leaving = true;
                m_leaveFrom = m_camera.ShownZoom;
            }
            if (m_camera.ShownZoom >= GalaxyCamera.LeaveAt)
                Map.GoSystem(Map.Tab, true);
        }

        // The galaxy as it shows: fading in when it arrives from the system,
        // fading out when it leaves for it.
        private float Shown
        {
            get { return Fade * (m_leaving ? (float)m_camera.LeaveFade(m_leaveFrom) : 1f); }
        }

        private Vector2 AreaCentre
        {
            get
            {
                var area = Map.MapArea;
                return new Vector2(area.X + area.Width / 2, area.Y + area.Height / 2);
            }
        }

        // Pixels for half the galaxy at zoom 1.
        private float HalfSize
        {
            get
            {
                var area = Map.MapArea;
                return (float)Math.Max(Math.Min(area.Width, area.Height) * 0.46, 1);
            }
        }

        // Where a galaxy point shows now.
        private Vector2 ToScreen(GalaxyPoint point)
        {
            double u, v;
            m_camera.ToScreen(point.X, point.Y, out u, out v);
            var half = HalfSize;
            return AreaCentre + new Vector2((float)(u * half), (float)(v * half));
        }

        public override void Wheel(double notches, bool ctrl)
        {
            // Zoom around the cursor: the point under it stays there.
            var at = (Gfx.Mouse - AreaCentre) / HalfSize;
            m_camera.Wheel(notches, at.X, at.Y);
        }

        public override void Rotate(Vector2 delta)
        {
            Pan(delta);
        }

        public override void Pan(Vector2 delta)
        {
            var half = HalfSize;
            m_camera.Pan(delta.X / half, delta.Y / half);
        }

        public override void Move(Vector2 keys, double dt)
        {
            var step = new Vector2(keys.X, -keys.Y) * (float)(700 * dt * Gfx.Scale);
            Pan(-step);
        }

        // Back to the whole galaxy, as it rests.
        public override void Recentre()
        {
            m_camera.Home();
        }

        public override void Focus(object target)
        {
            var server = target as GalaxyServer;
            if (server == null)
                return;
            m_camera.CentreOn(server.Place.X, server.Place.Y);
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
            var size = HalfSize * 2 * (float)m_camera.ShownZoom;
            var centre = ToScreen(new GalaxyPoint(0, 0));
            DrawSky();
            if (GameTextures.GalaxyReady())
                Gfx.Sprite(GameTextures.Galaxy, centre.X, centre.Y, size, size, Gfx.Alpha(Color.White, Shown));
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

            var glide = ShiftGlide();
            m_icons.Clear();
            for (var i = 0; i < m_plain.Count; i++)
            {
                var at = ToScreen(m_plain[i].Place);
                m_icons.Add(new ScreenIcon(i, m_plain[i].Key, at.X, at.Y));
            }
            m_plainAt = GalaxyLayout.Dots(m_icons, DotSpacing * s);
            Soften(m_plainAt, m_plain, glide);

            // Every named row has the same height and the room of its name,
            // whichever server is the current one: the current server never
            // pushes the others around.
            m_icons.Clear();
            for (var i = 0; i < m_named.Count; i++)
            {
                float width;
                Fitted(ServerNames.Name(m_named[i]), out width);
                var at = ToScreen(m_named[i].Place);
                m_icons.Add(new ScreenIcon(i, m_named[i].Key, at.X, at.Y, (IconHalf + 11 + 6) * s + width, RowHeight * s));
            }
            m_namedAt = GalaxyLayout.Rows(m_icons, IconHalf * s, 4 * s);
            Soften(m_namedAt, m_named, glide);
            SwapShifts();

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

        // How much of the way the shown shifts of the icons go this frame.
        private float ShiftGlide()
        {
            var dt = MathHelper.Clamp(Map.Time - m_lastDraw, 0, 0.1);
            m_lastDraw = Map.Time;
            return (float)(1 - Math.Pow(0.5, dt / GalaxyCamera.HalfLife));
        }

        // The icons glide from where they were drawn to where the layout
        // puts them now, around their true places that follow the zoom.
        private void Soften(PlacedIcon[] placed, List<GalaxyServer> servers, float glide)
        {
            for (var i = 0; i < placed.Length && i < servers.Count; i++)
            {
                var key = servers[i].Key;
                var target = new Vector2((float)(placed[i].X - placed[i].TrueX), (float)(placed[i].Y - placed[i].TrueY));
                Vector2 shift;
                if (m_shifts.TryGetValue(key, out shift))
                {
                    shift += (target - shift) * glide;
                    if ((target - shift).LengthSquared() < 0.04f)
                        shift = target;
                }
                else
                {
                    shift = target;
                }
                m_nextShifts[key] = shift;
                placed[i].X = placed[i].TrueX + shift.X;
                placed[i].Y = placed[i].TrueY + shift.Y;
            }
        }

        // Only the icons drawn this frame are remembered.
        private void SwapShifts()
        {
            var old = m_shifts;
            m_shifts = m_nextShifts;
            m_nextShifts = old;
            m_nextShifts.Clear();
        }

        // The night sky the galaxy lies on: black, with stars to the edges of
        // the view however far the galaxy is zoomed out. Few of them, soft
        // and round with a light halo, in gentle colours, in two layers that
        // drift and spread at their own pace as the galaxy moves and zooms,
        // so that the sky has depth and the galaxy stays what the eye reads.
        private void DrawSky()
        {
            var s = Gfx.Scale;
            var top = Map.TopBarHeight;
            Gfx.Rect(0, top, Gfx.Width, Gfx.Height - top, Color.Black);
            StarLayer(FarSky, 900 * s, 0.1, 0.15, 7 * s, 0.85f);
            StarLayer(NearSky, 1150 * s, 0.25, 0.35, 12 * s, 1f);
        }

        // One layer: a tile of stars repeated over the view. parallax: how
        // much of the galaxy's motion it follows; spread: how much of its
        // zoom (as a power).
        private void StarLayer(SkyStar[] stars, float tile, double parallax, double spread, float starSize, float alpha)
        {
            var top = Map.TopBarHeight;
            var centre = AreaCentre;
            var half = HalfSize;
            var zoom = (float)Math.Pow(Math.Max(m_camera.ShownZoom, 1e-3), spread);
            var layerX = (float)(m_camera.ShownX * half * parallax);
            var layerY = (float)(m_camera.ShownY * half * parallax);
            var grow = (float)Math.Sqrt(Math.Min(zoom, 1.6f));
            var margin = starSize * 2.5f;
            // The part of the layer in view, in its own pixels.
            var minX = layerX + (0 - margin - centre.X) / zoom;
            var maxX = layerX + (Gfx.Width + margin - centre.X) / zoom;
            var minY = layerY + (top - margin - centre.Y) / zoom;
            var maxY = layerY + (Gfx.Height + margin - centre.Y) / zoom;
            var firstX = (int)Math.Floor(minX / tile);
            var lastX = (int)Math.Floor(maxX / tile);
            var firstY = (int)Math.Floor(minY / tile);
            var lastY = (int)Math.Floor(maxY / tile);
            if ((lastX - firstX + 1) * (lastY - firstY + 1) > 64)
                return;
            var texture = GameTextures.Shape(Images.Shape.Star);
            for (var ty = firstY; ty <= lastY; ty++)
            {
                for (var tx = firstX; tx <= lastX; tx++)
                {
                    foreach (var star in stars)
                    {
                        var x = centre.X + ((tx + (float)star.X) * tile - layerX) * zoom;
                        var y = centre.Y + ((ty + (float)star.Y) * tile - layerY) * zoom;
                        var size = starSize * (float)star.Size * grow;
                        if (y < top - size || y > Gfx.Height + size || x < -size || x > Gfx.Width + size)
                            continue;
                        var color = new Color(star.R, star.G, star.B, (byte)(255 * MathHelper.Clamp((float)star.Alpha * alpha, 0, 1)));
                        Gfx.Sprite(texture, x, y, size, size, color);
                    }
                }
            }
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
