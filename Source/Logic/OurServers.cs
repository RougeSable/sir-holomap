using System;

namespace SirHolomap
{
    // Quels serveurs écoutent les rapports : les nôtres, PU et PTU. Nulle part ailleurs
    // un rapport ne quitte la machine.
    public static class OurServers
    {
        // Un serveur à nous porte ceci dans le nom sous lequel il est listé.
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
