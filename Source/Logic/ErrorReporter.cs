using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SirHolomap
{
    // The reports waiting to leave: one file each in the plugin's folder,
    // until they are sent to one of our servers.
    public sealed class ReportQueue
    {
        public const string FolderName = "reports";
        public const string Extension = ".json";
        public const int MaximumFiles = 100;

        private readonly string m_directory;

        public ReportQueue(string pluginFolder)
        {
            m_directory = Path.Combine(pluginFolder, FolderName);
        }

        public string Directory
        {
            get { return m_directory; }
        }

        public string PathOf(string id)
        {
            return Path.Combine(m_directory, id + Extension);
        }

        // Written next to the file, then swapped in: a crash never leaves a
        // half-written report.
        public void Save(string id, ErrorReport report)
        {
            System.IO.Directory.CreateDirectory(m_directory);
            var path = PathOf(id);
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, ErrorReportJson.ToBytes(report));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
            Prune();
        }

        // The ids of the reports waiting, oldest first.
        public List<string> Waiting()
        {
            var ids = new List<string>();
            if (!System.IO.Directory.Exists(m_directory))
                return ids;
            var files = new List<FileInfo>(new DirectoryInfo(m_directory).GetFiles("*" + Extension));
            files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
            foreach (var file in files)
                ids.Add(Path.GetFileNameWithoutExtension(file.Name));
            return ids;
        }

        public byte[] Read(string id)
        {
            return File.ReadAllBytes(PathOf(id));
        }

        public void Remove(string id)
        {
            var path = PathOf(id);
            if (File.Exists(path))
                File.Delete(path);
        }

        // A player who never meets our servers keeps a bounded folder.
        private void Prune()
        {
            var ids = Waiting();
            for (var i = 0; i < ids.Count - MaximumFiles; i++)
                Remove(ids[i]);
        }
    }

    // Where the plugin's caught exceptions and error lines go: the library
    // code reports them here, and the game side listens.
    public static class Faults
    {
        public static Action<string, Exception> CaughtHandler;

        public static void Caught(string where, Exception e)
        {
            try
            {
                var handler = CaughtHandler;
                if (handler != null && e != null)
                    handler(where, e);
            }
            catch (Exception)
            {
            }
        }
    }

    // Collects what goes wrong in this session and sends it to our servers.
    // The same error (same message, same top of stack) is one report for the
    // whole session, with how many times it happened and when first and
    // last. Nothing here ever throws: a report that cannot be prepared or
    // sent is lost, the game goes on.
    public sealed class ErrorReporter
    {
        // An error that has not come back for this long is settled: it can
        // leave without waiting for the end of the session.
        public const int QuietSeconds = 120;

        public const int MaximumEntries = 200;

        private const string NoStack = "";

        private sealed class Entry
        {
            public string Id;
            public ErrorReport Report;
            public bool Dirty;
            public bool Sent;
        }

        private readonly object m_lock = new object();
        private readonly string m_version;
        private readonly DateTime m_sessionUtc;
        private readonly string m_stamp;
        private readonly ReportScrubber m_scrubber;
        private readonly ReportQueue m_queue;
        private readonly Dictionary<string, Entry> m_entries = new Dictionary<string, Entry>();
        private readonly Dictionary<string, Entry> m_byId = new Dictionary<string, Entry>();
        // The same errors by their raw text: an error that comes back every
        // frame is recognised without being cleaned again.
        private readonly Dictionary<string, Entry> m_raw = new Dictionary<string, Entry>();
        private readonly List<string> m_recent = new List<string>();

        public ErrorReporter(string pluginFolder, string version, DateTime sessionStartUtc, ReportScrubber scrubber)
        {
            m_version = version ?? "";
            m_sessionUtc = sessionStartUtc;
            m_stamp = sessionStartUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            m_scrubber = scrubber ?? new ReportScrubber();
            m_queue = new ReportQueue(pluginFolder);
        }

        public ReportQueue Queue
        {
            get { return m_queue; }
        }

        public ReportScrubber Scrubber
        {
            get { return m_scrubber; }
        }

        // How many different errors this session has seen.
        public int Kinds
        {
            get
            {
                lock (m_lock)
                    return m_entries.Count;
            }
        }

        // A line the plugin wrote to the game's log, error or not: the
        // reports carry the last ones.
        public void Note(string line)
        {
            try
            {
                lock (m_lock)
                {
                    var clean = m_scrubber.Clean(line);
                    if (clean == null)
                        return;
                    m_recent.Add(clean);
                    if (m_recent.Count > ReportRule.TailLines)
                        m_recent.RemoveAt(0);
                }
            }
            catch (Exception)
            {
            }
        }

        // An exception the plugin caught.
        public void Caught(string where, Exception e, DateTime nowUtc)
        {
            try
            {
                if (e == null)
                    return;
                var text = e.GetType().FullName + ": " + e.Message;
                Record(string.IsNullOrEmpty(where) ? text : where + ": " + text, e.StackTrace, nowUtc);
            }
            catch (Exception)
            {
            }
        }

        // An exception the plugin caught, with the line it wrote about it to
        // the game's log: the line is the message, the exception gives the
        // stack.
        public void CaughtLine(string line, Exception e, DateTime nowUtc)
        {
            try
            {
                if (e == null)
                    ErrorLine(line, nowUtc);
                else
                    Record(line, e.StackTrace, nowUtc);
            }
            catch (Exception)
            {
            }
        }

        // An error line the plugin wrote to the game's log.
        public void ErrorLine(string text, DateTime nowUtc)
        {
            try
            {
                Record(text, NoStack, nowUtc);
            }
            catch (Exception)
            {
            }
        }

        private void Record(string message, string stack, DateTime nowUtc)
        {
            lock (m_lock)
            {
                var stackLines = GameLog.Split(stack);
                var rawKey = (message ?? "") + "\n" + (stackLines.Count > 0 ? stackLines[0].Trim() : NoStack);
                Entry entry;
                if (m_raw.TryGetValue(rawKey, out entry))
                {
                    Again(entry, nowUtc);
                    return;
                }

                var cleanMessage = CleanText(message);
                var cleanStack = m_scrubber.CleanLines(stackLines);
                var top = cleanStack.Count > 0 ? cleanStack[0].Trim() : NoStack;
                var key = cleanMessage + "\n" + top;

                if (m_entries.TryGetValue(key, out entry))
                {
                    m_raw[rawKey] = entry;
                    Again(entry, nowUtc);
                    return;
                }
                if (m_entries.Count >= MaximumEntries)
                    return;

                var report = new ErrorReport
                {
                    Version = m_version,
                    Kind = ReportRule.KindError,
                    Message = cleanMessage,
                    Stack = string.Join("\n", cleanStack.ToArray()),
                    FirstUtc = nowUtc,
                    LastUtc = nowUtc,
                    Count = 1,
                    SessionUtc = m_sessionUtc,
                };
                report.Lines.AddRange(m_recent);
                entry = new Entry
                {
                    Id = m_stamp + "-" + Hashing.Fnv1a64(key).ToString("x16", CultureInfo.InvariantCulture),
                    Report = report,
                    Dirty = true,
                };
                m_entries[key] = entry;
                m_byId[entry.Id] = entry;
                m_raw[rawKey] = entry;
            }
        }

        private static void Again(Entry entry, DateTime nowUtc)
        {
            entry.Report.Count++;
            entry.Report.LastUtc = nowUtc;
            // Once an error has left, the session has said it.
            entry.Dirty = !entry.Sent;
        }

        // A message on one line, without anything about the player.
        private string CleanText(string text)
        {
            var kept = new List<string>();
            foreach (var line in GameLog.Split(text ?? ""))
            {
                var clean = m_scrubber.Clean(line);
                if (clean != null && clean.Trim().Length > 0)
                    kept.Add(clean.Trim());
            }
            return kept.Count > 0 ? string.Join(" | ", kept.ToArray()) : ReportScrubber.Removed;
        }

        // A report built elsewhere (the crash of the last session) joins
        // the queue.
        public void Enqueue(ErrorReport report)
        {
            try
            {
                lock (m_lock)
                    m_queue.Save("crash-" + report.SessionUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture), report);
            }
            catch (Exception)
            {
            }
        }

        // What changed since the last time goes to the plugin's folder, so
        // that a crash does not take it away.
        public void Flush()
        {
            try
            {
                lock (m_lock)
                    FlushLocked();
            }
            catch (Exception)
            {
            }
        }

        private void FlushLocked()
        {
            foreach (var entry in m_entries.Values)
            {
                if (!entry.Dirty)
                    continue;
                entry.Dirty = false;
                try
                {
                    m_queue.Save(entry.Id, entry.Report);
                }
                catch (Exception)
                {
                    entry.Dirty = true;
                }
            }
        }

        // Called now and then, and once more when the session ends. Outside
        // our servers (onOurServer false) nothing leaves: the reports wait in
        // the plugin's folder for the next connection. Returns how many
        // left.
        public int Pump(bool onOurServer, Func<byte[], bool> send, DateTime nowUtc, bool endOfSession)
        {
            try
            {
                lock (m_lock)
                {
                    FlushLocked();
                    if (!onOurServer || send == null)
                        return 0;
                    var sent = 0;
                    foreach (var id in m_queue.Waiting())
                    {
                        Entry live;
                        m_byId.TryGetValue(id, out live);
                        // An error of this session that may still come back
                        // waits, so that it leaves once, with its count.
                        if (live != null && !endOfSession && (nowUtc - live.Report.LastUtc).TotalSeconds < QuietSeconds)
                            continue;
                        if (live != null && live.Sent)
                            continue;
                        var done = false;
                        try
                        {
                            done = send(m_queue.Read(id));
                        }
                        catch (Exception)
                        {
                        }
                        if (!done)
                            break;
                        if (live != null)
                            live.Sent = true;
                        try
                        {
                            m_queue.Remove(id);
                        }
                        catch (Exception)
                        {
                        }
                        sent++;
                    }
                    return sent;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
