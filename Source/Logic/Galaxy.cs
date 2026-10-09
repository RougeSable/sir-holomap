using System;
using System.Collections.Generic;
using System.Globalization;

namespace SirHolomap
{
    // Server addresses as the game writes them ("steam://1.2.3.4:27016",
    // "1.2.3.4:27016"): one normal form, so that the same server is always
    // recognised.
    public static class ServerAddress
    {
        public const int DefaultPort = 27016;

        public static string Normalize(string address)
        {
            if (string.IsNullOrEmpty(address))
                return "";
            var text = address.Trim().ToLowerInvariant();
            var scheme = text.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
                text = text.Substring(scheme + 3);
            text = text.TrimEnd('/');

            uint ip;
            int port;
            if (TryParseIPv4(text, out ip, out port))
                return FormatIPv4(ip) + ":" + port.ToString(CultureInfo.InvariantCulture);
            return text;
        }

        public static bool TryParseIPv4(string text, out uint ip, out int port)
        {
            ip = 0;
            port = DefaultPort;
            if (string.IsNullOrEmpty(text))
                return false;

            var host = text;
            var colon = text.LastIndexOf(':');
            if (colon >= 0)
            {
                host = text.Substring(0, colon);
                if (!int.TryParse(text.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out port)
                    || port < 0 || port > 65535)
                    return false;
            }

            var parts = host.Split('.');
            if (parts.Length != 4)
                return false;
            for (var i = 0; i < 4; i++)
            {
                int value;
                if (parts[i].Length == 0 || parts[i].Length > 3
                    || !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out value)
                    || value > 255)
                    return false;
                ip = (ip << 8) | (uint)value;
            }
            return true;
        }

        private static string FormatIPv4(uint ip)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.{3}",
                (ip >> 24) & 255, (ip >> 16) & 255, (ip >> 8) & 255, ip & 255);
        }
    }

    // The galaxy every server is placed in: a central bulge and spiral arms,
    // the same picture for every player. Coordinates are in [-1, 1].
    public static class GalaxyShape
    {
        public const int Arms = 4;
        public const double InnerRadius = 0.14;
        public const double OuterRadius = 0.92;

        // How much the arms wind: the angle grows with the log of the radius.
        public const double Winding = 2.6;

        // Angular width of an arm, as a share of the gap between two arms.
        public const double ArmWidthShare = 0.55;

        public static double ArmCentre(int arm, double radius)
        {
            return arm * (2 * Math.PI / Arms) + Winding * Math.Log(Math.Max(radius, 1e-6) / InnerRadius);
        }

        // 1 on the axis of an arm, 0 midway between two arms.
        public static double ArmStrength(double x, double y)
        {
            var radius = Math.Sqrt(x * x + y * y);
            if (radius < 1e-9)
                return 1;
            var angle = Math.Atan2(y, x) - Winding * Math.Log(radius / InnerRadius);
            var gap = 2 * Math.PI / Arms;
            var phase = angle / gap - Math.Floor(angle / gap);
            var distance = Math.Min(phase, 1 - phase) * 2;
            return Math.Max(0, 1 - distance);
        }
    }

    public struct GalaxyPoint
    {
        public readonly double X;
        public readonly double Y;

        public GalaxyPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    // Every server gets a fixed place in the galaxy, computed from its address
    // alone: every player sees it at the same spot ("it is near the left
    // arm"). The address is first turned into a 48-bit number (IPv4 and port
    // exactly, a hash otherwise), shuffled by a reversible mix, then cut into
    // an arm, a radius and an offset across the arm. Distinct numbers give
    // distinct radii, or distinct angles within non-overlapping bands: two
    // different addresses never get exactly the same place.
    public static class GalaxyPlacement
    {
        private const ulong Mask48 = (1UL << 48) - 1;
        private const int Steps = 1 << 23;

        public static GalaxyPoint PositionOf(string address)
        {
            var mixed = Mix48(KeyOf(address));
            var arm = (int)(mixed & 3);
            var along = (double)((mixed >> 2) & (Steps - 1));
            var across = (double)((mixed >> 25) & (Steps - 1));

            var radius = GalaxyShape.InnerRadius
                + (GalaxyShape.OuterRadius - GalaxyShape.InnerRadius) * (along + 0.5) / Steps;
            var band = 2 * Math.PI / GalaxyShape.Arms * GalaxyShape.ArmWidthShare;
            var angle = GalaxyShape.ArmCentre(arm, radius) + ((across + 0.5) / Steps - 0.5) * band;
            return new GalaxyPoint(radius * Math.Cos(angle), radius * Math.Sin(angle));
        }

        // IPv4 and port packed exactly when the address has them; a hash of the
        // normal form otherwise (lobbies, host names).
        public static ulong KeyOf(string address)
        {
            var normal = ServerAddress.Normalize(address);
            uint ip;
            int port;
            if (ServerAddress.TryParseIPv4(normal, out ip, out port))
                return ((ulong)ip << 16) | (uint)port;
            return Hashing.Fnv1a64(normal) & Mask48;
        }

        // A bijection of the 48-bit numbers: xor-shifts and odd multipliers
        // are all reversible modulo 2^48. Nearby addresses land far apart.
        public static ulong Mix48(ulong value)
        {
            var x = value & Mask48;
            x ^= x >> 17;
            x = (x * 0x9E3779B97F4BUL) & Mask48;
            x ^= x >> 21;
            x = (x * 0xD6E8FEB86659UL) & Mask48;
            x ^= x >> 19;
            return x;
        }
    }

    // An icon of the galaxy on screen: where its server truly lies, and, for
    // a server that carries its name, the room its row takes (from the left
    // edge of the icon to the end of the name, and the height of the row).
    public struct ScreenIcon
    {
        public int Index;
        public ulong Key;
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public ScreenIcon(int index, ulong key, double x, double y, double width, double height)
        {
            Index = index;
            Key = key;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public ScreenIcon(int index, ulong key, double x, double y)
            : this(index, key, x, y, 0, 0)
        {
        }
    }

    // Where an icon is drawn. An icon that stands alone is drawn at its true
    // place; in a group it is moved aside, and a thin line joins it to its
    // true place.
    public struct PlacedIcon
    {
        public int Index;
        public double X;
        public double Y;
        public double TrueX;
        public double TrueY;
        public int GroupSize;

        public bool Moved
        {
            get { return Math.Abs(X - TrueX) > 0.5 || Math.Abs(Y - TrueY) > 0.5; }
        }
    }

    // Icons too close to be told apart gather into a group, and the group
    // spreads its icons so that none covers another: every icon stays in
    // sight and can be clicked on its own. The layout depends only on the
    // true places, the sizes and the keys of the icons: never on their order
    // in the list, nor on which server is the current one, so a server is
    // always drawn at the same spot for the same view of the galaxy.
    public static class GalaxyLayout
    {
        // Servers shown with their name: a group stacks its rows in a column
        // centred on the middle of its members, from the highest to the
        // lowest, so that no name ever covers another. Groups whose columns
        // would touch become one group.
        public static PlacedIcon[] Rows(IList<ScreenIcon> icons, double iconHalf, double margin)
        {
            var columns = new List<Column>();
            foreach (var index in ByKey(icons))
                columns.Add(Column.Build(icons, new List<int> { index }, iconHalf));

            bool merged;
            do
            {
                merged = false;
                for (var i = 0; i < columns.Count; i++)
                {
                    var j = i + 1;
                    while (j < columns.Count)
                    {
                        if (!columns[i].Touches(columns[j], margin))
                        {
                            j++;
                            continue;
                        }
                        var members = new List<int>(columns[i].Members);
                        members.AddRange(columns[j].Members);
                        columns.RemoveAt(j);
                        columns[i] = Column.Build(icons, members, iconHalf);
                        merged = true;
                        j = i + 1;
                    }
                }
            }
            while (merged);

            var placed = new PlacedIcon[icons.Count];
            foreach (var column in columns)
            {
                var y = column.Top;
                foreach (var index in column.Members)
                {
                    var icon = icons[index];
                    var height = Math.Max(icon.Height, 0);
                    placed[index] = new PlacedIcon
                    {
                        Index = icon.Index,
                        X = column.Members.Count == 1 ? icon.X : column.CentreX,
                        Y = column.Members.Count == 1 ? icon.Y : y + height / 2,
                        TrueX = icon.X,
                        TrueY = icon.Y,
                        GroupSize = column.Members.Count,
                    };
                    y += height;
                }
            }
            return placed;
        }

        private sealed class Column
        {
            public List<int> Members;
            public double CentreX;
            public double Left;
            public double Right;
            public double Top;
            public double Bottom;

            // Members from top to bottom; the centre is the mean of the
            // members, summed in the order of their keys.
            public static Column Build(IList<ScreenIcon> icons, List<int> members, double iconHalf)
            {
                members.Sort((a, b) => CompareByKey(icons[a], icons[b], a, b));
                double sumX = 0, sumY = 0, height = 0, width = 2 * iconHalf;
                foreach (var index in members)
                {
                    sumX += icons[index].X;
                    sumY += icons[index].Y;
                    height += Math.Max(icons[index].Height, 0);
                    width = Math.Max(width, icons[index].Width);
                }
                members.Sort((a, b) =>
                {
                    var byY = icons[a].Y.CompareTo(icons[b].Y);
                    return byY != 0 ? byY : CompareByKey(icons[a], icons[b], a, b);
                });
                var column = new Column { Members = members };
                column.CentreX = sumX / members.Count;
                var centreY = sumY / members.Count;
                column.Left = column.CentreX - iconHalf;
                column.Right = column.Left + width;
                column.Top = centreY - height / 2;
                column.Bottom = column.Top + height;
                return column;
            }

            public bool Touches(Column other, double margin)
            {
                return Left < other.Right + margin && other.Left < Right + margin
                    && Top < other.Bottom + margin && other.Top < Bottom + margin;
            }
        }

        // Servers shown as a plain dot: a group sets its dots on rings around
        // the middle of its members, each dot at least "spacing" from every
        // other. Groups whose rings would come too close become one group.
        public static PlacedIcon[] Dots(IList<ScreenIcon> icons, double spacing)
        {
            var count = icons.Count;
            var placed = new PlacedIcon[count];
            if (count == 0)
                return placed;
            spacing = Math.Max(spacing, 1e-6);
            var sets = new Sets(count);

            // Neighbours closer than the spacing, through a grid of cells of
            // that size.
            var grid = new Dictionary<long, List<int>>();
            foreach (var index in ByKey(icons))
            {
                var cx = Cell(icons[index].X, spacing);
                var cy = Cell(icons[index].Y, spacing);
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        List<int> near;
                        if (!grid.TryGetValue(CellKey(cx + dx, cy + dy), out near))
                            continue;
                        foreach (var other in near)
                        {
                            var ex = icons[index].X - icons[other].X;
                            var ey = icons[index].Y - icons[other].Y;
                            if (ex * ex + ey * ey < spacing * spacing)
                                sets.Join(index, other);
                        }
                    }
                }
                List<int> cell;
                var key = CellKey(cx, cy);
                if (!grid.TryGetValue(key, out cell))
                {
                    cell = new List<int>();
                    grid[key] = cell;
                }
                cell.Add(index);
            }

            // Spread groups may reach their neighbours: those become one
            // group, until no group comes too close to another.
            var groups = Groups(icons, sets);
            for (var round = 0; round < 16; round++)
            {
                var discs = new List<Disc>();
                var largest = 0.0;
                foreach (var members in groups)
                {
                    var disc = Spread(icons, members, spacing, placed);
                    discs.Add(disc);
                    largest = Math.Max(largest, disc.Radius);
                }

                var reach = 2 * largest + spacing;
                var discGrid = new Dictionary<long, List<int>>();
                var joined = false;
                for (var i = 0; i < discs.Count; i++)
                {
                    var cx = Cell(discs[i].X, reach);
                    var cy = Cell(discs[i].Y, reach);
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        for (var dy = -1; dy <= 1; dy++)
                        {
                            List<int> near;
                            if (!discGrid.TryGetValue(CellKey(cx + dx, cy + dy), out near))
                                continue;
                            foreach (var j in near)
                            {
                                var ex = discs[i].X - discs[j].X;
                                var ey = discs[i].Y - discs[j].Y;
                                var limit = discs[i].Radius + discs[j].Radius + spacing;
                                if (ex * ex + ey * ey < limit * limit && sets.Join(groups[i][0], groups[j][0]))
                                    joined = true;
                            }
                        }
                    }
                    List<int> cell;
                    var key = CellKey(cx, cy);
                    if (!discGrid.TryGetValue(key, out cell))
                    {
                        cell = new List<int>();
                        discGrid[key] = cell;
                    }
                    cell.Add(i);
                }
                if (!joined)
                    break;
                groups = Groups(icons, sets);
                if (round == 15)
                {
                    foreach (var members in groups)
                        Spread(icons, members, spacing, placed);
                }
            }
            return placed;
        }

        // How many dots a ring of this rank holds, the ring's radius being
        // rank times the spacing: a little less than its perimeter allows, so
        // that two neighbours on the ring are never closer than the spacing.
        public static int RingCapacity(int rank)
        {
            return Math.Max(1, (int)Math.Floor(2 * Math.PI * rank * 0.95));
        }

        private struct Disc
        {
            public double X;
            public double Y;
            public double Radius;
        }

        private static Disc Spread(IList<ScreenIcon> icons, List<int> members, double spacing, PlacedIcon[] placed)
        {
            double sumX = 0, sumY = 0;
            foreach (var index in members)
            {
                sumX += icons[index].X;
                sumY += icons[index].Y;
            }
            var disc = new Disc { X = sumX / members.Count, Y = sumY / members.Count };
            if (members.Count == 1)
            {
                var icon = icons[members[0]];
                placed[members[0]] = new PlacedIcon { Index = icon.Index, X = icon.X, Y = icon.Y, TrueX = icon.X, TrueY = icon.Y, GroupSize = 1 };
                disc.X = icon.X;
                disc.Y = icon.Y;
                return disc;
            }

            // Closest to the middle first: they take the inner rings.
            var order = new List<int>(members);
            order.Sort((a, b) =>
            {
                var da = Square(icons[a].X - disc.X) + Square(icons[a].Y - disc.Y);
                var db = Square(icons[b].X - disc.X) + Square(icons[b].Y - disc.Y);
                var byDistance = da.CompareTo(db);
                return byDistance != 0 ? byDistance : CompareByKey(icons[a], icons[b], a, b);
            });

            var next = 0;
            if (order.Count <= 6)
            {
                var radius = spacing / (2 * Math.Sin(Math.PI / order.Count));
                Ring(icons, order, 0, order.Count, disc, radius, placed, members.Count);
                disc.Radius = radius;
                return disc;
            }

            var centre = icons[order[0]];
            placed[order[0]] = new PlacedIcon { Index = centre.Index, X = disc.X, Y = disc.Y, TrueX = centre.X, TrueY = centre.Y, GroupSize = members.Count };
            next = 1;
            var rank = 0;
            while (next < order.Count)
            {
                rank++;
                var take = Math.Min(RingCapacity(rank), order.Count - next);
                Ring(icons, order, next, take, disc, rank * spacing, placed, members.Count);
                next += take;
            }
            disc.Radius = rank * spacing;
            return disc;
        }

        // The dots of one ring, evenly spaced, in the order of the angle of
        // their true places around the middle of the group.
        private static void Ring(IList<ScreenIcon> icons, List<int> order, int start, int take, Disc disc, double radius,
            PlacedIcon[] placed, int groupSize)
        {
            var ring = order.GetRange(start, take);
            ring.Sort((a, b) =>
            {
                var byAngle = Angle(icons[a], disc).CompareTo(Angle(icons[b], disc));
                return byAngle != 0 ? byAngle : CompareByKey(icons[a], icons[b], a, b);
            });
            var first = Angle(icons[ring[0]], disc);
            for (var k = 0; k < ring.Count; k++)
            {
                var angle = first + 2 * Math.PI * k / ring.Count;
                var icon = icons[ring[k]];
                placed[ring[k]] = new PlacedIcon
                {
                    Index = icon.Index,
                    X = disc.X + radius * Math.Cos(angle),
                    Y = disc.Y + radius * Math.Sin(angle),
                    TrueX = icon.X,
                    TrueY = icon.Y,
                    GroupSize = groupSize,
                };
            }
        }

        private static double Angle(ScreenIcon icon, Disc disc)
        {
            var dx = icon.X - disc.X;
            var dy = icon.Y - disc.Y;
            return dx * dx + dy * dy < 1e-12 ? 0 : Math.Atan2(dy, dx);
        }

        private static double Square(double value)
        {
            return value * value;
        }

        // The groups of the sets, each listing its members in the order of
        // their keys, the groups in the order of their first key.
        private static List<List<int>> Groups(IList<ScreenIcon> icons, Sets sets)
        {
            var byRoot = new Dictionary<int, List<int>>();
            var groups = new List<List<int>>();
            foreach (var index in ByKey(icons))
            {
                var root = sets.Find(index);
                List<int> members;
                if (!byRoot.TryGetValue(root, out members))
                {
                    members = new List<int>();
                    byRoot[root] = members;
                    groups.Add(members);
                }
                members.Add(index);
            }
            return groups;
        }

        private static List<int> ByKey(IList<ScreenIcon> icons)
        {
            var order = new List<int>(icons.Count);
            for (var i = 0; i < icons.Count; i++)
                order.Add(i);
            order.Sort((a, b) => CompareByKey(icons[a], icons[b], a, b));
            return order;
        }

        private static int CompareByKey(ScreenIcon a, ScreenIcon b, int ia, int ib)
        {
            var byKey = a.Key.CompareTo(b.Key);
            if (byKey != 0)
                return byKey;
            var byX = a.X.CompareTo(b.X);
            if (byX != 0)
                return byX;
            var byY = a.Y.CompareTo(b.Y);
            return byY != 0 ? byY : ia.CompareTo(ib);
        }

        // Cells of a grid over the screen; far beyond any screen, cells merge.
        private const long CellLimit = 1000000;

        private static long Cell(double value, double size)
        {
            var cell = Math.Floor(value / size);
            if (double.IsNaN(cell))
                return 0;
            return (long)Math.Max(-CellLimit, Math.Min(CellLimit, cell));
        }

        private static long CellKey(long x, long y)
        {
            return ((x + (1L << 21)) << 22) | (y + (1L << 21));
        }

        // Disjoint sets of icons.
        private sealed class Sets
        {
            private readonly int[] m_parent;

            public Sets(int count)
            {
                m_parent = new int[count];
                for (var i = 0; i < count; i++)
                    m_parent[i] = i;
            }

            public int Find(int i)
            {
                while (m_parent[i] != i)
                {
                    m_parent[i] = m_parent[m_parent[i]];
                    i = m_parent[i];
                }
                return i;
            }

            // True when the two were apart.
            public bool Join(int a, int b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra == rb)
                    return false;
                if (ra < rb)
                    m_parent[rb] = ra;
                else
                    m_parent[ra] = rb;
                return true;
            }
        }
    }
}
