using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Sandbox.Game;
using Sandbox.Game.Gui;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace SirHolomap
{
    // The map, full screen. While it is open it holds the keyboard and the
    // mouse: the game's controls never reach the character or the ship, and
    // the character is told every frame to stand still. The game keeps
    // drawing its world behind, seen from the map's camera.
    internal sealed class MapScreen : MyGuiScreenBase
    {
        public static MapScreen Current { get; private set; }

        public readonly MapWorld World;
        public readonly MapSettings Settings;
        public readonly MapCamera Camera = new MapCamera();
        public readonly PlanetGlobes Globes;
        public readonly ServerDirectory Servers;

        public MapMode Mode { get; private set; }
        public SystemTab Tab { get; private set; }

        public readonly PlanetView PlanetMode;
        public readonly LocalView LocalMode;
        public readonly OrreryView OrreryMode;
        public readonly BodiesView BodiesMode;
        public readonly GalaxyView GalaxyMode;

        public object Selected;
        public object Hovered;
        public object ListHovered;
        public double Time;

        private readonly HolomapPlugin m_plugin;
        private readonly Panel m_panel;
        private readonly ClickTracker m_clicks = new ClickTracker();
        private readonly Stopwatch m_clock = Stopwatch.StartNew();
        private readonly List<Hit> m_hits = new List<Hit>();
        private readonly List<Hit> m_lastHits = new List<Hit>();
        private double m_lastTime;
        private double m_lastInput;
        private double m_transitionUntil;
        private bool m_hudWasMinimal;
        private bool m_closed;

        private Vector2 m_lastMouse;
        private Vector2 m_pressAt;
        private bool m_leftOnMap;
        private bool m_middleOnMap;
        private bool m_dragging;
        private string m_help;

        private RectangleF m_listArea;
        private int m_listTotal;
        private int m_listShown;
        public int ListScroll;

        public string FocusedField { get; private set; }
        public string FieldText = "";
        private Action<string> m_fieldCommit;
        private bool m_fieldDigits;
        private bool m_settingsDirty;
        private double m_settingsSaveAt;

        private struct Hit
        {
            public RectangleF Rect;
            public Action Click;
            public object Item;
            public string Help;
        }

        public MapScreen(HolomapPlugin plugin, MapWorld world, MapSettings settings, PlanetGlobes globes, ServerDirectory servers)
            : base(new Vector2(0.5f, 0.5f), null, new Vector2(2f, 1f), false, null, 0f, 0f)
        {
            m_plugin = plugin;
            World = world;
            Settings = settings;
            Globes = globes;
            Servers = servers;
            m_panel = new Panel(this);

            CanHideOthers = false;
            EnabledBackgroundFade = false;
            CloseButtonEnabled = false;
            SkipTransition = true;
            m_closeOnEsc = false;
            m_drawEvenWithoutFocus = true;

            PlanetMode = new PlanetView(this);
            LocalMode = new LocalView(this);
            OrreryMode = new OrreryView(this);
            BodiesMode = new BodiesView(this);
            GalaxyMode = new GalaxyView(this);
            Tab = SystemTab.Orrery;

            Current = this;
            m_hudWasMinimal = MyHud.MinimalHud;
            MyHud.MinimalHud = true;

            World.Refresh();
            GameTextures.EnsureBasics();
            Gfx.BeginFrame();
            Camera.SetLens(RenderHooks.LastFov, Gfx.Width / Gfx.Height);

            // Always centred on the player: on a planet, its globe; in space,
            // the neighbourhood.
            var planet = World.PlanetUnderPlayer();
            if (planet != null)
                GoPlanet(planet, false, false);
            else
                GoLocal(LocalAnchor.Player, null, null, false, false);
            Camera.Snap();
            RenderHooks.Jump = true;
        }

        public override string GetFriendlyName()
        {
            return "SirHolomapScreen";
        }

        public MapView View
        {
            get
            {
                switch (Mode)
                {
                    case MapMode.Planet:
                        return PlanetMode;
                    case MapMode.Local:
                        return LocalMode;
                    default:
                        switch (Tab)
                        {
                            case SystemTab.Bodies:
                                return BodiesMode;
                            case SystemTab.Galaxy:
                                return GalaxyMode;
                            default:
                                return OrreryMode;
                        }
                }
            }
        }

        // Changes of view. Each starts a soft glide of the camera.

        private void StartTransition()
        {
            m_transitionUntil = Time + 1.2;
            ListScroll = 0;
            m_clicks.Cancel();
        }

        public void GoPlanet(Body body, bool fromSystem, bool continuous)
        {
            if (body == null)
                return;
            PlanetMode.Enter(body, fromSystem, continuous);
            Mode = MapMode.Planet;
            if (!continuous)
                Selected = null;
            StartTransition();
        }

        public void GoLocal(LocalAnchor anchor, Body body, Marker grid, bool fromSystem, bool continuous)
        {
            LocalMode.Enter(anchor, body, grid, fromSystem, continuous);
            Mode = MapMode.Local;
            StartTransition();
        }

        public void GoSystem(SystemTab tab, bool continuous)
        {
            var from3D = View.Uses3D;
            Tab = tab;
            Mode = MapMode.System;
            if (tab == SystemTab.Orrery)
                OrreryMode.Enter(continuous && from3D);
            StartTransition();
        }

        // The buttons at the top.
        private void PickMode(MapMode mode)
        {
            if (mode == Mode)
                return;
            switch (mode)
            {
                case MapMode.Planet:
                    var body = World.PlanetUnderPlayer() ?? PlanetMode.Body ?? World.NearestBody(World.PlayerPosition);
                    GoPlanet(body, false, false);
                    break;
                case MapMode.Local:
                    var gravity = World.PlanetUnderPlayer();
                    if (gravity != null)
                        GoLocal(LocalAnchor.Body, gravity, null, false, false);
                    else
                        GoLocal(LocalAnchor.Player, null, null, false, false);
                    break;
                default:
                    GoSystem(Tab, false);
                    break;
            }
        }

        // Input. The game's controls are not handled here at all: they are
        // not passed on, so nothing reaches the character.
        public override void HandleInput(bool receivedFocusInThisUpdate)
        {
            try
            {
                HandleMapInput();
            }
            catch (Exception e)
            {
                m_plugin.Fault("input", e);
            }
        }

        private void HandleMapInput()
        {
            var input = MyAPIGateway.Input;
            if (input == null)
                return;
            var mouse = Gfx.Mouse;
            var now = m_clock.Elapsed.TotalSeconds;
            var inputDt = MathHelper.Clamp(now - m_lastInput, 0, 0.1);
            m_lastInput = now;

            if (FocusedField != null)
            {
                if (HandleField(input))
                    return;
            }
            else
            {
                if (input.IsNewKeyPressed(MyKeys.Escape) || input.IsNewKeyPressed(HolomapPlugin.MapKey))
                {
                    CloseScreen();
                    return;
                }
                if (input.IsNewKeyPressed(MyKeys.Space))
                    View.Recentre();
            }

            // Left button: a click on the interface, a click or a drag on the
            // map.
            if (input.IsNewLeftMousePressed())
            {
                m_pressAt = mouse;
                m_dragging = false;
                m_leftOnMap = false;
                if (!ClickInterface(mouse))
                {
                    if (FocusedField != null)
                        CommitField();
                    m_leftOnMap = OverMap(mouse);
                }
            }
            if (m_leftOnMap && input.IsLeftMousePressed())
            {
                if (!m_dragging && (mouse - m_pressAt).Length() > 5 * Gfx.Scale)
                {
                    m_dragging = true;
                    m_clicks.Cancel();
                }
                if (m_dragging)
                    View.Rotate(mouse - m_lastMouse);
            }
            if (m_leftOnMap && input.IsNewLeftMouseReleased())
            {
                if (!m_dragging)
                {
                    object target;
                    var kind = m_clicks.Click(mouse.X, mouse.Y, Time, View.Pick(mouse) ?? (object)Nothing, out target);
                    if (kind == ClickKind.Double)
                        DoubleClick(target);
                }
                m_leftOnMap = false;
                m_dragging = false;
            }

            if (input.IsNewMiddleMousePressed())
                m_middleOnMap = OverMap(mouse);
            if (m_middleOnMap && input.IsMiddleMousePressed())
                View.Pan(mouse - m_lastMouse);
            else
                m_middleOnMap = false;

            if (input.IsNewRightMousePressed() && OverMap(mouse))
                Selected = null;

            var wheel = input.DeltaMouseScrollWheelValue();
            if (wheel != 0)
            {
                if (m_listArea.Contains(mouse) && m_listTotal > m_listShown)
                {
                    ListScroll = MathHelper.Clamp(ListScroll - Math.Sign(wheel) * 3, 0, Math.Max(0, m_listTotal - m_listShown));
                }
                else if (OverMap(mouse))
                {
                    View.Wheel(ZoomSteps.NotchesFromWheel(wheel), input.IsAnyCtrlKeyPressed());
                }
            }

            if (FocusedField == null)
            {
                var keys = Vector2.Zero;
                if (input.IsGameControlPressed(MyControlsSpace.FORWARD))
                    keys.Y += 1;
                if (input.IsGameControlPressed(MyControlsSpace.BACKWARD))
                    keys.Y -= 1;
                if (input.IsGameControlPressed(MyControlsSpace.STRAFE_LEFT))
                    keys.X -= 1;
                if (input.IsGameControlPressed(MyControlsSpace.STRAFE_RIGHT))
                    keys.X += 1;
                if (keys != Vector2.Zero)
                    View.Move(keys, inputDt);
            }

            m_lastMouse = mouse;
        }

        // What a click on empty space carries: the single click then clears
        // the selection.
        private static readonly object Nothing = new object();

        private void DoubleClick(object target)
        {
            if (target == null || ReferenceEquals(target, Nothing))
                return;
            Selected = target;
            View.DoubleClick(target);
        }

        private bool OverMap(Vector2 mouse)
        {
            return mouse.Y > TopBarHeight && mouse.X < PanelLeft - 8 * Gfx.Scale;
        }

        private bool ClickInterface(Vector2 mouse)
        {
            for (var i = m_lastHits.Count - 1; i >= 0; i--)
            {
                if (m_lastHits[i].Rect.Contains(mouse))
                {
                    if (m_lastHits[i].Click != null)
                        m_lastHits[i].Click();
                    return true;
                }
            }
            return mouse.Y <= TopBarHeight || mouse.X >= PanelLeft - 8 * Gfx.Scale;
        }

        // Registered by the panel while drawing; true when the mouse is over.
        public bool AddHit(RectangleF rect, Action click, object item, string help)
        {
            m_hits.Add(new Hit { Rect = rect, Click = click, Item = item, Help = help });
            var over = rect.Contains(Gfx.Mouse);
            if (over && help != null)
                m_help = help;
            if (over && item != null)
                ListHovered = item;
            return over;
        }

        public void SetListArea(RectangleF area, int total, int shown)
        {
            m_listArea = area;
            m_listTotal = total;
            m_listShown = shown;
            ListScroll = MathHelper.Clamp(ListScroll, 0, Math.Max(0, total - shown));
        }

        // A row of the list: one click selects and shows it, a double click
        // dives into it, as on the map.
        public void ListClick(object item)
        {
            if (item == null)
                return;
            // Selecting is harmless: it shows at once. Only the dive waits for
            // the second click.
            object target;
            var kind = m_clicks.Click(Gfx.Mouse.X, Gfx.Mouse.Y, Time, item, out target);
            Selected = item;
            View.Focus(item);
            if (kind == ClickKind.Double)
                View.DoubleClick(item);
        }

        // Text fields of the panel: digits for thresholds, free text for the
        // galaxy search.
        public void FocusField(string id, string value, Action<string> commit, bool digits)
        {
            if (FocusedField == id)
                return;
            if (FocusedField != null)
                CommitField();
            FocusedField = id;
            FieldText = value ?? "";
            m_fieldCommit = commit;
            m_fieldDigits = digits;
        }

        private void CommitField()
        {
            var commit = m_fieldCommit;
            var text = FieldText;
            FocusedField = null;
            m_fieldCommit = null;
            if (commit != null)
            {
                commit(text);
                SettingsChanged();
            }
        }

        private bool HandleField(VRage.ModAPI.IMyInput input)
        {
            if (input.IsNewKeyPressed(MyKeys.Escape) || input.IsNewKeyPressed(MyKeys.Enter) || input.IsNewKeyPressed(MyKeys.Tab))
            {
                CommitField();
                return true;
            }
            if (input.IsNewKeyPressed(MyKeys.Back) && FieldText.Length > 0)
                FieldText = FieldText.Substring(0, FieldText.Length - 1);
            foreach (var c in input.TextInput)
            {
                if (char.IsControl(c))
                    continue;
                if (m_fieldDigits && !char.IsDigit(c))
                    continue;
                if (FieldText.Length < (m_fieldDigits ? 6 : 48))
                    FieldText += c;
            }
            return false;
        }

        public void SettingsChanged()
        {
            m_settingsDirty = true;
            m_settingsSaveAt = Time + 0.5;
        }

        public override bool Update(bool hasFocus)
        {
            var result = base.Update(hasFocus);
            try
            {
                UpdateMap();
            }
            catch (Exception e)
            {
                m_plugin.Fault("update", e);
            }
            return result;
        }

        private void UpdateMap()
        {
            if (m_closed)
                return;
            Time = m_clock.Elapsed.TotalSeconds;
            var dt = MathHelper.Clamp(Time - m_lastTime, 0, 0.1);

            if (!World.InWorld || MyAPIGateway.Session == null)
            {
                CloseScreen();
                return;
            }

            // The character and the ship stay as they are.
            var controlled = MyAPIGateway.Session.ControlledObject;
            if (controlled != null)
                controlled.MoveAndRotate(Vector3.Zero, Vector2.Zero, 0f);

            World.Refresh();
            // A selected body stays selected when the bodies are read again.
            var selectedBody = Selected as Body;
            if (selectedBody != null)
                Selected = World.Follow(selectedBody);
            Globes.Work();
            if (Mode == MapMode.System && Tab == SystemTab.Galaxy)
                Servers.Update(World, Settings);

            object clicked;
            if (m_clicks.Poll(Time, out clicked) == ClickKind.Single)
                Selected = ReferenceEquals(clicked, Nothing) ? null : clicked;

            Camera.SetLens(RenderHooks.LastFov, Gfx.Width / Gfx.Height);
            Camera.HalfLife = Time < m_transitionUntil ? MapCamera.TransitionHalfLife : MapCamera.QuickHalfLife;
            var before = Camera.Position;
            View.Update(dt);
            Camera.Update(dt);

            // A long glide is a jump for the terrain's level of detail.
            if ((Camera.Position - before).Length() > Camera.Distance * 0.05)
                RenderHooks.Jump = true;

            var view = View;
            RenderHooks.Active = view.Uses3D && RenderHooks.CameraAvailable;
            RenderHooks.View = Camera.View;
            RenderHooks.Position = Camera.Position;
            RenderHooks.NeededFar = view.NeededFar;
            RenderHooks.Daylight = Mode == MapMode.Planet && Settings.DaylightGlobe;
            var light = Camera.Forward * 0.8 - Camera.Up * 0.45 + Camera.Right * 0.4;
            RenderHooks.LightDirection = Vector3D.Normalize(light);

            if (m_settingsDirty && Time > m_settingsSaveAt)
            {
                m_settingsDirty = false;
                m_plugin.SaveSettings();
            }
            m_lastTime = Time;
        }

        public override bool CloseScreen(bool isUnloading = false)
        {
            Release();
            return base.CloseScreen(isUnloading);
        }

        protected override void OnClosed()
        {
            Release();
            base.OnClosed();
        }

        private void Release()
        {
            if (m_closed)
                return;
            m_closed = true;
            if (FocusedField != null)
                CommitField();
            RenderHooks.Release();
            MyHud.MinimalHud = m_hudWasMinimal;
            Servers.Stop();
            m_plugin.SaveSettings();
            m_plugin.MapClosed();
            if (Current == this)
                Current = null;
        }

        // Layout, in pixels.
        public float TopBarHeight
        {
            get { return 56 * Gfx.Scale; }
        }

        public float PanelWidth
        {
            get { return Math.Max(330 * Gfx.Scale, Math.Min(420 * Gfx.Scale, Gfx.Width * 0.24f)); }
        }

        public float PanelLeft
        {
            get { return Gfx.Width - PanelWidth - 14 * Gfx.Scale; }
        }

        public RectangleF MapArea
        {
            get { return new RectangleF(0, TopBarHeight, PanelLeft - 14 * Gfx.Scale, Gfx.Height - TopBarHeight); }
        }

        public override bool Draw()
        {
            try
            {
                DrawMap();
            }
            catch (Exception e)
            {
                m_plugin.Fault("draw", e);
            }
            return true;
        }

        private void DrawMap()
        {
            if (m_closed)
                return;
            Gfx.BeginFrame();
            GameTextures.EnsureBasics();
            m_hits.Clear();
            m_help = null;
            ListHovered = null;

            var view = View;
            if (!view.Uses3D || !RenderHooks.Active)
                Gfx.Rect(0, 0, Gfx.Width, Gfx.Height, Style.Space);

            var mouse = Gfx.Mouse;
            Hovered = OverMap(mouse) && !m_dragging ? view.Pick(mouse) : null;
            view.DrawMap();

            DrawTopBar();
            DrawPanel(view);
            DrawStatus(view);

            m_lastHits.Clear();
            m_lastHits.AddRange(m_hits);
        }

        private void DrawTopBar()
        {
            var s = Gfx.Scale;
            var w = Gfx.Width;
            Gfx.Rect(0, 0, w, TopBarHeight, Style.BarBackground);
            Gfx.Rect(0, TopBarHeight - 2 * s, w, 2 * s, Gfx.Alpha(Style.Accent, 0.8f));
            Gfx.Text(Texts.Title, 24 * s, TopBarHeight / 2, 0.95f, Style.Accent, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);

            var labels = new[] { Texts.ModePlanet, Texts.ModeLocal, Texts.ModeSystem };
            var helps = new[] { Texts.ModePlanetHelp, Texts.ModeLocalHelp, Texts.ModeSystemHelp };
            var modes = new[] { MapMode.Planet, MapMode.Local, MapMode.System };
            var bw = 210 * s;
            var bh = 38 * s;
            var gap = 10 * s;
            var x0 = (PanelLeft - (bw * 3 + gap * 2)) / 2;
            var noPlanet = World.Bodies.Count == 0;
            for (var i = 0; i < 3; i++)
            {
                var x = x0 + i * (bw + gap);
                var y = (TopBarHeight - bh) / 2;
                var mode = modes[i];
                var disabled = mode == MapMode.Planet && noPlanet;
                var hover = AddHit(new RectangleF(x, y, bw, bh), disabled ? (Action)null : () => PickMode(mode), null,
                    disabled ? Texts.ModePlanetUnavailable : helps[i]);
                var on = Mode == mode;
                Gfx.Rect(x, y, bw, bh, on ? Gfx.Alpha(Style.Accent, 0.32f) : (hover ? Style.RowHover : Style.Row));
                Gfx.Frame(x, y, bw, bh, on ? 2 : 1, on ? Style.Accent : Style.AccentDim);
                Gfx.Text(labels[i], x + bw / 2, y + bh / 2, 0.75f, disabled ? Style.Dim : (on ? Color.White : Style.Text),
                    MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
            }

            var closeX = w - 16 * s;
            Gfx.Text(Texts.CloseHint, closeX, TopBarHeight / 2, 0.6f, Style.Dim, MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER);
        }

        private void DrawPanel(MapView view)
        {
            var s = Gfx.Scale;
            var top = TopBarHeight + 12 * s;
            var bottom = Gfx.Height - 14 * s;
            m_panel.Begin(PanelLeft, top, PanelWidth, bottom);
            m_panel.Title(view.Title, view.Subtitle);

            if (Mode == MapMode.System)
            {
                m_panel.Tabs(new[] { Texts.TabBodies, Texts.TabOrrery, Texts.TabGalaxy },
                    new[] { Texts.TabBodiesHelp, Texts.TabOrreryHelp, Texts.TabGalaxyHelp },
                    (int)Tab, i => GoSystem((SystemTab)i, false));
            }

            m_panel.Section(Texts.SectionInfo);
            var marker = Selected as Marker;
            var body = Selected as Body;
            var server = Selected as GalaxyServer;
            var cluster = Selected as IconCluster;
            if (marker != null)
                view.MarkerInfo(m_panel, marker);
            else if (body != null)
                view.BodyInfo(m_panel, body);
            else if (server != null)
                GalaxyMode.ServerInfo(m_panel, server);
            else if (cluster != null)
                m_panel.Heading(string.Format(Texts.ServersHere, cluster.Count), Texts.ServersHereHelp);
            else
                view.DefaultInfo(m_panel);

            m_panel.Section(Texts.SectionShow);
            view.Filters(m_panel);

            view.List(m_panel);
            m_panel.Footer(view.Help);
        }

        private void DrawStatus(MapView view)
        {
            var s = Gfx.Scale;
            var y = Gfx.Height - 30 * s;
            var x = 20 * s;
            var text = Texts.You + ": " + ((Vector3I)World.PlayerPosition).ToString();
            if (World.SyncRadius < double.MaxValue)
                text += "      " + Texts.SyncRange + ": " + MapView.Distance(World.SyncRadius);
            Gfx.Rect(0, y - 6 * s, PanelLeft - 14 * s, 36 * s, new Color(0, 0, 0, 120));
            Gfx.Text(text, x, y, 0.58f, Style.Dim);

            // A scale bar in the 3D views.
            if (view.Uses3D)
            {
                var metres = 120 * s / Camera.PixelsPerMetre(Camera.Distance);
                var nice = Math.Pow(10, Math.Floor(Math.Log10(metres)));
                if (metres / nice >= 5)
                    nice *= 5;
                else if (metres / nice >= 2)
                    nice *= 2;
                var length = (float)(nice * Camera.PixelsPerMetre(Camera.Distance));
                var bx = PanelLeft - 40 * s - length;
                Gfx.Rect(bx, y + 8 * s, length, 2 * s, Style.Text);
                Gfx.Rect(bx, y + 2 * s, 2 * s, 12 * s, Style.Text);
                Gfx.Rect(bx + length - 2 * s, y + 2 * s, 2 * s, 12 * s, Style.Text);
                Gfx.Text(MapView.Distance(nice), bx - 8 * s, y, 0.58f, Style.Text, MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP);
            }

            if (!string.IsNullOrEmpty(m_help))
            {
                var hx = (PanelLeft - 14 * s) / 2;
                Gfx.Text(m_help, hx, TopBarHeight + 10 * s, 0.6f, Style.Text, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP);
            }
        }
    }
}
