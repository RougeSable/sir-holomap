using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SirHolomap
{
    // Nothing about the player leaves the machine in a report: no Steam
    // identifier, no nickname, no grid name, no server address. A line that
    // carries one is dropped; the Windows user's folder becomes
    // %USERPROFILE%.
    public sealed class ReportScrubber
    {
        public const string UserProfile = "%USERPROFILE%";

        // What stands in for a message that had to be dropped.
        public const string Removed = "(line removed: it carried data about the player)";

        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // C:\Users\Someone, whatever the drive or the slashes.
        private static readonly Regex UserFolder = new Regex(@"[A-Za-z]:[\\/]+Users[\\/]+[^\\/""'<>|:*?\r\n]+", Options);

        // SteamID64 and the other shapes of a Steam identifier.
        private static readonly Regex SteamIdentifier = new Regex(
            @"\b7656119\d{10}\b|\b\d{17}\b|\bSTEAM_\d:\d:\d+\b|\[U:\d:\d+\]|steam://|\bsteam\s*_?id\b", Options);

        // host:port, whatever the host: a server.
        private static readonly Regex HostAndPort = new Regex(
            @"\b(?:\d{1,3}\.){3}\d{1,3}:\d{2,5}\b|\b[a-z0-9][a-z0-9-]*(?:\.[a-z0-9-]+)+:\d{2,5}\b", Options);

        // A bare IPv4 address only counts where the line talks about a
        // connection: version numbers have four parts too.
        private static readonly Regex BareAddress = new Regex(@"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])", Options);
        private static readonly Regex ConnectionWords = new Regex(
            @"\b(?:ip|address|addr|server|host|connect(?:ion|ed|ing)?|endpoint|remote|lobby|socket|udp|tcp|join(?:ed|ing)?)\b|connection\s*string", Options);

        // "Player name: ...", "Grid = ...", "Owner: ...": what follows is
        // somebody's.
        private static readonly Regex NamedThing = new Regex(
            @"\b(?:player|user|username|display|nick|nickname|persona|gamertag|owner|author|character|pilot|identity|faction|grid|cubegrid|ship|station|entity|block|world|session|server|host)\w*\s*(?:name|id)?\s*[:=]\s*\S", Options);

        // "Player Someone joined": the name sits between a role and what it did.
        private static readonly Regex PlayerEvent = new Regex(
            @"\b(?:player|user|client|pilot)\s+\S+\s+(?:joined|left|connected|disconnected|died|killed|respawned|logged|spawned)", Options);

        // A name between quotes after a word that says whose it is.
        private static readonly Regex QuotedName = new Regex(
            "\\b(?:player|user|grid|cubegrid|ship|station|entity|identity|character|faction|owner|pilot)\\b[^'\"\u2018\u2019\u201C\u201D\\r\\n]{0,40}['\"\u2018\u201C][^'\"\u2019\u201D\\r\\n]+['\"\u2019\u201D]", Options);

        private readonly object m_lock = new object();
        private readonly List<Regex> m_private = new List<Regex>();
        private readonly List<string> m_userFolders = new List<string>();
        private readonly HashSet<string> m_known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ReportScrubber()
        {
        }

        // A word that must not survive in a report: a nickname, a grid, a
        // server, the Windows user. Whole words, whatever the case.
        public void AddPrivate(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return;
            word = word.Trim();
            if (word.Length < 2)
                return;
            lock (m_lock)
            {
                if (m_known.Count >= 2000 || !m_known.Add(word))
                    return;
                m_private.Add(new Regex(@"(?<![\w])" + Regex.Escape(word) + @"(?![\w])", Options));
            }
        }

        // The folder of the Windows user as the machine spells it
        // (C:\Users\Someone), replaced wherever it appears.
        public void AddUserFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;
            lock (m_lock)
            {
                m_userFolders.Add(folder.Trim().TrimEnd('\\', '/'));
            }
        }

        // The line cleaned, or null when it carries data about the player
        // and has to go.
        public string Clean(string line)
        {
            if (line == null)
                return null;
            lock (m_lock)
            {
                var text = line;
                foreach (var folder in m_userFolders)
                {
                    if (folder.Length > 0)
                        text = Regex.Replace(text, Regex.Escape(folder), UserProfile, Options);
                }
                text = UserFolder.Replace(text, UserProfile);

                if (SteamIdentifier.IsMatch(text) || HostAndPort.IsMatch(text))
                    return null;
                if (BareAddress.IsMatch(text) && ConnectionWords.IsMatch(text))
                    return null;
                if (NamedThing.IsMatch(text) || QuotedName.IsMatch(text) || PlayerEvent.IsMatch(text))
                    return null;
                foreach (var word in m_private)
                {
                    if (word.IsMatch(text))
                        return null;
                }
                return text;
            }
        }

        // The lines that stay, in order.
        public List<string> CleanLines(IEnumerable<string> lines)
        {
            var kept = new List<string>();
            if (lines == null)
                return kept;
            foreach (var line in lines)
            {
                var clean = Clean(line);
                if (clean != null)
                    kept.Add(clean);
            }
            return kept;
        }

        // A single text (a message) cleaned: the placeholder when it had to
        // go.
        public string CleanMessage(string text)
        {
            return Clean(text) ?? Removed;
        }
    }
}
