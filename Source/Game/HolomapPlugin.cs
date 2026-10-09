using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.FileSystem;
using VRage.Input;
using VRage.Plugins;
using VRage.Utils;

namespace SirHolomap
{
    // Sir Holomap, a client-side plugin loaded by Pulsar: an in-game map on M.
    // Nothing goes through the server; a player without the plugin plays as
    // usual. Everything the map remembers, the player saw: it is kept on the
    // player's machine, one memory per server.
    public class HolomapPlugin : IPlugin
    {
        public const string Id = "sir-holomap";

        // M opens and closes the map, except with a block in hand: then M
        // keeps the game's symmetry.
        public const MyKeys MapKey = MyKeys.M;

        // Updates between two scans of what the game sends (60 per second).
        private const int ScanInterval = 20;

        // Updates between two looks at who else is hooked on our steps.
        private const int CoexistenceInterval = 600;

        private Harmony m_harmony;
        private string m_folder;
        private MapSettings m_settings;
        private MapWorld m_world;
        private PlanetGlobes m_globes;
        private ServerDirectory m_servers;
        private int m_frame;
        private int m_hotkeyBlockedUntil;
        private int m_scanCounter;
        private int m_coexistenceCounter;
        private bool m_stopped;
        // Notices waiting for a world to be shown in, oldest first. Written
        // when shown, in the language the game is set to by then.
        private readonly List<Func<string>> m_pendingNotices = new List<Func<string>>();
        private DateTime m_lastFault = DateTime.MinValue;

        internal static HolomapPlugin Instance { get; private set; }

        public static void Log(string text)
        {
            MyLog.Default.WriteLine("[" + Id + "] " + text);
        }

        public void Init(object gameInstance)
        {
            Instance = this;

            // The player's folder: %AppData%\SpaceEngineers\Storage\sir-holomap
            m_folder = Path.Combine(MyFileSystem.UserDataPath, "Storage", Id);
            string problem;
            m_settings = MapSettingsFile.Load(m_folder, out problem);
            if (problem != null)
                Log(problem + "; using the defaults");

            m_world = new MapWorld(m_folder);
            m_globes = new PlanetGlobes();
            m_servers = new ServerDirectory();

            try
            {
                Hook();
            }
            catch (Exception e)
            {
                RenderHooks.CameraAvailable = false;
                RenderHooks.LightAvailable = false;
                Log("could not hook the render camera: " + e);
                m_pendingNotices.Add(() => Texts.CameraHookFailed);
            }
            Log("loaded; camera " + (RenderHooks.CameraAvailable ? "hooked" : "not hooked") + ", " + m_world.History.Count + " servers in history");
        }

        // Two plugins never fight over the same step of the game: if another
        // one already holds the camera or the light, the map leaves it alone
        // and says so.
        private void Hook()
        {
            m_harmony = new Harmony(Id);
            var camera = RenderHooks.CameraMethod;
            var light = RenderHooks.LightMethod;
            if (camera == null)
            {
                m_pendingNotices.Add(() => Texts.CameraHookFailed);
                Log(RenderHooks.CameraStep + " not found");
            }
            else
            {
                var others = RenderHooks.OtherOwners(camera, Id);
                if (others.Count > 0)
                {
                    var cameraOwners = string.Join(", ", others.ToArray());
                    m_pendingNotices.Add(() => string.Format(Texts.CameraStepTaken, cameraOwners));
                    Log(RenderHooks.CameraStep + " is already patched by " + string.Join(", ", others.ToArray()) + ": stepping aside");
                }
                else
                {
                    m_harmony.Patch(camera, prefix: new HarmonyMethod(typeof(RenderHooks).GetMethod("CameraPrefix")));
                    RenderHooks.CameraAvailable = true;
                }
            }

            if (light == null)
            {
                m_pendingNotices.Add(() => Texts.LightHookFailed);
                Log(RenderHooks.LightStep + " not found: the globe keeps the game's light");
                return;
            }
            var lightOthers = RenderHooks.OtherOwners(light, Id);
            if (lightOthers.Count > 0)
            {
                var names = string.Join(", ", lightOthers.ToArray());
                m_pendingNotices.Add(() => string.Format(Texts.LightStepTaken, names));
                Log(RenderHooks.LightStep + " is already patched by " + names + ": stepping aside, the globe keeps the game's light");
                return;
            }
            m_harmony.Patch(light, prefix: new HarmonyMethod(typeof(RenderHooks).GetMethod("LightPrefix")));
            RenderHooks.LightAvailable = true;
        }

        private void CheckCoexistence()
        {
            if (RenderHooks.CameraAvailable)
            {
                var others = RenderHooks.OtherOwners(RenderHooks.CameraMethod, Id);
                if (others.Count > 0)
                {
                    RenderHooks.CameraAvailable = false;
                    RenderHooks.Release();
                    var names = string.Join(", ", others.ToArray());
                    Log(RenderHooks.CameraStep + " is now patched by " + names + ": stepping aside");
                    Notify(string.Format(Texts.CameraStepTaken, names));
                }
            }
            if (RenderHooks.LightAvailable)
            {
                var others = RenderHooks.OtherOwners(RenderHooks.LightMethod, Id);
                if (others.Count > 0)
                {
                    RenderHooks.LightAvailable = false;
                    RenderHooks.Daylight = false;
                    var names = string.Join(", ", others.ToArray());
                    Log(RenderHooks.LightStep + " is now patched by " + names + ": stepping aside");
                    Notify(string.Format(Texts.LightStepTaken, names));
                }
            }
        }

        public void Update()
        {
            m_frame++;
            if (m_stopped)
                return;
            try
            {
                FollowLanguage();
                var wasInWorld = m_world.InWorld;
                m_world.Track();
                if (wasInWorld != m_world.InWorld)
                    m_globes.Clear();
                ServerJoiner.Update(m_world.InWorld);
                if (!m_world.InWorld)
                {
                    if (MapScreen.Current == null && RenderHooks.Active)
                        RenderHooks.Release();
                    return;
                }

                if (m_pendingNotices.Count > 0 && MyAPIGateway.Utilities != null)
                {
                    foreach (var notice in m_pendingNotices)
                        Notify(notice());
                    m_pendingNotices.Clear();
                }

                if (++m_scanCounter >= ScanInterval)
                {
                    m_scanCounter = 0;
                    m_world.Scan();
                    m_world.SaveIfDue();
                    m_globes.Prepare(m_world.Bodies);
                }

                // The globes are painted from the moment the world is joined,
                // so that they are ready when the map opens.
                m_globes.Work(MapScreen.Current != null);

                if (++m_coexistenceCounter >= CoexistenceInterval)
                {
                    m_coexistenceCounter = 0;
                    CheckCoexistence();
                }

                // A map closed without passing through its own close: never
                // leave the camera elsewhere.
                if (MapScreen.Current == null && RenderHooks.Active)
                    RenderHooks.Release();

                CheckHotkey();
            }
            catch (Exception e)
            {
                Fault("update", e);
            }
        }

        // The plugin speaks the language set in the game's options.
        private static void FollowLanguage()
        {
            try
            {
                var config = Sandbox.MySandboxGame.Config;
                if (config != null)
                    Localization.Current = Localization.FromGame((int)config.Language);
            }
            catch (Exception)
            {
                Localization.Current = GameLanguage.English;
            }
        }

        private void CheckHotkey()
        {
            if (MapScreen.Current != null || m_frame < m_hotkeyBlockedUntil)
                return;
            var input = MyAPIGateway.Input;
            var gui = MyAPIGateway.Gui;
            if (input == null || gui == null || !input.IsNewKeyPressed(MapKey))
                return;
            // Not while typing, nor over another screen.
            if (gui.ChatEntryVisible || gui.IsCursorVisible)
                return;
            // A block in hand: M is the game's symmetry.
            var builder = MyAPIGateway.CubeBuilder;
            if (builder != null && builder.IsActivated)
                return;
            if (input.IsAnyCtrlKeyPressed() || input.IsAnyAltKeyPressed() || input.IsAnyShiftKeyPressed())
                return;
            Open();
        }

        private void Open()
        {
            m_hotkeyBlockedUntil = m_frame + 5;
            m_world.Scan();
            MyGuiSandbox.AddScreen(new MapScreen(this, m_world, m_settings, m_globes, m_servers));
        }

        internal void MapClosed()
        {
            m_hotkeyBlockedUntil = m_frame + 5;
        }

        internal void SaveSettings()
        {
            try
            {
                MapSettingsFile.Save(m_folder, m_settings);
            }
            catch (Exception e)
            {
                Log("settings not saved: " + e.Message);
            }
        }

        // The server the player is on, null outside a world.
        internal ServerIdentity CurrentServer
        {
            get { return m_world != null ? m_world.Identity : null; }
        }

        // Before leaving for another server from the galaxy: the memory of
        // this one is kept.
        internal void BeforeLeaving()
        {
            try
            {
                if (MapScreen.Current != null)
                    MapScreen.Current.CloseScreen();
                m_world.Leave();
            }
            catch (Exception e)
            {
                Log("could not save before leaving: " + e.Message);
            }
        }

        // A fault never brings the game down: the map closes, the camera goes
        // back to the game, one line in the log, one notice to the player.
        internal void Fault(string where, Exception e)
        {
            RenderHooks.Release();
            // A fault that repeats is told once in a while, not every frame.
            var now = DateTime.UtcNow;
            var repeated = (now - m_lastFault).TotalSeconds < 30;
            m_lastFault = now;
            if (repeated)
            {
                try
                {
                    if (MapScreen.Current != null)
                        MapScreen.Current.CloseScreen();
                }
                catch (Exception)
                {
                }
                return;
            }
            Log("fault in " + where + ": " + e);
            try
            {
                if (MapScreen.Current != null)
                    MapScreen.Current.CloseScreen();
            }
            catch (Exception)
            {
            }
            Notify(Texts.Fault);
        }

        private static void Notify(string text)
        {
            try
            {
                if (MyAPIGateway.Utilities != null)
                    MyAPIGateway.Utilities.ShowNotification(text, 8000, "Red");
            }
            catch (Exception)
            {
            }
        }

        public void Dispose()
        {
            try
            {
                RenderHooks.Release();
                if (m_world != null)
                    m_world.Leave();
                ServerJoiner.Reset();
                if (m_servers != null)
                    m_servers.Stop();
                GameTextures.DestroyAll();
                // Only our patches, never anyone else's.
                if (m_harmony != null)
                    m_harmony.UnpatchAll(Id);
            }
            catch (Exception e)
            {
                Log("incomplete shutdown: " + e.Message);
            }
            m_stopped = true;
            Instance = null;
        }
    }
}
