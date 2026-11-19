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
    // La carte, plein écran. Tant qu'elle est ouverte, elle tient le clavier et la
    // souris : les commandes du jeu n'atteignent jamais le personnage ni le vaisseau, et
    // le personnage reçoit à chaque image l'ordre de rester immobile. Le jeu continue de
    // dessiner son monde derrière, vu depuis la caméra de la carte.
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
        private Vector2 m_rightPressAt;
        private bool m_rightOnMap;
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

            // Toujours centrée sur le joueur : sur une planète, son globe ; dans l'espace,
            // le voisinage.
            var planet = World.PlanetUnderPlayer();
            if (planet != null)
                GoPlanet(planet, false, false, null);
            else
                GoLocal(LocalAnchor.Player, null, null, false, false);
            Camera.Snap();
            RenderHooks.Jump = true;
            PointsDeFonctionnalites.Point(Fonctionnalites.Carte, World);
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
                    case MapMode.Galaxy:
                        return GalaxyMode;
                    default:
                        return Tab == SystemTab.Bodies ? (MapView)BodiesMode : OrreryMode;
                }
            }
        }

        // Changements de vue. Chacun lance un doux glissement de la caméra.

        private void StartTransition()
        {
            m_transitionUntil = Time + 1.2;
            ListScroll = 0;
            m_clicks.Cancel();
        }

        // Une vue qui déplace seule sa caméra loin (un autre globe, par
        // exemple) y glisse aussi doucement qu'un changement de vue.
        public void Glide()
        {
            StartTransition();
        }

        // grid : une grille à garder au milieu du globe, la caméra tournant
        // autour d'elle.
        public void GoPlanet(Body body, bool fromSystem, bool continuous, Marker grid)
        {
            if (body == null)
                return;
            PlanetMode.Enter(body, fromSystem, continuous, grid);
            Mode = MapMode.Planet;
            if (grid != null)
                Selected = grid;
            else if (!continuous)
                Selected = null;
            StartTransition();
        }

        public void GoLocal(LocalAnchor anchor, Body body, Marker grid, bool fromSystem, bool continuous)
        {
            LocalMode.Enter(anchor, body, grid, fromSystem, continuous);
            Mode = MapMode.Local;
            StartTransition();
        }

        // continuous : atteint avec la molette, en glissant depuis l'endroit où est la
        // caméra ; depuis la galaxie, le système revient de loin.
        public void GoSystem(SystemTab tab, bool continuous)
        {
            var fromGalaxy = Mode == MapMode.Galaxy;
            var from3D = View.Uses3D;
            Tab = tab;
            Mode = MapMode.System;
            PointsDeFonctionnalites.Point(Fonctionnalites.Systeme, World);
            if (fromGalaxy)
                Selected = null;
            if (tab == SystemTab.Orrery)
            {
                if (fromGalaxy && continuous)
                    OrreryMode.EnterFromGalaxy();
                else
                    OrreryMode.Enter(continuous && from3D);
            }
            StartTransition();
        }

        // La galaxie, une vue à part. continuous : atteinte en dézoomant
        // du système, elle commence près du serveur actuel et glisse
        // vers la galaxie entière.
        public void GoGalaxy(bool continuous)
        {
            if (Mode == MapMode.Galaxy)
                return;
            Selected = null;
            Mode = MapMode.Galaxy;
            PointsDeFonctionnalites.Point(Fonctionnalites.Galaxie, World);
            // Les serveurs d'abord : le glissement commence sur le serveur actuel.
            Servers.Update(World, Settings);
            GalaxyMode.Enter(continuous);
            StartTransition();
        }

        // Les boutons du haut.
        private void PickMode(MapMode mode)
        {
            if (mode == Mode)
                return;
            switch (mode)
            {
                case MapMode.Planet:
                    // Une grille suivie en B près d'une planète reste au milieu
                    // du globe.
                    var locked = Mode == MapMode.Local ? LocalMode.LockedGrid : null;
                    var lockedBody = locked != null ? World.BodyAt(locked.Position, 1.6) : null;
                    if (lockedBody != null)
                    {
                        GoPlanet(lockedBody, false, true, locked);
                        break;
                    }
                    var body = World.PlanetUnderPlayer() ?? PlanetMode.Body ?? World.NearestBody(World.PlayerPosition);
                    GoPlanet(body, false, false, null);
                    break;
                case MapMode.Local:
                    var fastened = Mode == MapMode.Planet ? PlanetMode.LockedGrid : null;
                    if (fastened != null && fastened.Live)
                    {
                        GoLocal(LocalAnchor.Grid, null, fastened, false, false);
                        Selected = fastened;
                        break;
                    }
                    var gravity = World.PlanetUnderPlayer();
                    if (gravity != null)
                        GoLocal(LocalAnchor.Body, gravity, null, false, false);
                    else
                        GoLocal(LocalAnchor.Player, null, null, false, false);
                    break;
                case MapMode.Galaxy:
                    GoGalaxy(false);
                    break;
                default:
                    GoSystem(Tab, false);
                    break;
            }
        }

        // Entrées. Les commandes du jeu ne sont pas du tout traitées ici : elles ne
        // sont pas transmises, donc rien n'atteint le personnage.
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

            // Bouton gauche : un clic sur l'interface, un clic ou un glissement sur la
            // carte.
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
                if (!m_dragging && !FollowGestures.IsClick(m_pressAt.X, m_pressAt.Y, mouse.X, mouse.Y, Gfx.Scale))
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

            // Bouton droit : un simple clic efface la sélection et met fin au
            // suivi d'une grille ; le jeu ne le voit jamais.
            if (input.IsNewRightMousePressed())
            {
                m_rightPressAt = mouse;
                m_rightOnMap = OverMap(mouse);
                if (m_rightOnMap)
                    Selected = null;
            }
            if (m_rightOnMap && input.IsRightMousePressed()
                && !FollowGestures.IsClick(m_rightPressAt.X, m_rightPressAt.Y, mouse.X, mouse.Y, Gfx.Scale))
                m_rightOnMap = false;
            if (m_rightOnMap && input.IsNewRightMouseReleased())
            {
                m_rightOnMap = false;
                StopFollowing();
            }

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

        // Ce que porte un clic dans le vide : le simple clic efface alors
        // la sélection.
        private static readonly object Nothing = new object();

        private void DoubleClick(object target)
        {
            if (target == null)
                return;
            // Deux clics rapides dans le vide sont encore des clics sur rien.
            if (ReferenceEquals(target, Nothing))
            {
                Selected = null;
                StopFollowing();
                return;
            }
            Selected = target;
            View.DoubleClick(target);
        }

        // Retour au voisinage centré sur le joueur, en glissant, quand une
        // grille est suivie en B.
        private void StopFollowing()
        {
            if (Mode != MapMode.Local || !LocalMode.StopFollowing())
                return;
            Selected = null;
            StartTransition();
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

        // Enregistré par le panneau pendant le dessin ; vrai quand la souris est dessus.
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

        // Une ligne de la liste : un clic la sélectionne et la montre, un double clic
        // plonge dedans, comme sur la carte.
        public void ListClick(object item)
        {
            if (item == null)
                return;
            // Sélectionner est sans danger : cela se montre tout de suite. Seule la plongée attend
            // le second clic.
            object target;
            var kind = m_clicks.Click(Gfx.Mouse.X, Gfx.Mouse.Y, Time, item, out target);
            Selected = item;
            View.Focus(item);
            if (kind == ClickKind.Double)
                View.DoubleClick(item);
        }

        // Les champs de texte du panneau : des chiffres pour les seuils, du texte libre pour la
        // recherche de la galaxie.
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

            // Hors du monde, ou personnage mort : la carte se ferme toute seule
            // et le jeu montre son propre écran de réapparition, comme sans
            // la carte. M la rouvre une fois le joueur revenu.
            if (MyAPIGateway.Session == null || !PlayerAlive(World.InWorld))
            {
                CloseScreen();
                return;
            }

            // Le personnage et le vaisseau restent comme ils sont.
            var controlled = MyAPIGateway.Session.ControlledObject;
            if (controlled != null)
                controlled.MoveAndRotate(Vector3.Zero, Vector2.Zero, 0f);

            World.Refresh();
            // Un astre sélectionné reste sélectionné quand les astres sont relus.
            var selectedBody = Selected as Body;
            if (selectedBody != null)
                Selected = World.Follow(selectedBody);
            if (Mode == MapMode.Galaxy)
                Servers.Update(World, Settings);

            object clicked;
            if (m_clicks.Poll(Time, out clicked) == ClickKind.Single)
            {
                Selected = ReferenceEquals(clicked, Nothing) ? null : clicked;
                // Un simple clic dans le vide, hors du cadre de la
                // grille suivie, la lâche.
                if (ReferenceEquals(clicked, Nothing))
                    StopFollowing();
            }

            Camera.SetLens(RenderHooks.LastFov, Gfx.Width / Gfx.Height);
            Camera.HalfLife = Time < m_transitionUntil ? MapCamera.TransitionHalfLife : MapCamera.QuickHalfLife;
            var before = Camera.Position;
            View.Update(dt);
            Camera.Update(dt);

            // Un long glissement est un saut pour le niveau de détail du terrain.
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

        // Vrai tant que la carte peut rester ouverte : dans un monde, avec un
        // personnage vivant.
        public static bool PlayerAlive(bool inWorld)
        {
            var session = MyAPIGateway.Session;
            var player = session != null ? session.Player : null;
            var character = player != null ? player.Character : null;
            return !MapLife.MustClose(inWorld && player != null, character != null, character != null && character.IsDead);
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

        // Mise en page, en pixels.
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

            // Les vues sont nommées, jamais lettrées, de la plus proche à la
            // plus lointaine. Les boutons se partagent la place entre le titre et le
            // menu de droite.
            var labels = new[] { Texts.ModePlanet, Texts.ModeLocal, Texts.ModeSystem, Texts.ModeGalaxy };
            var helps = new[] { Texts.ModePlanetHelp, Texts.ModeLocalHelp, Texts.ModeSystemHelp, Texts.TabGalaxyHelp };
            var modes = new[] { MapMode.Planet, MapMode.Local, MapMode.System, MapMode.Galaxy };
            var count = modes.Length;
            var gap = 10 * s;
            var titleRight = 24 * s + Gfx.Measure(Texts.Title, 0.95f).X + 20 * s;
            var room = Math.Max(0, PanelLeft - 10 * s - titleRight);
            var bw = Math.Max(90 * s, Math.Min(210 * s, (room - gap * (count - 1)) / count));
            var bh = 38 * s;
            var total = bw * count + gap * (count - 1);
            var x0 = Math.Max(titleRight, (PanelLeft - total) / 2);
            var noPlanet = World.Bodies.Count == 0;
            for (var i = 0; i < count; i++)
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
                Gfx.Text(Gfx.Fit(labels[i], 0.75f, bw - 16 * s), x + bw / 2, y + bh / 2, 0.75f, disabled ? Style.Dim : (on ? Color.White : Style.Text),
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
            m_panel.PrepareFooter(view.Help);
            m_panel.Title(view.Title, view.Subtitle);

            if (Mode == MapMode.System)
            {
                m_panel.Tabs(new[] { Texts.TabBodies, Texts.TabOrrery },
                    new[] { Texts.TabBodiesHelp, Texts.TabOrreryHelp },
                    (int)Tab, i => GoSystem((SystemTab)i, false));
            }

            m_panel.Section(Texts.SectionInfo);
            var marker = Selected as Marker;
            var body = Selected as Body;
            var server = Selected as GalaxyServer;
            if (marker != null)
                view.MarkerInfo(m_panel, marker);
            else if (body != null)
                view.BodyInfo(m_panel, body);
            else if (server != null)
                GalaxyMode.ServerInfo(m_panel, server);
            else
                view.DefaultInfo(m_panel);

            m_panel.Section(Texts.SectionShow);
            view.Filters(m_panel);

            view.List(m_panel);
            m_panel.Footer();
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

            // Une barre d'échelle dans les vues 3D.
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
