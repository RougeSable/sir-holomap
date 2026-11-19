using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SirHolomap
{
    // Rien sur le joueur ne quitte la machine dans un rapport : ni identifiant Steam,
    // ni pseudo, ni nom de grille, ni adresse de serveur. Une ligne qui
    // en porte une est retirée ; le dossier de l'utilisateur Windows devient
    // %USERPROFILE%.
    public sealed class ReportScrubber
    {
        public const string UserProfile = "%USERPROFILE%";

        // Ce qui remplace un message qu'il a fallu retirer.
        public const string Removed = "(ligne retirée : elle portait des données sur le joueur)";

        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // Un dossier d'utilisateur Windows, quels que soient le disque ou les barres obliques.
        private static readonly Regex UserFolder = new Regex(@"[A-Za-z]:[\\/]+Users[\\/]+[^\\/""'<>|:*?\r\n]+", Options);

        // SteamID64 et les autres formes d'un identifiant Steam.
        private static readonly Regex SteamIdentifier = new Regex(
            @"\b7656119\d{10}\b|\b\d{17}\b|\bSTEAM_\d:\d:\d+\b|\[U:\d:\d+\]|steam://|\bsteam\s*_?id\b", Options);

        // hôte:port, quel que soit l'hôte : un serveur.
        private static readonly Regex HostAndPort = new Regex(
            @"\b(?:\d{1,3}\.){3}\d{1,3}:\d{2,5}\b|\b[a-z0-9][a-z0-9-]*(?:\.[a-z0-9-]+)+:\d{2,5}\b", Options);

        // Une adresse IPv4 nue ne compte que là où la ligne parle d'une
        // connexion : les numéros de version ont aussi quatre parties.
        private static readonly Regex BareAddress = new Regex(@"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])", Options);
        private static readonly Regex ConnectionWords = new Regex(
            @"\b(?:ip|address|addr|server|host|connect(?:ion|ed|ing)?|endpoint|remote|lobby|socket|udp|tcp|join(?:ed|ing)?)\b|connection\s*string", Options);

        // « Player name: ... », « Grid = ... », « Owner: ... » : ce qui suit est
        // à quelqu'un.
        private static readonly Regex NamedThing = new Regex(
            @"\b(?:player|user|username|display|nick|nickname|persona|gamertag|owner|author|character|pilot|identity|faction|grid|cubegrid|ship|station|entity|block|world|session|server|host)\w*\s*(?:name|id)?\s*[:=]\s*\S", Options);

        // « Player Untel joined » : le nom est entre un rôle et ce qu'il a fait.
        private static readonly Regex PlayerEvent = new Regex(
            @"\b(?:player|user|client|pilot)\s+\S+\s+(?:joined|left|connected|disconnected|died|killed|respawned|logged|spawned)", Options);

        // Un nom entre guillemets après un mot qui dit à qui il est.
        private static readonly Regex QuotedName = new Regex(
            "\\b(?:player|user|grid|cubegrid|ship|station|entity|identity|character|faction|owner|pilot)\\b[^'\"\u2018\u2019\u201C\u201D\\r\\n]{0,40}['\"\u2018\u201C][^'\"\u2019\u201D\\r\\n]+['\"\u2019\u201D]", Options);

        private readonly object m_lock = new object();
        private readonly List<Regex> m_private = new List<Regex>();
        private readonly List<string> m_userFolders = new List<string>();
        private readonly HashSet<string> m_known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ReportScrubber()
        {
        }

        // Un mot qui ne doit pas survivre dans un rapport : un pseudo, une grille, un
        // serveur, l'utilisateur Windows. Mots entiers, quelle que soit la casse.
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

        // Le dossier de l'utilisateur Windows tel que la machine l'écrit,
        // remplacé partout où il apparaît.
        public void AddUserFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;
            lock (m_lock)
            {
                m_userFolders.Add(folder.Trim().TrimEnd('\\', '/'));
            }
        }

        // La ligne nettoyée, ou null quand elle porte des données sur le joueur
        // et doit partir.
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

        // Les lignes qui restent, dans l'ordre.
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

        // Un texte seul (un message) nettoyé : le texte de remplacement quand il a fallu
        // le retirer.
        public string CleanMessage(string text)
        {
            return Clean(text) ?? Removed;
        }
    }
}
