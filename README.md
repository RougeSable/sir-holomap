# Sir Holomap

Une carte en jeu pour Space Engineers, sous la forme d'un greffon côté joueur chargé par Pulsar.
Appuyez sur **M** pour l'ouvrir et la fermer. Avec un bloc en main, M garde la
touche de symétrie du jeu. Si le personnage meurt pendant que la carte est ouverte, la carte se ferme
toute seule et le jeu montre son propre écran de réapparition ; M ouvre de nouveau la carte
une fois le joueur revenu.

Un joueur sans le greffon joue normalement, et tout ce que la carte retient,
le joueur l'a vu de ses propres yeux. La carte ne demande rien au serveur et ne lui envoie rien sur le joueur ;
ce qu'elle envoie, à nos serveurs seulement, ce sont ses rapports d'erreurs (voir « Rapports d'erreurs »)
et un point par fonctionnalité utilisée (voir « Suivi des fonctionnalités »).

## Les quatre vues

Les boutons **Planète**, **Espace proche**, **Système** et **Galaxie** en haut
passent de l'une à l'autre ; la molette de la souris glisse aussi de l'une à la suivante. Chaque vue a le même menu à
droite (infos, ce qu'il faut montrer, liste de ce qui est en vue) et répond aux mêmes
gestes :

| Geste | Partout |
| --- | --- |
| Glissement gauche | tourner |
| Glissement milieu, ou WASD | déplacer |
| Molette | zoomer ; chaque cran multiplie la distance (Ctrl + molette avance dans le système 3D) |
| Clic | sélectionner, les infos se montrent à droite |
| Double clic | plonger dans ce qui est sous le curseur |
| Espace | retour vers vous |
| M ou Échap | fermer |

- **Planète.** Le globe de la planète où vous êtes, dessiné par le moteur de rendu
  du jeu depuis l'orbite : son vrai relief et ses vraies couleurs, pour n'importe quelle planète, celles
  des mods comprises. Vous, vos grilles et les bases sont à leur place, les vaisseaux bougent
  comme ils bougent. Molette vers l'avant : le sol se rapproche et le jeu affine le
  relief jusqu'à quelques dizaines de mètres. Molette vers l'arrière : le voisinage. Un double
  clic sur une grille à portée attache la caméra à elle : le glissement gauche
  tourne alors autour de la grille, la molette zoome toujours sur le globe. Un double clic
  sur un autre astre en vue (une lune, une planète voisine) fait glisser le globe
  vers lui, toujours dans la vue Planète, et le menu montre cet astre.
- **Espace proche.** Votre voisinage vu d'au-dessus d'un plan virtuel, sur
  le ciel du monde. Chaque grille pend au-dessus du plan par une ligne qui dit sa
  hauteur. Les planètes et les lunes cachent le plan, et les personnages
  des joueurs aussi, le vôtre compris : toute partie du plan qui passe à l'intérieur d'un astre ou d'un
  personnage, ou derrière lui, n'est pas dessinée. Le menu liste les grilles
  en vue ; un clic en surligne une. Un clic sur une planète ou une lune en vue la montre
  dans le menu ; un double clic fait glisser la caméra vers son globe, dans
  la vue Planète, et le menu présente cet astre. Un double
  clic sur une grille à portée attache la caméra à elle, et le jeu la dessine
  en plein écran ; sur une grille retenue, la vue se centre sur son repère et ses infos disent qu'elle est hors de portée.
  Tant que la caméra suit une grille, le plan reste où il était avant le double clic,
  et la ligne de hauteur de la grille grandit et rétrécit quand elle monte ou descend.
  Un simple clic dans le vide hors du cadre de la grille, un clic droit ou la molette lâchent
  la grille, de retour au-dessus de vous ; un glissement gauche ne fait que tourner autour de la grille.
  Le bouton Planète garde une grille proche d'une planète au milieu de son globe.
- **Système.** Deux onglets : **Système 2D**, les planètes et leurs lunes mises
  à plat par distance au centre du monde ; **Orbites**, le système en 3D
  avec les vraies distances et les astres agrandis, le soleil marqué sur le bord de
  l'écran dans sa vraie direction. Une plongée depuis la vue Système (double clic sur
  un astre ou une grille) y revient tout droit quand vous dézoomez. Les globes des astres sont peints
  d'après les vraies planètes dès que vous rejoignez un monde, une image grossière
  en quelques images, puis la complète, pour qu'ils soient prêts quand la carte s'ouvre. Chacun
  montre son propre relief et le terrain que le jeu met à chaque endroit, de la
  couleur dont le jeu le peint de loin, ou sinon de la couleur moyenne de la propre texture du terrain :
  vert et bleu pour EarthLike, rouge pour Mars, et pareil pour les planètes d'un mod de monde.
  Un globe une fois peint reste peint quand vous fermez et rouvrez la carte, et quand
  vous quittez puis rejoignez le même monde. Les noms des onglets rétrécissent pour tenir dans leurs boutons dans
  toutes les langues.
- **Galaxie.** Chaque serveur a une place fixe calculée à partir de son adresse,
  sur un ciel noir et étoilé qui remplit la vue, aussi loin que vous dézoomiez. Dézoomez
  du système jusqu'au bout (ou deux crans depuis Système 2D) et la carte glisse
  vers la galaxie ; zoomez et elle revient au système. La galaxie
  se pose toujours sur son entier, si bien que chaque serveur se retrouve au même endroit d'une
  ouverture de la carte à l'autre, quel que soit le serveur où vous êtes. Chaque cran de la molette fait
  grandir ou rétrécir l'image doucement autour du curseur, le point dessous y restant. Le ciel
  derrière est clairsemé : quelques étoiles douces et rondes avec un halo léger, aux couleurs
  douces (blanc, bleu pâle, orange, rose), en deux couches qui dérivent à leur propre allure,
  pour que les bras de la galaxie et les noms des serveurs se lisent d'abord. Le serveur sur lequel vous jouez
  brille avec « Vous êtes ici ». Les serveurs que vous avez visités ou gardés en favoris portent leur nom ; quand
  plusieurs sont proches, leurs lignes s'empilent dans une colonne à côté de leurs
  places, un trait fin vers chaque vraie place, si bien qu'aucune icône ni aucun nom n'en couvre
  un autre et que chacun peut être survolé et cliqué. Les autres serveurs sont des points
  qui s'étalent sur des anneaux autour du milieu de leur groupe de la même façon.
  Les filtres du menu (favoris, visités, recherche) sont gardés d'un lancement du jeu à l'autre
  et pour chaque serveur ; le filtre en vigueur est nommé dans le menu et
  dans le coin de la carte.

Les passages entre les vues ne dépendent que de l'échelle, avec un écart
entre la sortie et le retour, si bien que les vues n'alternent jamais quand
la molette s'arrête sur une limite.

## Mémoire

Tout ce qui entre dans votre portée de synchronisation est vu : sa position et l'heure
sont enregistrées. Au-delà de cette portée le jeu n'envoie plus rien, donc un repère reste
au dernier endroit où il a été vu, grisé, avec « vu il y a ... ». Revenir près d'un
lieu le met à jour : ce qui s'y trouve est vu de nouveau, ce qui n'y est plus est oublié.

La mémoire est gardée sur votre machine, une par serveur, dans
`%AppData%\SpaceEngineers\Storage\sir-holomap\Memory`. Les réglages (seuils de blocs
de la vue Planète et de l'espace, filtres de la galaxie, ce qu'il faut montrer) sont gardés dans
`settings.xml` à côté, les mêmes pour tous les serveurs.

## Suivi des fonctionnalités

Le greffon déclare dans `fonctionnalites.json` ce que les joueurs peuvent utiliser à part, et le
dit à Sir Studio Torch, qui compte **1 point par joueur, par jour et par fonctionnalité** et le
montre dans l'onglet Statistiques de chaque univers (carte « Fonctionnalités »). Il en déclare quatre :

| Clé | Libellé | Un point quand le joueur… |
| --- | --- | --- |
| `holomap.carte` | Ouvrir la carte | ouvre la carte (M) |
| `holomap.systeme` | Vue Système | passe en vue Système, en 2D ou en 3D |
| `holomap.galaxie` | Vue Galaxie | passe à l'onglet Galaxie |
| `holomap.suivi_grille` | Suivi d'une grille | suit une grille en Espace proche |

Un point ne se note que par un geste du joueur : cocher ou décocher une case du
greffon n'en envoie jamais. Une clé ne change jamais ; son libellé peut changer. Le point est mis en
file au geste, sans rien lire, écrire ni envoyer sur le fil du jeu ; il part ensuite, au plus
une fois par clé et par jour (heure de Paris, comme Sir Studio Torch) et par session, par un message réseau sur le canal
commun du suivi (`SendMessageToServer`, canal 46481), dont le texte UTF-8 est
`<identifiant Steam>;<clé>` (par exemple `76561198000000001;holomap.carte`). Il ne part que
quand le joueur est sur l'un de nos serveurs (PU ou PTU) ; ailleurs, il est jeté. Une faute d'envoi
est avalée : elle n'atteint jamais le jeu.

## Rapports d'erreurs

Quand le greffon va mal, le studio le sait. Chaque exception que le greffon rattrape
et chaque ligne d'erreur qu'il écrit au journal du jeu devient un rapport de genre `erreur` ; la même erreur
(même message, même haut de pile) fait un seul rapport par session, avec le nombre de fois où elle
est revenue et sa première et sa dernière heure. Le greffon pose aussi une marque dans son dossier quand une session
commence et la retire quand le jeu est quitté proprement : une marque trouvée au
démarrage suivant veut dire que le jeu a planté. Le greffon relit alors le journal de cette session (le
`SpaceEngineers_*.log` d'avant le courant, dans `%AppData%\SpaceEngineers`)
et en fait un rapport de genre `plantage`, avec le bloc CRASH INFO du jeu, l'exception qui l'a fait
tomber et sa pile, les 20 dernières lignes d'erreur du jeu et les 20 dernières lignes du greffon.

Un rapport est un document JSON UTF-8 `{ greffon, version, genre, message, pile,
premiere, derniere, nombre, session, lignes }` envoyé par
`MyAPIGateway.Multiplayer.SendMessageToServer` sur le canal 5465479001 (l'API du jeu
adresse un message sur 16 bits, donc le message part sur les 16 bits de poids
faible de ce nombre, 38745). Il ne porte rien sur le joueur : une ligne de journal
avec un identifiant Steam, un pseudo, un nom de grille ou une adresse de serveur est
retirée, et le dossier de l'utilisateur Windows s'écrit `%USERPROFILE%`.

Les rapports ne partent que quand vous êtes sur l'un de nos serveurs (PU ou PTU, reconnus par
le nom sous lequel le serveur est listé, qui contient « Sirius Immersion »).
Partout ailleurs (solo, partie hébergée, tout autre serveur) ils attendent, un fichier
chacun, dans `%AppData%\SpaceEngineers\Storage\sir-holomap\reports`, et partent à la
prochaine connexion à l'un des nôtres ; le fichier est effacé une fois envoyé. Une faute
en préparant ou en envoyant un rapport est avalée : elle n'atteint jamais le jeu.

## Rejoindre un autre serveur

Un double clic sur un serveur de la galaxie demande d'abord au serveur s'il
répond, s'il a de la place et s'il fait tourner cette version du jeu ; à chacune de ces défaillances
vous restez où vous êtes, avec un message qui dit pourquoi. Puis une confirmation nomme
le serveur ; quand le serveur demande un mot de passe, elle porte une case pour le taper. Son
texte est coupé à la largeur de la case et la case grandit pour le porter, dans toutes les
langues et pour les noms de serveur les plus longs.
Le mot de passe est remis à la connexion propre du jeu quand elle le demande, et n'est pas
gardé. Alors seulement le jeu quitte le serveur actuel. Si l'autre
serveur refuse encore une fois l'actuel quitté (un mauvais mot de passe, par
exemple), le jeu est de retour à son menu principal et la carte propose de revenir au serveur
que vous venez de quitter.

## Langues

La carte parle la langue réglée dans les options du jeu, pour chaque langue
que le jeu offre ; un texte absent d'une langue se montre en français, la langue de référence. Les textes sont
dans `Source/Logic/Texts.cs` (français) et `Source/Logic/Translations.*.cs`.

## Cohabiter avec d'autres greffons

La carte emprunte deux étapes du jeu tant qu'elle est ouverte : la caméra de rendu (pour
que le jeu dessine la vraie planète depuis le point de vue de la carte) et la direction du
soleil (la case « Globe éclairé »). Si un autre greffon tient déjà l'une des deux,
la carte n'y touche pas et le dit dans une notification. Sans la caméra, la vue Planète montre
un globe que la carte peint elle-même d'après le relief et les couleurs de terrain de la vraie planète,
tournant et zoomant avec la caméra de la carte, à un niveau de détail
plus grossier que celui du jeu. Sans la lumière, le globe garde celle du jeu.

## Construction

Pulsar compile lui-même le dossier `Source`. Pour une compilation locale, donnez le chemin
du dossier `Bin64` du jeu (voir `Directory.Build.props.example`) :

    dotnet build sir-holomap.csproj

Les tests couvrent la logique pure du greffon et tournent sans le jeu :

    dotnet test tests/tests.csproj
