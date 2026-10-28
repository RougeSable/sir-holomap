using System;

namespace SirHolomap
{
    // Which servers listen to the reports: ours, PU and PTU. Nowhere else
    // does a report leave the machine.
    public static class OurServers
    {
        // A server of ours has this in the name it is listed under.
        public static readonly string[] NameMarkers = { "sirius immersion" };

        public static bool IsOurs(string serverName, bool isDedicated)
        {
            if (!isDedicated || string.IsNullOrEmpty(serverName))
                return false;
            foreach (var marker in NameMarkers)
            {
                if (serverName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
