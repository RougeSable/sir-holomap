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

    public struct ScreenIcon
    {
        public int Index;
        public double X;
        public double Y;

        public ScreenIcon(int index, double x, double y)
        {
            Index = index;
            X = x;
            Y = y;
        }
    }

    // A group of icons too close to be told apart on screen: drawn as one
    // badge with a count, it opens up as the player zooms in.
    public sealed class IconCluster
    {
        public double X;
        public double Y;
        public readonly List<int> Members = new List<int>();

        public int Count
        {
            get { return Members.Count; }
        }
    }

    public static class IconClustering
    {
        // Greedy, in a stable order: an icon joins the first group whose
        // centre is closer than the radius, the centre following its members.
        public static List<IconCluster> Cluster(IList<ScreenIcon> icons, double radius)
        {
            var clusters = new List<IconCluster>();
            var sorted = new List<ScreenIcon>(icons);
            sorted.Sort((a, b) => a.Index.CompareTo(b.Index));
            var radius2 = radius * radius;

            foreach (var icon in sorted)
            {
                IconCluster home = null;
                foreach (var cluster in clusters)
                {
                    var dx = cluster.X - icon.X;
                    var dy = cluster.Y - icon.Y;
                    if (dx * dx + dy * dy < radius2)
                    {
                        home = cluster;
                        break;
                    }
                }

                if (home == null)
                {
                    home = new IconCluster { X = icon.X, Y = icon.Y };
                    clusters.Add(home);
                    home.Members.Add(icon.Index);
                    continue;
                }

                var n = home.Members.Count;
                home.X = (home.X * n + icon.X) / (n + 1);
                home.Y = (home.Y * n + icon.Y) / (n + 1);
                home.Members.Add(icon.Index);
            }
            return clusters;
        }
    }
}
