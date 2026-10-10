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

    // What the player sees of a grid or a character at one moment.
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

        // Joined to another grid (wheel, rotor head, connector): never
        // treated as debris.
        public bool Linked;
    }

    // One remembered thing. Live while the game sends it to the player, a
    // memory otherwise: it then stays at its last known position, with the
    // time it was last seen.
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
        public bool Linked;

        // Empty when the contact is not near a planet.
        public string Body = "";

        // Runtime state, never saved: a memory loaded from disk is never live.
        public bool IsLive;
        public DateTime? AbsentSinceUtc;
    }

    // The personal memory of one server. The game sends nothing beyond the
    // synchronisation range of the server: whatever entered that range was
    // seen, its position and time are kept; whatever left it stays where it
    // was last seen (the player's own grids included, the game says nothing
    // more about them). Coming back near a place updates it: what is there is
    // seen again, what is no longer there is forgotten.
    public sealed class MapMemory
    {
        public const string Header = "sir-holomap memory 1";

        // A remembered place counts as checked only well inside the
        // synchronisation range: at its edge, the game is still streaming.
        public const double ConfirmFraction = 0.75;

        // How long a remembered contact must be missing from a checked place
        // before it is forgotten.
        public static readonly TimeSpan ForgetAfter = TimeSpan.FromSeconds(20);

        // Beyond that, the oldest memories go first.
        public const int MaximumContacts = 20000;

        private readonly Dictionary<long, MemoryContact> m_contacts = new Dictionary<long, MemoryContact>();
        private readonly HashSet<long> m_seen = new HashSet<long>();
        private readonly List<long> m_forget = new List<long>();

        public MapMemory(string serverKey)
        {
            ServerKey = serverKey ?? "";
        }

        public string ServerKey { get; private set; }

        // True when something worth saving changed since the last save.
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

        // A scan lists everything the game currently sends: BeginScan, See for
        // each of them, then EndScan.
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
                || contact.Linked != sighting.Linked
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
            contact.Linked = sighting.Linked;
            contact.Radius = sighting.Radius;
            contact.Position = sighting.Position;
            contact.Body = Clean(sighting.Body);
            contact.LastSeenUtc = nowUtc;
            contact.IsLive = true;
            contact.AbsentSinceUtc = null;
        }

        // Everything not seen in this scan is out of reach: it keeps its last
        // known position. If its last known position is well inside the
        // observer's range and it stays missing, it is no longer there, and
        // the map forgets it.
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

        // On leaving a server, nothing is live any more.
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

        // One line per contact, tab separated, invariant culture.
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
                    .Append(Clean(c.Name)).Append('\t')
                    .Append(c.Linked ? "1" : "0").Append('\n');
            }
            return text.ToString();
        }

        // Unreadable lines are skipped: a damaged file never blocks the game.
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
                    Linked = parts.Length > 11 && parts[11].Trim() == "1",
                };
            }
            return memory;
        }
    }
}
