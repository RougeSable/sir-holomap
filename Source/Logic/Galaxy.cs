using System;
using System.Collections.Generic;
using System.Globalization;

namespace SirHolomap
{
    // Les adresses de serveur telles que le jeu les écrit (« steam://1.2.3.4:27016 »,
    // « 1.2.3.4:27016 ») : une seule forme normale, pour que le même serveur soit toujours
    // reconnu.
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

    // La galaxie où chaque serveur est placé : un renflement central et des bras en spirale,
    // la même image pour tous les joueurs. Les coordonnées sont dans [-1, 1].
    public static class GalaxyShape
    {
        public const int Arms = 4;
        public const double InnerRadius = 0.14;
        public const double OuterRadius = 0.92;

        // De combien les bras s'enroulent : l'angle croît avec le logarithme du rayon.
        public const double Winding = 2.6;

        // Largeur angulaire d'un bras, en part de l'écart entre deux bras.
        public const double ArmWidthShare = 0.55;

        public static double ArmCentre(int arm, double radius)
        {
            return arm * (2 * Math.PI / Arms) + Winding * Math.Log(Math.Max(radius, 1e-6) / InnerRadius);
        }

        // 1 sur l'axe d'un bras, 0 à mi-chemin entre deux bras.
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

    // Chaque serveur a une place fixe dans la galaxie, calculée à partir de son adresse
    // seule : chaque joueur la voit au même endroit (« il est près du bras
    // gauche »). L'adresse devient d'abord un nombre de 48 bits (IPv4 et port
    // exactement, un hachage sinon), brassé par un mélange réversible, puis découpé en
    // un bras, un rayon et un décalage en travers du bras. Des nombres distincts donnent
    // des rayons distincts, ou des angles distincts dans des bandes qui ne se chevauchent pas : deux
    // adresses différentes n'obtiennent jamais exactement la même place.
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

        // IPv4 et port empaquetés exactement quand l'adresse les a ; un hachage de la
        // forme normale sinon (salons, noms d'hôte).
        public static ulong KeyOf(string address)
        {
            var normal = ServerAddress.Normalize(address);
            uint ip;
            int port;
            if (ServerAddress.TryParseIPv4(normal, out ip, out port))
                return ((ulong)ip << 16) | (uint)port;
            return Hashing.Fnv1a64(normal) & Mask48;
        }

        // Une bijection des nombres de 48 bits : les décalages avec ou exclusif et les multiplicateurs impairs
        // sont tous réversibles modulo 2^48. Des adresses voisines tombent loin l'une de l'autre.
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

    // Une icône de la galaxie à l'écran : où son serveur se trouve vraiment, et, pour
    // un serveur qui porte son nom, la place que prend sa ligne (du bord gauche
    // de l'icône à la fin du nom, et la hauteur de la ligne).
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

    // Où une icône est dessinée. Une icône seule est dessinée à sa vraie
    // place ; dans un groupe, elle est écartée, et un trait fin la relie à sa
    // vraie place.
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

    // Les icônes trop proches pour se distinguer se rassemblent en groupe, et le groupe
    // écarte ses icônes pour qu'aucune n'en couvre une autre : chaque icône reste en
    // vue et peut être cliquée seule. La disposition ne dépend que des
    // vraies places, des tailles et des clés des icônes : jamais de leur ordre
    // dans la liste, ni de quel serveur est le serveur actuel, si bien qu'un serveur est
    // toujours dessiné au même endroit pour la même vue de la galaxie.
    public static class GalaxyLayout
    {
        // Les serveurs montrés avec leur nom : un groupe empile ses lignes dans une colonne
        // centrée sur le milieu de ses membres, du plus haut au
        // plus bas, pour qu'aucun nom n'en couvre un autre. Les groupes dont les colonnes
        // se toucheraient n'en font plus qu'un.
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

            // Les membres de haut en bas ; le centre est la moyenne des
            // membres, sommée dans l'ordre de leurs clés.
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

        // Les serveurs montrés comme un simple point : un groupe pose ses points sur des anneaux autour
        // du milieu de ses membres, chaque point à au moins « spacing » de tous les
        // autres. Les groupes dont les anneaux se rapprocheraient trop n'en font plus qu'un.
        public static PlacedIcon[] Dots(IList<ScreenIcon> icons, double spacing)
        {
            var count = icons.Count;
            var placed = new PlacedIcon[count];
            if (count == 0)
                return placed;
            spacing = Math.Max(spacing, 1e-6);
            var sets = new Sets(count);

            // Les voisins plus proches que l'espacement, par une grille de cellules de
            // cette taille.
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

            // Les groupes étalés peuvent atteindre leurs voisins : ceux-ci n'en font plus qu'un
            // groupe, jusqu'à ce qu'aucun groupe ne soit trop près d'un autre.
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

        // Combien de points un anneau de ce rang porte, le rayon de l'anneau étant
        // le rang fois l'espacement : un peu moins que son périmètre ne le permet, pour
        // que deux voisins de l'anneau ne soient jamais plus proches que l'espacement.
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

            // Les plus proches du milieu d'abord : ils prennent les anneaux intérieurs.
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

        // Les points d'un anneau, régulièrement espacés, dans l'ordre de l'angle de
        // leurs vraies places autour du milieu du groupe.
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

        // Les groupes des ensembles, chacun listant ses membres dans l'ordre de
        // leurs clés, les groupes dans l'ordre de leur première clé.
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

        // Des cellules d'une grille sur l'écran ; bien au-delà de tout écran, les cellules fusionnent.
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

        // Des ensembles disjoints d'icônes.
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

            // Vrai quand les deux étaient séparés.
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
