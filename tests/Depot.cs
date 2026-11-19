using System;
using System.IO;

namespace SirHolomap.Tests
{
    // Les fichiers du dépôt que les tests relisent (fiche des fonctionnalités,
    // sources du jeu), retrouvés en remontant depuis le dossier des tests.
    internal static class Depot
    {
        public static string Racine()
        {
            var dossier = new DirectoryInfo(AppContext.BaseDirectory);
            while (dossier != null)
            {
                if (File.Exists(Path.Combine(dossier.FullName, "fonctionnalites.json")))
                    return dossier.FullName;
                dossier = dossier.Parent;
            }
            throw new InvalidOperationException("Racine du dépôt introuvable depuis " + AppContext.BaseDirectory);
        }

        public static string Lire(string relatif)
        {
            return File.ReadAllText(Path.Combine(Racine(), relatif.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
