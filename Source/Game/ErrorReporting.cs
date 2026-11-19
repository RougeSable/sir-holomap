using System;
using System.Reflection;
using Sandbox.Engine.Networking;
using Sandbox.ModAPI;
using VRage.FileSystem;

namespace SirHolomap
{
    // Le côté jeu des rapports d'erreurs (la règle des greffons côté
    // joueur) : les erreurs du greffon et le plantage de la dernière session sont
    // écrits dans son dossier, et partent vers nos serveurs (PU, PTU) tant que le
    // joueur est sur l'un d'eux. Partout ailleurs ils attendent. Tout ce qui va mal
    // ici reste ici : le jeu ne le voit jamais.
    internal sealed class ErrorReporting
    {
        // Mises à jour entre deux écritures de ce qui a changé, et entre deux regards
        // pour savoir si un serveur à nous écoute (60 mises à jour par seconde).
        private const int FlushInterval = 300;
        private const int PumpInterval = 600;

        private static ErrorReporting s_current;

        private readonly string m_folder;
        private readonly DateTime m_startUtc = DateTime.UtcNow;
        private ErrorReporter m_reporter;
        private int m_counter;

        private ErrorReporting(string folder)
        {
            m_folder = folder;
        }

        // Démarre les rapports de cette session : pose la marque qui fait reconnaître un
        // plantage au démarrage suivant, et fait un rapport du plantage de la dernière
        // session si elle a laissé sa marque.
        public static void Start(string folder, ServerHistory history)
        {
            try
            {
                var reporting = new ErrorReporting(folder);
                reporting.Begin(history);
                s_current = reporting;
            }
            catch (Exception)
            {
            }
        }

        // Une ligne que le greffon a écrite au journal du jeu.
        public static void Note(string line)
        {
            var current = s_current;
            if (current != null && current.m_reporter != null)
                current.m_reporter.Note(line);
        }

        // Une ligne que le greffon a écrite au journal du jeu comme une erreur.
        public static void Error(string text)
        {
            var current = s_current;
            if (current != null && current.m_reporter != null)
                current.m_reporter.ErrorLine(text, DateTime.UtcNow);
        }

        // Une exception que le greffon a rattrapée.
        public static void Caught(string where, Exception e)
        {
            var current = s_current;
            if (current != null && current.m_reporter != null)
                current.m_reporter.Caught(where, e, DateTime.UtcNow);
        }

        // Une exception que le greffon a rattrapée, avec la ligne qu'il a écrite à son sujet.
        public static void CaughtLine(string line, Exception e)
        {
            var current = s_current;
            if (current != null && current.m_reporter != null)
                current.m_reporter.CaughtLine(line, e, DateTime.UtcNow);
        }

        private void Begin(ServerHistory history)
        {
            var version = typeof(ErrorReporting).Assembly.GetName().Version.ToString();
            var scrubber = new ReportScrubber();
            Prepare(scrubber, history);
            m_reporter = new ErrorReporter(m_folder, version, m_startUtc, scrubber);
            Faults.CaughtHandler = Caught;

            DateTime? previous = null;
            try
            {
                previous = CrashMarker.Begin(m_folder, m_startUtc);
            }
            catch (Exception)
            {
            }
            if (previous.HasValue)
                ReportCrash(previous.Value, version, scrubber);
        }

        // Ce qui ne doit jamais être dans un rapport, pour autant qu'on le sache au démarrage.
        private static void Prepare(ReportScrubber scrubber, ServerHistory history)
        {
            try
            {
                scrubber.AddPrivate(Environment.UserName);
                scrubber.AddUserFolder(Environment.GetEnvironmentVariable("USERPROFILE"));
                scrubber.AddUserFolder(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                AddSteamAccount(scrubber);
                if (history != null)
                {
                    foreach (var visit in history.Servers)
                        AddServer(scrubber, visit.Name, visit.Address);
                }
            }
            catch (Exception)
            {
            }
        }

        // Le nom et l'identifiant Steam du joueur, tels que les services du jeu les
        // connaissent.
        private static void AddSteamAccount(ReportScrubber scrubber)
        {
            try
            {
                var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var name = typeof(MyGameService).GetProperty("UserName", flags);
                if (name != null)
                    scrubber.AddPrivate(name.GetValue(null, null) as string);
                var id = typeof(MyGameService).GetProperty("UserId", flags);
                if (id != null)
                {
                    var value = id.GetValue(null, null);
                    if (value != null)
                        scrubber.AddPrivate(value.ToString());
                }
            }
            catch (Exception)
            {
            }
        }

        private static void AddServer(ReportScrubber scrubber, string name, string address)
        {
            scrubber.AddPrivate(name);
            if (string.IsNullOrEmpty(address))
                return;
            scrubber.AddPrivate(address);
            var normalized = ServerAddress.Normalize(address);
            scrubber.AddPrivate(normalized);
            var colon = normalized.LastIndexOf(':');
            if (colon > 0)
                scrubber.AddPrivate(normalized.Substring(0, colon));
        }

        // Le jeu ne s'est pas terminé proprement la dernière fois : son journal, relu, fait
        // un rapport qui attend un serveur à nous.
        private void ReportCrash(DateTime previousStartUtc, string version, ReportScrubber scrubber)
        {
            try
            {
                var logs = GameLogFiles.List(MyFileSystem.UserDataPath);
                var log = GameLogFiles.Pick(logs, previousStartUtc);
                var text = "";
                var crashedAt = previousStartUtc;
                if (log != null)
                {
                    text = GameLogFiles.ReadTail(log.Path, GameLogFiles.TailBytes);
                    crashedAt = log.LastWriteUtc;
                }
                var report = GameLog.BuildCrashReport(text, version, previousStartUtc, crashedAt, scrubber);
                m_reporter.Enqueue(report);
            }
            catch (Exception)
            {
            }
        }

        // À chaque mise à jour, depuis celle du greffon.
        public static void Tick(MapWorld world)
        {
            var current = s_current;
            if (current != null)
                current.Step(world);
        }

        private void Step(MapWorld world)
        {
            try
            {
                if (m_reporter == null)
                    return;
                m_counter++;
                if (m_counter % FlushInterval == 0)
                    m_reporter.Flush();
                if (m_counter % PumpInterval == 0)
                {
                    Learn(world);
                    m_reporter.Pump(OnOurServer(world), Send, DateTime.UtcNow, false);
                }
            }
            catch (Exception)
            {
            }
        }

        // La session se termine proprement : ce qui attend part si un serveur à nous
        // est là, et la marque est retirée, pour que le démarrage suivant
        // ne prenne pas cette session pour un plantage.
        public static void Stop(MapWorld world)
        {
            var current = s_current;
            if (current == null)
                return;
            s_current = null;
            Faults.CaughtHandler = null;
            try
            {
                if (current.m_reporter != null)
                {
                    current.Learn(world);
                    current.m_reporter.Pump(OnOurServer(world), Send, DateTime.UtcNow, true);
                }
            }
            catch (Exception)
            {
            }
            try
            {
                CrashMarker.End(current.m_folder);
            }
            catch (Exception)
            {
            }
        }

        private void Learn(MapWorld world)
        {
            try
            {
                var scrubber = m_reporter.Scrubber;
                if (world == null)
                    return;
                var identity = world.Identity;
                if (identity != null)
                    AddServer(scrubber, identity.Name, identity.ConnectionString);
                var multiplayer = MyAPIGateway.Multiplayer;
                if (multiplayer != null)
                {
                    scrubber.AddPrivate(multiplayer.MyName);
                    scrubber.AddPrivate(multiplayer.MyId.ToString());
                }
                var session = MyAPIGateway.Session;
                if (session != null && session.Player != null)
                {
                    scrubber.AddPrivate(session.Player.DisplayName);
                    scrubber.AddPrivate(session.Player.SteamUserId.ToString());
                }
                foreach (var marker in world.Markers)
                {
                    if (marker.Name != null && marker.Name.Length >= 4)
                        scrubber.AddPrivate(marker.Name);
                }
            }
            catch (Exception)
            {
            }
        }

        // Le joueur est sur un serveur à nous, qui écoute sur le canal.
        internal static bool OnOurServer(MapWorld world)
        {
            try
            {
                if (world == null || !world.InWorld || world.Identity == null)
                    return false;
                var multiplayer = MyAPIGateway.Multiplayer;
                if (multiplayer == null || !multiplayer.MultiplayerActive || multiplayer.IsServer)
                    return false;
                return OurServers.IsOurs(world.Identity.Name, world.Identity.IsDedicated);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Send(byte[] payload)
        {
            try
            {
                var multiplayer = MyAPIGateway.Multiplayer;
                return multiplayer != null && multiplayer.SendMessageToServer(ReportRule.ModApiChannel, payload, true);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
