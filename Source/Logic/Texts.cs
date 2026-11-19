using System.Collections.Generic;

namespace SirHolomap
{
    // Tout ce que le joueur lit, en un seul endroit, dans la langue réglée dans le
    // jeu (voir Localization). Le texte français ci-dessous est la référence ; les
    // autres langues sont dans les fichiers Translations, et un texte qui y manque
    // se montre en français.
    public static class Texts
    {

        // Nom de la carte, en haut à gauche.
        public static string Title { get { return Localization.Get("Title"); } }

        // Vue A.
        public static string ModePlanet { get { return Localization.Get("ModePlanet"); } }

        // Vue B.
        public static string ModeLocal { get { return Localization.Get("ModeLocal"); } }

        // Vue C.
        public static string ModeSystem { get { return Localization.Get("ModeSystem"); } }

        // Vue D.
        public static string ModeGalaxy { get { return Localization.Get("ModeGalaxy"); } }
        public static string ModePlanetHelp { get { return Localization.Get("ModePlanetHelp"); } }
        public static string ModeLocalHelp { get { return Localization.Get("ModeLocalHelp"); } }
        public static string ModeSystemHelp { get { return Localization.Get("ModeSystemHelp"); } }
        public static string ModePlanetUnavailable { get { return Localization.Get("ModePlanetUnavailable"); } }

        // Onglets de C, et nom et aide de D dans son menu.
        public static string TabBodies { get { return Localization.Get("TabBodies"); } }
        public static string TabOrrery { get { return Localization.Get("TabOrrery"); } }
        public static string TabGalaxy { get { return Localization.Get("TabGalaxy"); } }
        public static string TabBodiesHelp { get { return Localization.Get("TabBodiesHelp"); } }
        public static string TabOrreryHelp { get { return Localization.Get("TabOrreryHelp"); } }
        public static string TabGalaxyHelp { get { return Localization.Get("TabGalaxyHelp"); } }

        // Sections du menu de droite.
        public static string SectionInfo { get { return Localization.Get("SectionInfo"); } }
        public static string SectionShow { get { return Localization.Get("SectionShow"); } }
        public static string SectionGrids { get { return Localization.Get("SectionGrids"); } }
        public static string SectionBodies { get { return Localization.Get("SectionBodies"); } }
        public static string SectionServers { get { return Localization.Get("SectionServers"); } }

        // Filtres.
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

        // Filtres de la galaxie.
        public static string Favorites { get { return Localization.Get("Favorites"); } }
        public static string Visited { get { return Localization.Get("Visited"); } }
        public static string FavoritesHelp { get { return Localization.Get("FavoritesHelp"); } }
        public static string VisitedHelp { get { return Localization.Get("VisitedHelp"); } }
        public static string Search { get { return Localization.Get("Search"); } }
        public static string SearchHelp { get { return Localization.Get("SearchHelp"); } }
        public static string SyncRange { get { return Localization.Get("SyncRange"); } }
        public static string FilterActive { get { return Localization.Get("FilterActive"); } }
        public static string AllServers { get { return Localization.Get("AllServers"); } }
        public static string Refresh { get { return Localization.Get("Refresh"); } }
        public static string RefreshHelp { get { return Localization.Get("RefreshHelp"); } }
        public static string Loading { get { return Localization.Get("Loading"); } }

        // Lignes d'infos.
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

        // Lignes d'aide : groupes séparés par trois espaces.
        public static string HelpPlanet { get { return Localization.Get("HelpPlanet"); } }
        public static string HelpPlanetLocked { get { return Localization.Get("HelpPlanetLocked"); } }
        public static string HelpLocal { get { return Localization.Get("HelpLocal"); } }
        public static string HelpLocked { get { return Localization.Get("HelpLocked"); } }
        public static string HelpOrrery { get { return Localization.Get("HelpOrrery"); } }
        public static string HelpBodies { get { return Localization.Get("HelpBodies"); } }
        public static string HelpGalaxy { get { return Localization.Get("HelpGalaxy"); } }
        public static string CloseHint { get { return Localization.Get("CloseHint"); } }

        // Rejoindre un autre serveur.
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

        // Âges des souvenirs.
        public static string AgoFewSeconds { get { return Localization.Get("AgoFewSeconds"); } }
        public static string AgoMinutes { get { return Localization.Get("AgoMinutes"); } }
        public static string AgoHours { get { return Localization.Get("AgoHours"); } }
        public static string AgoDays { get { return Localization.Get("AgoDays"); } }
        public static string AgoMonths { get { return Localization.Get("AgoMonths"); } }
        public static string AgoYears { get { return Localization.Get("AgoYears"); } }

        // Les textes de référence, en français, par clé.
        public static Dictionary<string, string> EnglishTable()
        {
            return new Dictionary<string, string>
            {
                { "Title", "HOLOMAP" },
                { "ModePlanet", "PLANÈTE" },
                { "ModeLocal", "ESPACE PROCHE" },
                { "ModeSystem", "SYSTÈME" },
                { "ModeGalaxy", "GALAXIE" },
                { "ModePlanetHelp", "Le globe de la planète où vous êtes, ou de la dernière regardée." },
                { "ModeLocalHelp", "Votre voisinage dans l'espace, vu d'en haut." },
                { "ModeSystemHelp", "Tout le système : les planètes et leurs lunes, à plat ou en 3D." },
                { "ModePlanetUnavailable", "Aucune planète proche : choisissez-en une dans la vue Système." },
                { "TabBodies", "Système 2D" },
                { "TabOrrery", "Orbites" },
                { "TabGalaxy", "Galaxie" },
                { "TabBodiesHelp", "Les planètes et leurs lunes à plat, par distance au centre du monde." },
                { "TabOrreryHelp", "Le système en 3D : distances réelles, astres agrandis pour rester lisibles." },
                { "TabGalaxyHelp", "La place de ce serveur dans la galaxie, parmi les serveurs que vous connaissez." },
                { "SectionInfo", "INFOS" },
                { "SectionShow", "AFFICHER" },
                { "SectionGrids", "GRILLES EN VUE" },
                { "SectionBodies", "ASTRES" },
                { "SectionServers", "SERVEURS" },
                { "ShowStations", "Bases" },
                { "ShowShips", "Grands vaisseaux" },
                { "ShowSmallGrids", "Petites grilles" },
                { "ShowPlayers", "Joueurs" },
                { "ShowGps", "GPS" },
                { "ShowMemories", "Souvenirs" },
                { "Daylight", "Globe éclairé" },
                { "DaylightHelp", "Éclaire le globe depuis la caméra tant que la carte est ouverte, pour lire aussi la face de nuit." },
                { "DaylightUnavailableHelp", "Indisponible : un autre greffon règle la lumière du jeu, le globe garde la lumière du jeu." },
                { "ThresholdPlanet", "Blocs min. (planète)" },
                { "ThresholdSpace", "Blocs min. (espace)" },
                { "ThresholdHelp", "Les grilles de moins de blocs sont masquées dans cette vue : les débris de grilles brisées ne gênent plus. Valable pour tous les serveurs." },
                { "Favorites", "Favoris" },
                { "Visited", "Visités" },
                { "FavoritesHelp", "Seulement les serveurs de vos favoris. Conservé entre les lancements du jeu." },
                { "VisitedHelp", "Seulement les serveurs où vous avez joué. Conservé entre les lancements du jeu." },
                { "Search", "Rechercher" },
                { "SearchHelp", "Chercher un serveur par nom ou adresse." },
                { "SyncRange", "Portée de synchro" },
                { "FilterActive", "Filtre actif : {0} serveurs sur {1} affichés" },
                { "AllServers", "Tous les serveurs connus sont affichés" },
                { "Refresh", "Actualiser" },
                { "RefreshHelp", "Redemande les listes de serveurs au jeu." },
                { "Loading", "Demande des serveurs au jeu..." },
                { "Type", "Type" },
                { "Diameter", "Diamètre" },
                { "Gravity", "Gravité" },
                { "Atmosphere", "Atmosphère" },
                { "Oxygen", "Oxygène" },
                { "Temperature", "Température" },
                { "Ores", "Minerais" },
                { "Distance", "Distance" },
                { "Altitude", "Altitude" },
                { "Speed", "Vitesse" },
                { "Blocks", "Blocs" },
                { "Owner", "Propriétaire" },
                { "Seen", "Vu" },
                { "Range", "Portée" },
                { "Position", "Position" },
                { "Moons", "Lunes" },
                { "Planets", "Planètes" },
                { "Players", "Joueurs" },
                { "Address", "Adresse" },
                { "Ping", "Ping" },
                { "LastVisit", "Dernière visite" },
                { "Status", "Statut" },
                { "SystemSize", "Taille du système" },
                { "None", "aucun" },
                { "Breathable", "respirable" },
                { "NotBreathable", "non respirable" },
                { "Live", "en direct" },
                { "InRange", "à portée, en direct" },
                { "OutOfRange", "hors de portée : dernière position connue" },
                { "SeenAgo", "vu il y a {0}" },
                { "LastVisitAgo", "dernière visite il y a {0}" },
                { "NeverVisited", "jamais visité" },
                { "YouAreHere", "Vous êtes ici" },
                { nameof(You), "Vous" },
                { "Sun", "Soleil" },
                { "MoonOf", "Lune de {0}" },
                { "LockedSubtitle", "Clic gauche glissé : tourner autour de la grille" },
                { "TempExtremeCold", "froid extrême" },
                { "TempCold", "froid" },
                { "TempTemperate", "tempéré" },
                { "TempHot", "chaud" },
                { "TempExtremeHeat", "chaleur extrême" },
                { "KindStation", "Base" },
                { "KindLargeShip", "Grand vaisseau" },
                { "KindSmallShip", "Petite grille" },
                { "KindCharacter", "Joueur" },
                { "KindGps", "GPS" },
                { "KindPlanet", "Planète" },
                { "KindMoon", "Lune" },
                { "RelationOwn", "vous" },
                { "RelationFaction", "votre faction" },
                { "RelationNeutral", "neutre" },
                { "RelationEnemy", "ennemi" },
                { "RelationUnowned", "personne" },
                { "HelpPlanet", "Clic gauche glissé : tourner le globe   Molette : zoom   Clic : sélectionner   Double-clic sur une grille : tourner autour   Double-clic sur un astre : y aller" },
                { "HelpPlanetLocked", "Clic gauche glissé : tourner autour de la grille   Molette : zoom   Clic molette glissé ou ZQSD : lâcher" },
                { "HelpLocal", "Clic gauche glissé : tourner   Clic molette glissé ou ZQSD : déplacer   Molette : zoom   Double-clic sur une grille : voler autour   Double-clic sur un astre : y aller" },
                { "HelpLocked", "Clic gauche glissé : tourner autour de la grille   Clic dans le vide, clic droit ou molette : retour à votre voisinage" },
                { "HelpOrrery", "Clic gauche glissé : tourner   Clic molette glissé ou ZQSD : déplacer   Molette : zoom (Ctrl : avancer)   Double-clic : plonger" },
                { "HelpBodies", "Clic : sélectionner   Double-clic ou molette : ouvrir le globe" },
                { "HelpGalaxy", "Molette : zoom   Clic molette glissé : déplacer   Clic : détails   Double-clic : rejoindre le serveur" },
                { "CloseHint", "M ou Échap : fermer" },
                { "JoinCaption", "Rejoindre un autre serveur" },
                { "JoinQuestion", "Quitter ce serveur et rejoindre « {0} » ?" },
                { "JoinChecking", "Vérification de « {0} »..." },
                { "JoinOffline", "« {0} » ne répond pas. Vous restez sur ce serveur." },
                { "JoinFull", "« {0} » est plein ({1}/{2}). Vous restez sur ce serveur." },
                { "JoinVersion", "« {0} » utilise une autre version du jeu. Vous restez sur ce serveur." },
                { "JoinNoAddress", "L'adresse de « {0} » est inconnue. Vous restez sur ce serveur." },
                { "JoinAlreadyHere", "Vous êtes déjà sur « {0} »." },
                { "JoinFailedCaption", "Connexion impossible" },
                { "JoinPasswordNeeded", "Ce serveur demande un mot de passe." },
                { "JoinPasswordLabel", "Mot de passe" },
                { "JoinPasswordHelp", "Le mot de passe du serveur, transmis au jeu quand il le demande. Il n'est pas conservé." },
                { "JoinButton", "Rejoindre" },
                { "StayButton", "Rester ici" },
                { "ReturnCaption", "Retour à votre serveur" },
                { "ReturnQuestion", "« {0} » n'a pas pu être rejoint.\n\nRevenir sur « {1} », le serveur que vous venez de quitter ?" },
                { "ReturnOffline", "« {0} » ne répond pas non plus. Rejoignez un serveur depuis la liste des serveurs du jeu." },
                { "CameraStepTaken", "Sir Holomap : un autre greffon déplace déjà la caméra du jeu ({0}). La carte peint son propre globe au lieu de la vue 3D du jeu." },
                { "CameraHookFailed", "Sir Holomap : la caméra du jeu est inaccessible. La carte peint son propre globe au lieu de la vue 3D du jeu." },
                { "LightStepTaken", "Sir Holomap : un autre greffon règle déjà la lumière du jeu ({0}). « Globe éclairé » reste éteint : le globe garde la lumière du jeu." },
                { "LightHookFailed", "Sir Holomap : la lumière du jeu est inaccessible. « Globe éclairé » reste éteint : le globe garde la lumière du jeu." },
                { "Fault", "Sir Holomap : une erreur est survenue, la carte est fermée. Voir le journal du jeu." },
                { "AgoFewSeconds", "quelques secondes" },
                { "AgoMinutes", "{0} min" },
                { "AgoHours", "{0} h" },
                { "AgoDays", "{0} jours" },
                { "AgoMonths", "{0} mois" },
                { "AgoYears", "{0} ans" },
            };
        }
    }
}
