using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SirHolomap
{
    public enum ContactKind
    {
        Station = 0,
        LargeShip = 1,
        SmallShip = 2,
        Character = 3,
    }

    public enum ContactRelation
    {
        Unowned = 0,
        Own = 1,
        Faction = 2,
        Neutral = 3,
        Enemy = 4,
    }

    // Ce que le joueur voit d'une grille ou d'un personnage à un instant.
    public struct Sighting
    {
        public long Id;
        public string Name;
        public ContactKind Kind;
        public ContactRelation Relation;
        public int Blocks;
        public double Radius;
        public Vec3 Position;
        public string Body;
    }

    // Une chose retenue. Vivante tant que le jeu l'envoie au joueur, un
    // souvenir ensuite : elle reste alors à sa dernière position connue, avec l'heure
    // où elle a été vue pour la dernière fois.
    public sealed class MemoryContact
    {
        public long Id;
        public string Name = "";
        public ContactKind Kind;
        public ContactRelation Relation;
        public int Blocks;
        public double Radius;
        public Vec3 Position;
        public DateTime LastSeenUtc;

        // Vide quand le contact n'est pas près d'une planète.
        public string Body = "";

        // État d'exécution, jamais enregistré : un souvenir chargé du disque n'est jamais vivant.
        public bool IsLive;
        public DateTime? AbsentSinceUtc;
    }

    // La mémoire personnelle d'un serveur. Le jeu n'envoie rien au-delà de la portée de
    // synchronisation du serveur : tout ce qui est entré dans cette portée a été
    // vu, sa position et l'heure sont gardées ; tout ce qui en est sorti reste où
    // il a été vu en dernier (les grilles du joueur comprises, le jeu n'en dit rien
    // de plus). Revenir près d'un lieu le met à jour : ce qui s'y trouve est
    // vu de nouveau, ce qui n'y est plus est oublié.
    public sealed class MapMemory
    {
        public const string Header = "sir-holomap memory 1";

        // Un lieu retenu ne compte comme vérifié que bien à l'intérieur de la
        // portée de synchronisation : à sa limite, le jeu envoie encore.
        public const double ConfirmFraction = 0.75;

        // Combien de temps un contact retenu doit manquer à un lieu vérifié
        // avant d'être oublié.
        public static readonly TimeSpan ForgetAfter = TimeSpan.FromSeconds(20);

        // Au-delà, les plus vieux souvenirs partent en premier.
        public const int MaximumContacts = 20000;

        private readonly Dictionary<long, MemoryContact> m_contacts = new Dictionary<long, MemoryContact>();
        private readonly HashSet<long> m_seen = new HashSet<long>();
        private readonly List<long> m_forget = new List<long>();

        public MapMemory(string serverKey)
        {
            ServerKey = serverKey ?? "";
        }

        public string ServerKey { get; private set; }

        // Vrai quand quelque chose qui vaut d'être enregistré a changé depuis le dernier enregistrement.
        public bool Dirty { get; set; }

        public int Count
        {
            get { return m_contacts.Count; }
        }

        public IEnumerable<MemoryContact> Contacts
        {
            get { return m_contacts.Values; }
        }

        public bool TryGet(long id, out MemoryContact contact)
        {
            return m_contacts.TryGetValue(id, out contact);
        }

        // Un balayage liste tout ce que le jeu envoie en ce moment : BeginScan, See pour
        // chacun d'eux, puis EndScan.
        public void BeginScan()
        {
            m_seen.Clear();
        }

        public void See(Sighting sighting, DateTime nowUtc)
        {
            m_seen.Add(sighting.Id);

            MemoryContact contact;
            if (!m_contacts.TryGetValue(sighting.Id, out contact))
            {
                contact = new MemoryContact { Id = sighting.Id };
                m_contacts.Add(sighting.Id, contact);
                Dirty = true;
            }
            else if (contact.Position.DistanceTo(sighting.Position) > 1.0
                || contact.Blocks != sighting.Blocks
                || contact.Relation != sighting.Relation
                || contact.Kind != sighting.Kind
                || !string.Equals(contact.Name, sighting.Name ?? "", StringComparison.Ordinal))
            {
                Dirty = true;
            }

            contact.Name = Clean(sighting.Name);
            contact.Kind = sighting.Kind;
            contact.Relation = sighting.Relation;
            contact.Blocks = sighting.Blocks;
            contact.Radius = sighting.Radius;
            contact.Position = sighting.Position;
            contact.Body = Clean(sighting.Body);
            contact.LastSeenUtc = nowUtc;
            contact.IsLive = true;
            contact.AbsentSinceUtc = null;
        }

        // Tout ce qui n'est pas vu dans ce balayage est hors de portée : cela garde sa dernière
        // position connue. Si sa dernière position connue est bien à l'intérieur de la
        // portée de l'observateur et qu'elle continue de manquer, elle n'y est plus,
        // et la carte l'oublie.
        public void EndScan(Vec3 observer, double syncRadius, DateTime nowUtc)
        {
            m_forget.Clear();
            var checkedRadius = syncRadius * ConfirmFraction;
            foreach (var contact in m_contacts.Values)
            {
                if (m_seen.Contains(contact.Id))
                    continue;

                contact.IsLive = false;
                if (contact.Position.DistanceTo(observer) <= checkedRadius)
                {
                    if (!contact.AbsentSinceUtc.HasValue)
                        contact.AbsentSinceUtc = nowUtc;
                    else if (nowUtc - contact.AbsentSinceUtc.Value >= ForgetAfter)
                        m_forget.Add(contact.Id);
                }
                else
                {
                    contact.AbsentSinceUtc = null;
                }
            }

            foreach (var id in m_forget)
                m_contacts.Remove(id);
            if (m_forget.Count > 0)
                Dirty = true;

            TrimOldest();
        }

        // En quittant un serveur, plus rien n'est vivant.
        public void MarkAllRemembered()
        {
            foreach (var contact in m_contacts.Values)
            {
                contact.IsLive = false;
                contact.AbsentSinceUtc = null;
            }
        }

        private void TrimOldest()
        {
            if (m_contacts.Count <= MaximumContacts)
                return;

            var all = new List<MemoryContact>(m_contacts.Values);
            all.Sort((a, b) => a.LastSeenUtc.CompareTo(b.LastSeenUtc));
            for (var i = 0; i < all.Count - MaximumContacts; i++)
            {
                if (!all[i].IsLive)
                    m_contacts.Remove(all[i].Id);
            }
            Dirty = true;
        }

        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        // Une ligne par contact, séparée par des tabulations, culture invariante.
        public string Save()
        {
            var text = new StringBuilder();
            text.Append(Header).Append('\n');
            foreach (var c in m_contacts.Values)
            {
                text.Append(c.Id.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(((int)c.Kind).ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(((int)c.Relation).ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(c.Blocks.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(c.Radius.ToString("R", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(c.Position.X.ToString("R", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(c.Position.Y.ToString("R", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(c.Position.Z.ToString("R", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(c.LastSeenUtc.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(Clean(c.Body)).Append('\t')
                    .Append(Clean(c.Name)).Append('\n');
            }
            return text.ToString();
        }

        // Les lignes illisibles sont sautées : un fichier abîmé ne bloque jamais le jeu.
        public static MapMemory Load(string serverKey, string text)
        {
            var memory = new MapMemory(serverKey);
            if (string.IsNullOrEmpty(text))
                return memory;

            var lines = text.Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != Header)
                return memory;

            for (var i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].TrimEnd('\r').Split('\t');
                if (parts.Length < 11)
                    continue;

                long id;
                int kind, relation, blocks;
                double radius, x, y, z;
                long ticks;
                var ok = long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
                    & int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out kind)
                    & int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out relation)
                    & int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out blocks)
                    & double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out radius)
                    & double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                    & double.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                    & double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out z)
                    & long.TryParse(parts[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks);
                if (!ok || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                    continue;
                if (!Enum.IsDefined(typeof(ContactKind), kind) || !Enum.IsDefined(typeof(ContactRelation), relation))
                    continue;

                memory.m_contacts[id] = new MemoryContact
                {
                    Id = id,
                    Kind = (ContactKind)kind,
                    Relation = (ContactRelation)relation,
                    Blocks = blocks,
                    Radius = radius,
                    Position = new Vec3(x, y, z),
                    LastSeenUtc = new DateTime(ticks, DateTimeKind.Utc),
                    Body = parts[9],
                    Name = parts[10],
                };
            }
            return memory;
        }
    }
}
