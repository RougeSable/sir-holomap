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
    // Sir Holomap, un greffon côté joueur chargé par Pulsar : une carte en jeu sur M.
    // Rien ne part vers le serveur, sauf vers nos serveurs (PU, PTU) : les rapports d'erreurs
    // et un point par fonctionnalité utilisée et par jour ; un joueur sans le greffon joue
    // comme d'habitude. Tout ce que la carte retient, le joueur l'a vu : c'est gardé sur la
    // machine du joueur, une mémoire par serveur.
    public class HolomapPlugin : IPlugin
    {
        public const string Id = "sir-holomap";

        // M ouvre et ferme la carte, sauf avec un bloc en main : M
        // garde alors la symétrie du jeu.
        public const MyKeys MapKey = MyKeys.M;

        // Mises à jour entre deux balayages de ce que le jeu envoie (60 par seconde).
        private const int ScanInterval = 20;

        // Mises à jour entre deux regards sur qui d'autre est accroché à nos étapes.
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
        // Notifications qui attendent un monde où s'afficher, la plus ancienne en premier. Écrites
        // à l'affichage, dans la langue réglée dans le jeu à ce moment-là.
        private readonly List<Func<string>> m_pendingNotices = new List<Func<string>>();
        private DateTime m_lastFault = DateTime.MinValue;

        internal static HolomapPlugin Instance { get; private set; }

        public static void Log(string text)
        {
            var line = "[" + Id + "] " + text;
            MyLog.Default.WriteLine(line);
            ErrorReporting.Note(line);
        }

        // Une ligne qui dit que quelque chose a mal tourné : écrite au journal du jeu
        // comme les autres, et rapportée à nos serveurs une fois par session.
        public static void LogError(string text)
        {
            Log(text);
            ErrorReporting.Error(text);
        }

        // Une exception que le greffon a rattrapée et traitée : elle est rapportée aussi.
        public static void Caught(string where, Exception e)
        {
            ErrorReporting.Caught(where, e);
        }

        // Une exception rattrapée, avec la ligne que le greffon écrit à son sujet dans le
        // journal du jeu : le rapport porte cette ligne et la pile.
        public static void LogCaught(string text, Exception e)
        {
            Log(text);
            ErrorReporting.CaughtLine(text, e);
        }

        public void Init(object gameInstance)
        {
            Instance = this;

            // Le dossier du joueur : %AppData%\SpaceEngineers\Storage\sir-holomap
            m_folder = Path.Combine(MyFileSystem.UserDataPath, "Storage", Id);
            string problem;
            m_settings = MapSettingsFile.Load(m_folder, out problem);

            m_world = new MapWorld(m_folder);
            ErrorReporting.Start(m_folder, m_world.History);
            if (problem != null)
                LogError(problem + " ; valeurs par défaut utilisées");
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
                Log("impossible d'accrocher la caméra de rendu : " + e);
                Caught("accroche de la caméra de rendu", e);
                m_pendingNotices.Add(() => Texts.CameraHookFailed);
            }
            Log("Sir Holomap chargé ; caméra " + (RenderHooks.CameraAvailable ? "accrochée" : "non accrochée") + ", " + m_world.History.Count + " serveurs dans l'historique");
        }

        // Deux greffons ne se disputent jamais la même étape du jeu : si un autre
        // tient déjà la caméra ou la lumière, la carte n'y touche pas
        // et le dit.
        private void Hook()
        {
            m_harmony = new Harmony(Id);
            var camera = RenderHooks.CameraMethod;
            var light = RenderHooks.LightMethod;
            if (camera == null)
            {
                m_pendingNotices.Add(() => Texts.CameraHookFailed);
                LogError(RenderHooks.CameraStep + " introuvable");
            }
            else
            {
                var others = RenderHooks.OtherOwners(camera, Id);
                if (others.Count > 0)
                {
                    var cameraOwners = string.Join(", ", others.ToArray());
                    m_pendingNotices.Add(() => string.Format(Texts.CameraStepTaken, cameraOwners));
                    Log(RenderHooks.CameraStep + " est déjà modifiée par " + string.Join(", ", others.ToArray()) + " : la carte s'efface");
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
                LogError(RenderHooks.LightStep + " introuvable : le globe garde la lumière du jeu");
                return;
            }
            var lightOthers = RenderHooks.OtherOwners(light, Id);
            if (lightOthers.Count > 0)
            {
                var names = string.Join(", ", lightOthers.ToArray());
                m_pendingNotices.Add(() => string.Format(Texts.LightStepTaken, names));
                Log(RenderHooks.LightStep + " est déjà modifiée par " + names + " : la carte s'efface, le globe garde la lumière du jeu");
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
                    Log(RenderHooks.CameraStep + " est maintenant modifiée par " + names + " : la carte s'efface");
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
                    Log(RenderHooks.LightStep + " est maintenant modifiée par " + names + " : la carte s'efface");
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
                ErrorReporting.Tick(m_world);
                PointsDeFonctionnalites.Tick(m_world);
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

                // Les globes sont peints dès que le monde est rejoint,
                // pour qu'ils soient prêts quand la carte s'ouvre.
                m_globes.Work(MapScreen.Current != null);

                if (++m_coexistenceCounter >= CoexistenceInterval)
                {
                    m_coexistenceCounter = 0;
                    CheckCoexistence();
                }

                // Une carte fermée sans passer par sa propre fermeture : ne jamais
                // laisser la caméra ailleurs.
                if (MapScreen.Current == null && RenderHooks.Active)
                    RenderHooks.Release();

                CheckHotkey();
            }
            catch (Exception e)
            {
                Fault("update", e);
            }
        }

        // Le greffon parle la langue réglée dans les options du jeu.
        private static void FollowLanguage()
        {
            try
            {
                var config = Sandbox.MySandboxGame.Config;
                if (config != null)
                    Localization.Current = Localization.FromGame((int)config.Language);
            }
            catch (Exception ex)
            {
                HolomapPlugin.Caught("HolomapPlugin.FollowLanguage", ex);
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
            // Pas pendant la saisie, ni par-dessus un autre écran.
            if (gui.ChatEntryVisible || gui.IsCursorVisible)
                return;
            // Un bloc en main : M est la symétrie du jeu.
            var builder = MyAPIGateway.CubeBuilder;
            if (builder != null && builder.IsActivated)
                return;
            if (input.IsAnyCtrlKeyPressed() || input.IsAnyAltKeyPressed() || input.IsAnyShiftKeyPressed())
                return;
            // Mort, ou en attente de renaître : la réapparition du jeu d'abord.
            if (!MapScreen.PlayerAlive(m_world.InWorld))
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
                LogCaught("réglages non enregistrés : " + e.Message, e);
            }
        }

        // Le serveur où le joueur se trouve, null hors d'un monde.
        internal ServerIdentity CurrentServer
        {
            get { return m_world != null ? m_world.Identity : null; }
        }

        // Avant de partir pour un autre serveur depuis la galaxie : la mémoire de
        // celui-ci est gardée.
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
                LogCaught("impossible d'enregistrer avant de partir : " + e.Message, e);
            }
        }

        // Une faute ne fait jamais tomber le jeu : la carte se ferme, la caméra
        // retourne au jeu, une ligne dans le journal, une notification au joueur.
        internal void Fault(string where, Exception e)
        {
            RenderHooks.Release();
            // Chaque faute compte pour les rapports, qui disent chacune une seule fois.
            Caught("faute dans " + where, e);
            // Une faute qui se répète est dite de temps en temps, pas à chaque image.
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
                catch (Exception ex)
                {
                    HolomapPlugin.Caught("HolomapPlugin.Fault", ex);
                }
                return;
            }
            Log("faute dans " + where + " : " + e);
            try
            {
                if (MapScreen.Current != null)
                    MapScreen.Current.CloseScreen();
            }
            catch (Exception ex)
            {
                HolomapPlugin.Caught("HolomapPlugin.Fault", ex);
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
            catch (Exception ex)
            {
                HolomapPlugin.Caught("HolomapPlugin.Notify", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                RenderHooks.Release();
                // Avant de quitter le monde : ce qui attend part si un serveur à
                // nous est encore là.
                PointsDeFonctionnalites.Arreter(m_world);
                ErrorReporting.Stop(m_world);
                if (m_world != null)
                    m_world.Leave();
                ServerJoiner.Reset();
                if (m_servers != null)
                    m_servers.Stop();
                GameTextures.DestroyAll();
                // Seulement nos correctifs, jamais ceux d'un autre.
                if (m_harmony != null)
                    m_harmony.UnpatchAll(Id);
            }
            catch (Exception e)
            {
                LogCaught("arrêt incomplet : " + e.Message, e);
            }
            m_stopped = true;
            Instance = null;
        }
    }
}
