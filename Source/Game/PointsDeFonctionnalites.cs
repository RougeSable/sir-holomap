using System;
using Sandbox.ModAPI;

namespace SirHolomap
{
    // Le côté jeu du suivi des fonctionnalités : quand le joueur fait un geste qui
    // actionne l'une d'elles, un point est mis en file ; le tour d'envoi le fait
    // partir vers Sir Studio Torch, qui l'écrit. Rien ne part hors de nos
    // serveurs (PU et PTU), et tout ce qui va mal ici reste ici : le jeu ne le voit
    // jamais.
    internal static class PointsDeFonctionnalites
    {
        // Mises à jour entre deux tours d'envoi (60 mises à jour par seconde).
        private const int IntervalleDEnvoi = 120;

        private static readonly RelevesFonctionnalites s_releves = new RelevesFonctionnalites();
        private static int s_compteur;

        // Le joueur vient de faire le geste d'une fonctionnalité. Jamais appelé pour
        // une case cochée ou décochée.
        public static void Point(string cle, MapWorld monde)
        {
            try
            {
                if (ErrorReporting.OnOurServer(monde))
                    s_releves.Compter(cle);
            }
            catch (Exception)
            {
            }
        }

        // À chaque mise à jour du greffon.
        public static void Tick(MapWorld monde)
        {
            try
            {
                s_compteur++;
                if (s_compteur % IntervalleDEnvoi != 0)
                    return;
                Vider(monde);
            }
            catch (Exception)
            {
            }
        }

        // À la sortie propre du jeu : ce qui attend part si un serveur à nous est là.
        public static void Arreter(MapWorld monde)
        {
            try
            {
                Vider(monde);
            }
            catch (Exception)
            {
            }
        }

        private static void Vider(MapWorld monde)
        {
            if (s_releves.EnAttente == 0)
                return;
            if (!ErrorReporting.OnOurServer(monde))
            {
                // Un point n'a de sens que sur le serveur où le geste a eu lieu.
                s_releves.Oublier();
                return;
            }
            var multijoueur = MyAPIGateway.Multiplayer;
            if (multijoueur == null)
                return;
            s_releves.Vider(multijoueur.MyId, Envoyer);
        }

        private static bool Envoyer(byte[] message)
        {
            try
            {
                var multijoueur = MyAPIGateway.Multiplayer;
                return multijoueur != null && multijoueur.SendMessageToServer((ushort)RelevesFonctionnalites.CanalDesPoints, message, true);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
