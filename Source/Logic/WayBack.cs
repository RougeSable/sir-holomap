using System;

namespace SirHolomap
{
    // Partir pour un autre serveur depuis la galaxie passe par le menu principal,
    // comme avec le navigateur de serveurs du jeu. La plupart des refus sont attrapés avant
    // le départ (pas de réponse, plein, mot de passe, autre version) ; quand l'autre
    // serveur refuse malgré tout ensuite, le jeu reste au menu principal. Le
    // chemin du retour s'en aperçoit et propose de revenir au serveur que le joueur
    // a quitté.
    public sealed class WayBack
    {
        // Le jeu a besoin d'un moment entre le déchargement du monde et l'affichage
        // de ses écrans de connexion : aucune conclusion avant.
        public static readonly TimeSpan Settle = TimeSpan.FromSeconds(10);

        // Inactif au menu aussi longtemps, sans rien rejoindre ni charger :
        // la connexion a échoué.
        public static readonly TimeSpan Idle = TimeSpan.FromSeconds(4);

        // Après cela, tout ce qui arrive est le fait du joueur.
        public static readonly TimeSpan GiveUp = TimeSpan.FromMinutes(15);

        public string BackConnection { get; private set; }
        public string BackName { get; private set; }
        public string TargetName { get; private set; }

        private DateTime m_leftAt;
        private DateTime? m_idleSince;

        public bool Armed { get; private set; }

        public void Leave(string backConnection, string backName, string targetName, DateTime now)
        {
            Armed = !string.IsNullOrEmpty(backConnection);
            BackConnection = backConnection ?? "";
            BackName = string.IsNullOrEmpty(backName) ? BackConnection : backName;
            TargetName = targetName ?? "";
            m_leftAt = now;
            m_idleSince = null;
        }

        public void Disarm()
        {
            Armed = false;
            m_idleSince = null;
        }

        // Appelé souvent. inWorld : un monde est chargé (la connexion a marché, ou le
        // joueur est allé ailleurs). busy : le jeu montre un écran de connexion ou de
        // chargement ; null quand on ne peut pas le dire, et rien n'est alors proposé.
        // Vrai une seule fois, quand le chemin du retour doit être proposé.
        public bool Update(DateTime now, bool inWorld, bool? busy)
        {
            if (!Armed)
                return false;
            if (now - m_leftAt > GiveUp)
            {
                Disarm();
                return false;
            }
            // Un monde juste après le départ peut encore être celui qu'on quitte :
            // ce n'est qu'une fois stabilisé qu'un monde veut dire que la connexion a marché.
            if (inWorld)
            {
                m_idleSince = null;
                if (now - m_leftAt >= Settle)
                    Disarm();
                return false;
            }
            if (busy == null)
                return false;
            if (busy.Value)
            {
                m_idleSince = null;
                return false;
            }
            if (m_idleSince == null)
                m_idleSince = now;
            if (now - m_leftAt >= Settle && now - m_idleSince.Value >= Idle)
            {
                Disarm();
                return true;
            }
            return false;
        }
    }
}
