using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SirHolomap
{
    // Les rapports qui attendent de partir : un fichier chacun dans le dossier du greffon,
    // jusqu'à leur envoi à l'un de nos serveurs.
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

        // Écrit à côté du fichier, puis mis à sa place : un plantage ne laisse jamais un
        // rapport à moitié écrit.
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

        // Les identifiants des rapports en attente, le plus ancien en premier.
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

        // Un joueur qui ne croise jamais nos serveurs garde un dossier borné.
        private void Prune()
        {
            var ids = Waiting();
            for (var i = 0; i < ids.Count - MaximumFiles; i++)
                Remove(ids[i]);
        }
    }

    // Où vont les exceptions rattrapées et les lignes d'erreur du greffon : le code
    // de la bibliothèque les rapporte ici, et le côté jeu écoute.
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

    // Rassemble ce qui va mal dans cette session et l'envoie à nos serveurs.
    // La même erreur (même message, même haut de pile) fait un seul rapport pour
    // toute la session, avec le nombre de fois où elle est arrivée et sa première et sa
    // dernière heure. Rien ici ne lève jamais d'exception : un rapport qu'on ne peut pas préparer ou
    // envoyer est perdu, le jeu continue.
    public sealed class ErrorReporter
    {
        // Une erreur qui n'est pas revenue depuis ce temps est réglée : elle peut
        // partir sans attendre la fin de la session.
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
        // Les mêmes erreurs par leur texte brut : une erreur qui revient à chaque
        // image est reconnue sans être nettoyée de nouveau.
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

        // Combien d'erreurs différentes cette session a vues.
        public int Kinds
        {
            get
            {
                lock (m_lock)
                    return m_entries.Count;
            }
        }

        // Une ligne que le greffon a écrite au journal du jeu, erreur ou non : les
        // rapports portent les dernières.
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

        // Une exception que le greffon a rattrapée.
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

        // Une exception que le greffon a rattrapée, avec la ligne qu'il a écrite à son sujet dans
        // le journal du jeu : la ligne est le message, l'exception donne la
        // pile.
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

        // Une ligne d'erreur que le greffon a écrite au journal du jeu.
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
            // Une fois qu'une erreur est partie, la session l'a dite.
            entry.Dirty = !entry.Sent;
        }

        // Un message sur une ligne, sans rien sur le joueur.
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

        // Un rapport construit ailleurs (le plantage de la dernière session) rejoint
        // la file.
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

        // Ce qui a changé depuis la dernière fois va dans le dossier du greffon, pour
        // qu'un plantage ne l'emporte pas.
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

        // Appelé de temps en temps, et une fois de plus à la fin de la session. Hors de
        // nos serveurs (onOurServer faux) rien ne part : les rapports attendent dans
        // le dossier du greffon la prochaine connexion. Rend le nombre de rapports
        // partis.
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
                        // Une erreur de cette session qui peut encore revenir
                        // attend, pour partir une seule fois, avec son compte.
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
