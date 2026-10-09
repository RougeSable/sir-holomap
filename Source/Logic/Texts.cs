using System.Collections.Generic;

namespace SirHolomap
{
    // Everything the player reads, in one place, in the language set in the
    // game (see Localization). The English text below is the reference; the
    // other languages are in the Translations files, and a text missing there
    // shows in English.
    public static class Texts
    {

        // Name of the map, top left.
        public static string Title { get { return Localization.Get("Title"); } }

        // View A.
        public static string ModePlanet { get { return Localization.Get("ModePlanet"); } }

        // View B.
        public static string ModeLocal { get { return Localization.Get("ModeLocal"); } }

        // View C.
        public static string ModeSystem { get { return Localization.Get("ModeSystem"); } }
        public static string ModePlanetHelp { get { return Localization.Get("ModePlanetHelp"); } }
        public static string ModeLocalHelp { get { return Localization.Get("ModeLocalHelp"); } }
        public static string ModeSystemHelp { get { return Localization.Get("ModeSystemHelp"); } }
        public static string ModePlanetUnavailable { get { return Localization.Get("ModePlanetUnavailable"); } }

        // Tabs of C.
        public static string TabBodies { get { return Localization.Get("TabBodies"); } }
        public static string TabOrrery { get { return Localization.Get("TabOrrery"); } }
        public static string TabGalaxy { get { return Localization.Get("TabGalaxy"); } }
        public static string TabBodiesHelp { get { return Localization.Get("TabBodiesHelp"); } }
        public static string TabOrreryHelp { get { return Localization.Get("TabOrreryHelp"); } }
        public static string TabGalaxyHelp { get { return Localization.Get("TabGalaxyHelp"); } }

        // Sections of the menu on the right.
        public static string SectionInfo { get { return Localization.Get("SectionInfo"); } }
        public static string SectionShow { get { return Localization.Get("SectionShow"); } }
        public static string SectionGrids { get { return Localization.Get("SectionGrids"); } }
        public static string SectionBodies { get { return Localization.Get("SectionBodies"); } }
        public static string SectionServers { get { return Localization.Get("SectionServers"); } }

        // Filters.
        public static string ShowStations { get { return Localization.Get("ShowStations"); } }
        public static string ShowShips { get { return Localization.Get("ShowShips"); } }
        public static string ShowSmallGrids { get { return Localization.Get("ShowSmallGrids"); } }
        public static string ShowPlayers { get { return Localization.Get("ShowPlayers"); } }
        public static string ShowGps { get { return Localization.Get("ShowGps"); } }
        public static string ShowMemories { get { return Localization.Get("ShowMemories"); } }
        public static string Daylight { get { return Localization.Get("Daylight"); } }
        public static string DaylightHelp { get { return Localization.Get("DaylightHelp"); } }
        public static string DaylightUnavailableHelp { get { return Localization.Get("DaylightUnavailableHelp"); } }
        public static string ThresholdPlanet { get { return Localization.Get("ThresholdPlanet"); } }
        public static string ThresholdSpace { get { return Localization.Get("ThresholdSpace"); } }
        public static string ThresholdHelp { get { return Localization.Get("ThresholdHelp"); } }

        // Galaxy filters.
        public static string Favorites { get { return Localization.Get("Favorites"); } }
        public static string Visited { get { return Localization.Get("Visited"); } }
        public static string FavoritesHelp { get { return Localization.Get("FavoritesHelp"); } }
        public static string VisitedHelp { get { return Localization.Get("VisitedHelp"); } }
        public static string Search { get { return Localization.Get("Search"); } }
        public static string SearchHelp { get { return Localization.Get("SearchHelp"); } }
        public static string ServersHere { get { return Localization.Get("ServersHere"); } }
        public static string ServersHereHelp { get { return Localization.Get("ServersHereHelp"); } }
        public static string SyncRange { get { return Localization.Get("SyncRange"); } }
        public static string FilterActive { get { return Localization.Get("FilterActive"); } }
        public static string AllServers { get { return Localization.Get("AllServers"); } }
        public static string Refresh { get { return Localization.Get("Refresh"); } }
        public static string RefreshHelp { get { return Localization.Get("RefreshHelp"); } }
        public static string Loading { get { return Localization.Get("Loading"); } }

        // Info lines.
        public static string Type { get { return Localization.Get("Type"); } }
        public static string Diameter { get { return Localization.Get("Diameter"); } }
        public static string Gravity { get { return Localization.Get("Gravity"); } }
        public static string Atmosphere { get { return Localization.Get("Atmosphere"); } }
        public static string Oxygen { get { return Localization.Get("Oxygen"); } }
        public static string Temperature { get { return Localization.Get("Temperature"); } }
        public static string Ores { get { return Localization.Get("Ores"); } }
        public static string Distance { get { return Localization.Get("Distance"); } }
        public static string Altitude { get { return Localization.Get("Altitude"); } }
        public static string Speed { get { return Localization.Get("Speed"); } }
        public static string Blocks { get { return Localization.Get("Blocks"); } }
        public static string Owner { get { return Localization.Get("Owner"); } }
        public static string Seen { get { return Localization.Get("Seen"); } }
        public static string Range { get { return Localization.Get("Range"); } }
        public static string Position { get { return Localization.Get("Position"); } }
        public static string Moons { get { return Localization.Get("Moons"); } }
        public static string Planets { get { return Localization.Get("Planets"); } }
        public static string Players { get { return Localization.Get("Players"); } }
        public static string Address { get { return Localization.Get("Address"); } }
        public static string Ping { get { return Localization.Get("Ping"); } }
        public static string LastVisit { get { return Localization.Get("LastVisit"); } }
        public static string Status { get { return Localization.Get("Status"); } }
        public static string SystemSize { get { return Localization.Get("SystemSize"); } }
        public static string None { get { return Localization.Get("None"); } }
        public static string Breathable { get { return Localization.Get("Breathable"); } }
        public static string NotBreathable { get { return Localization.Get("NotBreathable"); } }
        public static string Live { get { return Localization.Get("Live"); } }
        public static string InRange { get { return Localization.Get("InRange"); } }
        public static string OutOfRange { get { return Localization.Get("OutOfRange"); } }
        public static string SeenAgo { get { return Localization.Get("SeenAgo"); } }
        public static string LastVisitAgo { get { return Localization.Get("LastVisitAgo"); } }
        public static string NeverVisited { get { return Localization.Get("NeverVisited"); } }
        public static string YouAreHere { get { return Localization.Get("YouAreHere"); } }
        public static string You { get { return Localization.Get("You"); } }
        public static string Sun { get { return Localization.Get("Sun"); } }
        public static string MoonOf { get { return Localization.Get("MoonOf"); } }
        public static string LockedSubtitle { get { return Localization.Get("LockedSubtitle"); } }
        public static string TempExtremeCold { get { return Localization.Get("TempExtremeCold"); } }
        public static string TempCold { get { return Localization.Get("TempCold"); } }
        public static string TempTemperate { get { return Localization.Get("TempTemperate"); } }
        public static string TempHot { get { return Localization.Get("TempHot"); } }
        public static string TempExtremeHeat { get { return Localization.Get("TempExtremeHeat"); } }
        public static string KindStation { get { return Localization.Get("KindStation"); } }
        public static string KindLargeShip { get { return Localization.Get("KindLargeShip"); } }
        public static string KindSmallShip { get { return Localization.Get("KindSmallShip"); } }
        public static string KindCharacter { get { return Localization.Get("KindCharacter"); } }
        public static string KindGps { get { return Localization.Get("KindGps"); } }
        public static string KindPlanet { get { return Localization.Get("KindPlanet"); } }
        public static string KindMoon { get { return Localization.Get("KindMoon"); } }
        public static string RelationOwn { get { return Localization.Get("RelationOwn"); } }
        public static string RelationFaction { get { return Localization.Get("RelationFaction"); } }
        public static string RelationNeutral { get { return Localization.Get("RelationNeutral"); } }
        public static string RelationEnemy { get { return Localization.Get("RelationEnemy"); } }
        public static string RelationUnowned { get { return Localization.Get("RelationUnowned"); } }

        // Help lines: groups separated by three spaces.
        public static string HelpPlanet { get { return Localization.Get("HelpPlanet"); } }
        public static string HelpPlanetLocked { get { return Localization.Get("HelpPlanetLocked"); } }
        public static string HelpLocal { get { return Localization.Get("HelpLocal"); } }
        public static string HelpLocked { get { return Localization.Get("HelpLocked"); } }
        public static string HelpOrrery { get { return Localization.Get("HelpOrrery"); } }
        public static string HelpBodies { get { return Localization.Get("HelpBodies"); } }
        public static string HelpGalaxy { get { return Localization.Get("HelpGalaxy"); } }
        public static string CloseHint { get { return Localization.Get("CloseHint"); } }

        // Joining another server.
        public static string JoinCaption { get { return Localization.Get("JoinCaption"); } }
        public static string JoinQuestion { get { return Localization.Get("JoinQuestion"); } }
        public static string JoinChecking { get { return Localization.Get("JoinChecking"); } }
        public static string JoinOffline { get { return Localization.Get("JoinOffline"); } }
        public static string JoinFull { get { return Localization.Get("JoinFull"); } }
        public static string JoinVersion { get { return Localization.Get("JoinVersion"); } }
        public static string JoinNoAddress { get { return Localization.Get("JoinNoAddress"); } }
        public static string JoinAlreadyHere { get { return Localization.Get("JoinAlreadyHere"); } }
        public static string JoinFailedCaption { get { return Localization.Get("JoinFailedCaption"); } }
        public static string JoinPasswordNeeded { get { return Localization.Get("JoinPasswordNeeded"); } }
        public static string JoinPasswordLabel { get { return Localization.Get("JoinPasswordLabel"); } }
        public static string JoinPasswordHelp { get { return Localization.Get("JoinPasswordHelp"); } }
        public static string JoinButton { get { return Localization.Get("JoinButton"); } }
        public static string StayButton { get { return Localization.Get("StayButton"); } }
        public static string ReturnCaption { get { return Localization.Get("ReturnCaption"); } }
        public static string ReturnQuestion { get { return Localization.Get("ReturnQuestion"); } }
        public static string ReturnOffline { get { return Localization.Get("ReturnOffline"); } }

        // Notifications.
        public static string CameraStepTaken { get { return Localization.Get("CameraStepTaken"); } }
        public static string CameraHookFailed { get { return Localization.Get("CameraHookFailed"); } }
        public static string LightStepTaken { get { return Localization.Get("LightStepTaken"); } }
        public static string LightHookFailed { get { return Localization.Get("LightHookFailed"); } }
        public static string Fault { get { return Localization.Get("Fault"); } }

        // Ages of memories.
        public static string AgoFewSeconds { get { return Localization.Get("AgoFewSeconds"); } }
        public static string AgoMinutes { get { return Localization.Get("AgoMinutes"); } }
        public static string AgoHours { get { return Localization.Get("AgoHours"); } }
        public static string AgoDays { get { return Localization.Get("AgoDays"); } }
        public static string AgoMonths { get { return Localization.Get("AgoMonths"); } }
        public static string AgoYears { get { return Localization.Get("AgoYears"); } }

        // The English texts, by key.
        public static Dictionary<string, string> EnglishTable()
        {
            return new Dictionary<string, string>
            {
                { "Title", "HOLOMAP" },
                { "ModePlanet", "PLANET" },
                { "ModeLocal", "LOCAL SPACE" },
                { "ModeSystem", "SYSTEM" },
                { "ModePlanetHelp", "The globe of the planet you are on, or the last one you looked at." },
                { "ModeLocalHelp", "Your neighbourhood in space, seen from above." },
                { "ModeSystemHelp", "The whole system: planets, the system in 3D and the galaxy." },
                { "ModePlanetUnavailable", "No planet nearby: pick one in the System view." },
                { "TabBodies", "System 2D" },
                { "TabOrrery", "System 3D" },
                { "TabGalaxy", "Galaxy" },
                { "TabBodiesHelp", "Planets and their moons laid flat, by distance to the centre of the world." },
                { "TabOrreryHelp", "The system in 3D: true distances, bodies enlarged so that they stay readable." },
                { "TabGalaxyHelp", "Where this server lies in the galaxy, among the servers you know." },
                { "SectionInfo", "INFO" },
                { "SectionShow", "SHOW" },
                { "SectionGrids", "GRIDS IN VIEW" },
                { "SectionBodies", "BODIES" },
                { "SectionServers", "SERVERS" },
                { "ShowStations", "Bases" },
                { "ShowShips", "Large ships" },
                { "ShowSmallGrids", "Small grids" },
                { "ShowPlayers", "Players" },
                { "ShowGps", "GPS" },
                { "ShowMemories", "Memories" },
                { "Daylight", "Daylight globe" },
                { "DaylightHelp", "Lights the globe from the camera while the map is open, so that the night side reads too." },
                { "DaylightUnavailableHelp", "Not available: another plugin sets the game's light, so the globe keeps the game's light." },
                { "ThresholdPlanet", "Min. blocks (planet)" },
                { "ThresholdSpace", "Min. blocks (space)" },
                { "ThresholdHelp", "Grids with fewer blocks are hidden from this view: debris of broken grids stay out of the way. Kept for every server." },
                { "Favorites", "Favorites" },
                { "Visited", "Visited" },
                { "FavoritesHelp", "Only the servers in your favorites. Kept between game launches." },
                { "VisitedHelp", "Only the servers you have played on. Kept between game launches." },
                { "Search", "Search" },
                { "SearchHelp", "Search a server by name or address." },
                { "ServersHere", "{0} servers here" },
                { "ServersHereHelp", "Double click to zoom in on them, or pick one in the list." },
                { "SyncRange", "Sync range" },
                { "FilterActive", "Filter active: {0} of {1} servers shown" },
                { "AllServers", "All known servers shown" },
                { "Refresh", "Refresh" },
                { "RefreshHelp", "Asks the game for the server lists again." },
                { "Loading", "Asking the game for servers..." },
                { "Type", "Type" },
                { "Diameter", "Diameter" },
                { "Gravity", "Gravity" },
                { "Atmosphere", "Atmosphere" },
                { "Oxygen", "Oxygen" },
                { "Temperature", "Temperature" },
                { "Ores", "Ores" },
                { "Distance", "Distance" },
                { "Altitude", "Altitude" },
                { "Speed", "Speed" },
                { "Blocks", "Blocks" },
                { "Owner", "Owner" },
                { "Seen", "Seen" },
                { "Range", "Range" },
                { "Position", "Position" },
                { "Moons", "Moons" },
                { "Planets", "Planets" },
                { "Players", "Players" },
                { "Address", "Address" },
                { "Ping", "Ping" },
                { "LastVisit", "Last visit" },
                { "Status", "Status" },
                { "SystemSize", "Size of the system" },
                { "None", "none" },
                { "Breathable", "breathable" },
                { "NotBreathable", "not breathable" },
                { "Live", "live" },
                { "InRange", "in sync range, live" },
                { "OutOfRange", "out of range: last known position" },
                { "SeenAgo", "seen {0} ago" },
                { "LastVisitAgo", "last visit {0} ago" },
                { "NeverVisited", "never visited" },
                { "YouAreHere", "You are here" },
                { "You", "You" },
                { "Sun", "Sun" },
                { "MoonOf", "Moon of {0}" },
                { "LockedSubtitle", "Left drag: turn around the grid" },
                { "TempExtremeCold", "extreme cold" },
                { "TempCold", "cold" },
                { "TempTemperate", "temperate" },
                { "TempHot", "hot" },
                { "TempExtremeHeat", "extreme heat" },
                { "KindStation", "Base" },
                { "KindLargeShip", "Large ship" },
                { "KindSmallShip", "Small grid" },
                { "KindCharacter", "Player" },
                { "KindGps", "GPS" },
                { "KindPlanet", "Planet" },
                { "KindMoon", "Moon" },
                { "RelationOwn", "you" },
                { "RelationFaction", "your faction" },
                { "RelationNeutral", "neutral" },
                { "RelationEnemy", "enemy" },
                { "RelationUnowned", "nobody" },
                { "HelpPlanet", "Left drag: turn the globe   Wheel: zoom   Click: select   Double click a grid: turn around it   Double click a body: go to it" },
                { "HelpPlanetLocked", "Left drag: turn around the grid   Wheel: zoom   Middle drag or WASD: let go" },
                { "HelpLocal", "Left drag: turn   Middle drag or WASD: move   Wheel: zoom   Double click a grid: fly around it" },
                { "HelpLocked", "Left drag: turn around the grid   Click on empty space, right click or wheel: back to your neighbourhood" },
                { "HelpOrrery", "Left drag: turn   Middle drag or WASD: move   Wheel: zoom (Ctrl: fly)   Double click: dive" },
                { "HelpBodies", "Click: select   Double click or wheel: open the globe" },
                { "HelpGalaxy", "Wheel: zoom   Middle drag: move   Click: details   Double click: join the server" },
                { "CloseHint", "M or Esc: close" },
                { "JoinCaption", "Join another server" },
                { "JoinQuestion", "Leave this server and join \"{0}\"?" },
                { "JoinChecking", "Checking \"{0}\"..." },
                { "JoinOffline", "\"{0}\" does not answer. You stay on this server." },
                { "JoinFull", "\"{0}\" is full ({1}/{2}). You stay on this server." },
                { "JoinVersion", "\"{0}\" runs another version of the game. You stay on this server." },
                { "JoinNoAddress", "The address of \"{0}\" is unknown. You stay on this server." },
                { "JoinAlreadyHere", "You are already on \"{0}\"." },
                { "JoinFailedCaption", "Could not join" },
                { "JoinPasswordNeeded", "This server asks for a password." },
                { "JoinPasswordLabel", "Password" },
                { "JoinPasswordHelp", "The password of the server, handed to the game when it asks for it. It is not kept." },
                { "JoinButton", "Join" },
                { "StayButton", "Stay here" },
                { "ReturnCaption", "Back to your server" },
                { "ReturnQuestion", "\"{0}\" could not be joined.\n\nGo back to \"{1}\", the server you just left?" },
                { "ReturnOffline", "\"{0}\" does not answer either. Join a server from the game's server list." },
                { "CameraStepTaken", "Sir Holomap: another plugin already moves the game camera ({0}). The map paints its own globe instead of the game's 3D view." },
                { "CameraHookFailed", "Sir Holomap: the game's camera could not be reached. The map paints its own globe instead of the game's 3D view." },
                { "LightStepTaken", "Sir Holomap: another plugin already sets the game's light ({0}). \"Daylight globe\" stays off: the globe keeps the game's light." },
                { "LightHookFailed", "Sir Holomap: the game's light could not be reached. \"Daylight globe\" stays off: the globe keeps the game's light." },
                { "Fault", "Sir Holomap: a fault occurred, the map is closed. See the game log." },
                { "AgoFewSeconds", "a few seconds" },
                { "AgoMinutes", "{0} min" },
                { "AgoHours", "{0} h" },
                { "AgoDays", "{0} days" },
                { "AgoMonths", "{0} months" },
                { "AgoYears", "{0} years" },
            };
        }
    }
}
