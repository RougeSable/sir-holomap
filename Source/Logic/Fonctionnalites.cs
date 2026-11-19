namespace SirHolomap
{
    // Les fonctionnalités que le produit déclare au suivi des fonctionnalités de
    // Sir Studio Torch (fichier fonctionnalites.json, à la racine du dépôt). Une
    // clé ne change jamais ; son libellé peut changer.
    //
    // Une fonctionnalité se compte quand le joueur fait le geste qui l'actionne :
    // jamais quand il coche ou décoche une case du greffon.
    public static class Fonctionnalites
    {
        // Le joueur ouvre la carte (touche M).
        public const string Carte = "holomap.carte";
        public const string LibelleCarte = "Ouvrir la carte";

        // Le joueur passe en vue Système, en 2D ou en 3D.
        public const string Systeme = "holomap.systeme";
        public const string LibelleSysteme = "Vue Système";

        // Le joueur passe à l'onglet Galaxie.
        public const string Galaxie = "holomap.galaxie";
        public const string LibelleGalaxie = "Vue Galaxie";

        // Le joueur suit une grille en Espace proche.
        public const string SuiviGrille = "holomap.suivi_grille";
        public const string LibelleSuiviGrille = "Suivi d'une grille";

        // Toutes les clés déclarées, dans l'ordre du fichier.
        public static readonly string[] Cles = { Carte, Systeme, Galaxie, SuiviGrille };

        // Leurs libellés, dans le même ordre.
        public static readonly string[] Libelles = { LibelleCarte, LibelleSysteme, LibelleGalaxie, LibelleSuiviGrille };

        public static bool EstDeclaree(string cle)
        {
            return System.Array.IndexOf(Cles, cle) >= 0;
        }
    }
}
