namespace SirHolomap
{
    // Everything the player reads, in one place.
    public static class Texts
    {
        public const string Title = "HOLOMAP";

        // The three views, buttons at the top of the map.
        public const string ModePlanet = "A  PLANET";
        public const string ModeLocal = "B  LOCAL SPACE";
        public const string ModeSystem = "C  SYSTEM";
        public const string ModePlanetHelp = "The globe of the planet you are on, or the last one you looked at.";
        public const string ModeLocalHelp = "Your neighbourhood in space, seen from above.";
        public const string ModeSystemHelp = "The whole system: planets, the system in 3D and the galaxy.";
        public const string ModePlanetUnavailable = "No planet nearby: pick one in C.";

        // The tabs of C.
        public const string TabBodies = "Planets";
        public const string TabOrrery = "System 3D";
        public const string TabGalaxy = "Galaxy";
        public const string TabBodiesHelp = "Planets and their moons laid flat, by distance to the centre of the world.";
        public const string TabOrreryHelp = "The system in 3D: true distances, bodies enlarged so that they stay readable.";
        public const string TabGalaxyHelp = "Where this server lies in the galaxy, among the servers you know.";

        // Panel sections.
        public const string SectionInfo = "INFO";
        public const string SectionShow = "SHOW";
        public const string SectionGrids = "GRIDS IN VIEW";
        public const string SectionBodies = "BODIES";
        public const string SectionServers = "SERVERS";

        // Filters.
        public const string ShowStations = "Bases";
        public const string ShowShips = "Large ships";
        public const string ShowSmallGrids = "Small grids";
        public const string ShowPlayers = "Players";
        public const string ShowGps = "GPS";
        public const string ShowMemories = "Memories";
        public const string Daylight = "Daylight globe";
        public const string DaylightHelp = "Lights the globe from the camera while the map is open, so that the night side reads too.";
        public const string DaylightUnavailableHelp = "Not available: another plugin sets the game's light, so the globe keeps the game's light.";
        public const string ThresholdPlanet = "Min. blocks (A)";
        public const string ThresholdSpace = "Min. blocks (B, C)";
        public const string ThresholdHelp = "Grids with fewer blocks are hidden from this view: debris of broken grids stay out of the way. Kept for every server.";
        public const string Favorites = "Favorites";
        public const string Visited = "Visited";
        public const string FavoritesHelp = "Only the servers in your favorites. Kept between game launches.";
        public const string VisitedHelp = "Only the servers you have played on. Kept between game launches.";
        public const string Search = "Search";
        public const string SearchHelp = "Search a server by name or address.";
        public const string ServersHere = "{0} servers here";
        public const string ServersHereHelp = "Double click to zoom in on them, or pick one in the list.";
        public const string SyncRange = "Sync range";
        public const string FilterActive = "Filter active: {0} of {1} servers shown";
        public const string AllServers = "All known servers shown";
        public const string Refresh = "Refresh";
        public const string RefreshHelp = "Asks the game for the server lists again.";
        public const string Loading = "Asking the game for servers...";

        // Info lines.
        public const string Name = "Name";
        public const string Type = "Type";
        public const string Diameter = "Diameter";
        public const string Gravity = "Gravity";
        public const string Atmosphere = "Atmosphere";
        public const string Oxygen = "Oxygen";
        public const string Temperature = "Temperature";
        public const string Ores = "Ores";
        public const string Distance = "Distance";
        public const string Altitude = "Altitude";
        public const string Speed = "Speed";
        public const string Blocks = "Blocks";
        public const string Owner = "Owner";
        public const string Seen = "Seen";
        public const string Position = "Position";
        public const string Moons = "Moons";
        public const string Players = "Players";
        public const string Address = "Address";
        public const string Ping = "Ping";
        public const string LastVisit = "Last visit";
        public const string Status = "Status";
        public const string None = "none";
        public const string Yes = "yes";
        public const string No = "no";
        public const string Breathable = "breathable";
        public const string NotBreathable = "not breathable";
        public const string Live = "live";
        public const string SeenAgo = "seen {0} ago";
        public const string LastVisitAgo = "last visit {0} ago";
        public const string NeverVisited = "never visited";
        public const string YouAreHere = "You are here";
        public const string You = "You";
        public const string Sun = "Sun";
        public const string Unknown = "unknown";

        public const string KindStation = "Base";
        public const string KindLargeShip = "Large ship";
        public const string KindSmallShip = "Small grid";
        public const string KindCharacter = "Player";
        public const string KindGps = "GPS";
        public const string KindPlanet = "Planet";
        public const string KindMoon = "Moon";

        public const string RelationOwn = "you";
        public const string RelationFaction = "your faction";
        public const string RelationNeutral = "neutral";
        public const string RelationEnemy = "enemy";
        public const string RelationUnowned = "nobody";

        // Help lines at the bottom of the panel: the same gestures everywhere.
        public const string HelpPlanet = "Left drag: turn the globe   Wheel: zoom   Click: select   Double click: follow";
        public const string HelpLocal = "Left drag: turn   Middle drag or WASD: move   Wheel: zoom   Double click a grid: fly around it";
        public const string HelpLocked = "Left drag: turn around the grid   Wheel out: back to your neighbourhood";
        public const string HelpOrrery = "Left drag: turn   Middle drag or WASD: move   Wheel: zoom (Ctrl: fly)   Double click: dive";
        public const string HelpBodies = "Click: select   Double click: open the globe   Wheel out: system";
        public const string HelpGalaxy = "Wheel: zoom   Middle drag: move   Click: details   Double click: join the server";
        public const string CloseHint = "M or Esc: close";

        // Joining another server.
        public const string JoinCaption = "Join another server";
        public const string JoinQuestion = "Leave this server and join \"{0}\"?\n\n{1}";
        public const string JoinChecking = "Checking \"{0}\"...";
        public const string JoinOffline = "\"{0}\" does not answer. You stay on this server.";
        public const string JoinFull = "\"{0}\" is full ({1}/{2}). You stay on this server.";
        public const string JoinPassword = "\"{0}\" asks for a password: join it from the game's server list. You stay on this server.";
        public const string JoinVersion = "\"{0}\" runs another version of the game. You stay on this server.";
        public const string JoinNoAddress = "The address of \"{0}\" is unknown. You stay on this server.";
        public const string JoinAlreadyHere = "You are already on \"{0}\".";
        public const string JoinFailedCaption = "Could not join";
        public const string ReturnCaption = "Back to your server";
        public const string ReturnQuestion = "\"{0}\" could not be joined.\n\nGo back to \"{1}\", the server you just left?";
        public const string ReturnOffline = "\"{0}\" does not answer either. Join a server from the game's server list.";

        // Plugin state.
        public const string StopPrefix = "Sir Holomap: ";
        public const string CameraStepTaken = StopPrefix + "another plugin already moves the game camera ({0}). The map opens without the 3D view.";
        public const string LightStepTaken = StopPrefix + "another plugin already sets the game's light ({0}). " + Daylight + " stays off: the globe keeps the game's light.";
        public const string LightHookFailed = StopPrefix + "the game's light could not be reached. " + Daylight + " stays off: the globe keeps the game's light.";
        public const string CameraHookFailed = StopPrefix + "the game's camera could not be reached. The map opens without the 3D view.";
        public const string Fault = StopPrefix + "a fault occurred, the map is closed. See the game log.";
        public const string NoSession = StopPrefix + "the map opens in a world.";
    }
}
