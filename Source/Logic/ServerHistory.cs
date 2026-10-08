using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SirHolomap
{
    // A server the player has played on.
    public sealed class VisitedServer
    {
        public string Key = "";
        public string Name = "";
        public string Address = "";
        public DateTime LastVisitUtc;
        public int Visits;
    }

    // The servers the player has visited, kept on the player's machine across
    // game launches. It feeds the galaxy tab ("last visit 12 days ago") and its
    // "Visited" filter.
    public sealed class ServerHistory
    {
        public const string FileName = "servers.txt";
        public const string Header = "sir-holomap servers 1";

        private readonly Dictionary<string, VisitedServer> m_servers = new Dictionary<string, VisitedServer>(StringComparer.Ordinal);

        public IEnumerable<VisitedServer> Servers
        {
            get { return m_servers.Values; }
        }

        public int Count
        {
            get { return m_servers.Count; }
        }

        public VisitedServer Find(string key)
        {
            VisitedServer server;
            return key != null && m_servers.TryGetValue(key, out server) ? server : null;
        }

        public VisitedServer FindByAddress(string address)
        {
            var wanted = ServerAddress.Normalize(address);
            if (wanted.Length == 0)
                return null;
            foreach (var server in m_servers.Values)
            {
                if (ServerAddress.Normalize(server.Address) == wanted)
                    return server;
            }
            return null;
        }

        public VisitedServer RecordVisit(string key, string name, string address, DateTime nowUtc)
        {
            VisitedServer server;
            if (!m_servers.TryGetValue(key, out server))
            {
                server = new VisitedServer { Key = key };
                m_servers.Add(key, server);
            }
            if (!string.IsNullOrEmpty(name))
                server.Name = Clean(name);
            if (!string.IsNullOrEmpty(address))
                server.Address = Clean(address);
            server.LastVisitUtc = nowUtc;
            server.Visits++;
            return server;
        }

        private static string Clean(string text)
        {
            return (text ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        public string Save()
        {
            var text = new StringBuilder();
            text.Append(Header).Append('\n');
            foreach (var s in m_servers.Values)
            {
                text.Append(Clean(s.Key)).Append('\t')
                    .Append(Clean(s.Address)).Append('\t')
                    .Append(s.LastVisitUtc.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(s.Visits.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(Clean(s.Name)).Append('\n');
            }
            return text.ToString();
        }

        public static ServerHistory Load(string text)
        {
            var history = new ServerHistory();
            if (string.IsNullOrEmpty(text))
                return history;
            var lines = text.Split('\n');
            if (lines[0].Trim() != Header)
                return history;
            for (var i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].TrimEnd('\r').Split('\t');
                if (parts.Length < 5 || parts[0].Length == 0)
                    continue;
                long ticks;
                int visits;
                if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)
                    || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                    continue;
                int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out visits);
                history.m_servers[parts[0]] = new VisitedServer
                {
                    Key = parts[0],
                    Address = parts[1],
                    LastVisitUtc = new DateTime(ticks, DateTimeKind.Utc),
                    Visits = visits,
                    Name = parts[4],
                };
            }
            return history;
        }

        public static ServerHistory LoadFrom(string directory)
        {
            try
            {
                var path = Path.Combine(directory, FileName);
                return File.Exists(path) ? Load(File.ReadAllText(path, Encoding.UTF8)) : new ServerHistory();
            }
            catch (Exception)
            {
                return new ServerHistory();
            }
        }

        public void SaveTo(string directory)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, FileName);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, Save(), new UTF8Encoding(false));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }
    }
}
