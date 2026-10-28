using System;
using System.Reflection;
using Sandbox.Engine.Networking;
using Sandbox.ModAPI;
using VRage.FileSystem;

namespace SirHolomap
{
    // The game side of the error reports (the rule of the client-side
    // plugins): the plugin's errors and the crash of the last session are
    // written to its folder, and leave for our servers (PU, PTU) while the
    // player is on one. Everywhere else they wait. Whatever goes wrong in
    // here stays in here: the game never sees it.
    internal sealed class ErrorReporting
    {
        // Updates between two writes of what changed, and between two looks
        // at whether a server of ours is listening (60 updates per second).
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

        // Starts the reports of this session: lays the mark that tells a
        // crash at the next start, and turns the last session's crash into a
        // report if it left its mark behind.
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

        // A line the plugin wrote to the game's log.
        public static void Note(string line)
        {
            var current = s_current;
            if (current != null && current.m_reporter != null)
                current.m_reporter.Note(line);
        }

        // A line the plugin wrote to the game's log as an error.
        public static void Error(string text)
        {
            var current = s_current;
            if (current != null && current.m_reporter != null)
                current.m_reporter.ErrorLine(text, DateTime.UtcNow);
        }

        // An exception the plugin caught.
        public static void Caught(string where, Exception e)
        {
            var current = s_current;
            if (current != null && current.m_reporter != null)
                current.m_reporter.Caught(where, e, DateTime.UtcNow);
        }

        // An exception the plugin caught, with the line it wrote about it.
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

        // What must never be in a report, as far as it is known at the start.
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

        // The player's Steam name and identifier, as the game services know
        // them.
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

        // The game did not end cleanly last time: its log, read again, makes
        // a report that waits for a server of ours.
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

        // Every update, from the plugin's own.
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

        // The session ends cleanly: what is waiting leaves if a server of ours
        // is there, and the mark is taken away, so that the next start does
        // not take this session for a crash.
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

        // The player is on a server of ours, which listens on the channel.
        private static bool OnOurServer(MapWorld world)
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
