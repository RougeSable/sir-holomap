using System;
using System.Collections.Generic;
using System.Text;

namespace SirHolomap
{
    // Les points de fonctionnalités en route vers Sir Studio Torch, qui les reçoit
    // et les écrit : 1 point par joueur, par jour et par fonctionnalité.
    //
    // Compter ne fait qu'une inscription en mémoire : il peut être appelé du fil
    // du jeu, au geste du joueur, sans rien lire ni écrire ni envoyer. L'envoi se
    // fait plus tard, par Vider, un message à la fois ; une faute d'envoi est
    // avalée et ne remonte jamais jusqu'au jeu. Rien ici ne lève d'exception.
    public sealed class RelevesFonctionnalites
    {
        // Le canal réseau commun du suivi, celui que Sir Studio Torch écoute : le
        // même pour tous nos produits, il ne change pas.
        public const int CanalDesPoints = 46481;

        // Le séparateur du message : « identifiant Steam ; produit.fonctionnalité ».
        public const char Separateur = ';';

        private readonly object m_verrou = new object();
        private readonly Func<DateTime> m_maintenant;
        private readonly List<string> m_enAttente = new List<string>();
        private readonly HashSet<string> m_vues = new HashSet<string>();
        private string m_jour = "";

        public RelevesFonctionnalites()
            : this(() => DateTime.UtcNow)
        {
        }

        public RelevesFonctionnalites(Func<DateTime> maintenant)
        {
            m_maintenant = maintenant ?? (() => DateTime.UtcNow);
        }

        // Le nombre de points qui attendent leur envoi.
        public int EnAttente
        {
            get
            {
                lock (m_verrou)
                    return m_enAttente.Count;
            }
        }

        // Le joueur vient de se servir de la fonctionnalité. Un seul point par
        // clé et par jour part d'une même session : les suivants ne font rien.
        public void Compter(string cle)
        {
            try
            {
                if (!Fonctionnalites.EstDeclaree(cle))
                    return;
                var jour = JourDeParis(m_maintenant());
                lock (m_verrou)
                {
                    if (jour != m_jour)
                    {
                        m_jour = jour;
                        m_vues.Clear();
                    }
                    if (m_vues.Add(cle))
                        m_enAttente.Add(cle);
                }
            }
            catch (Exception)
            {
            }
        }

        // Le jour du point, à l'heure de Paris : c'est ainsi que Sir Studio Torch
        // compte ses lignes, une par jour parisien. Si le fuseau est introuvable sur
        // la machine, on applique à la main l'heure d'Europe centrale (UTC+1, UTC+2
        // du dernier dimanche de mars au dernier dimanche d'octobre, à 1 h UTC).
        public static string JourDeParis(DateTime instant)
        {
            var utc = instant.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(instant, DateTimeKind.Utc)
                : instant.ToUniversalTime();
            foreach (var identifiant in new[] { "Romance Standard Time", "Europe/Paris" })
            {
                try
                {
                    var fuseau = TimeZoneInfo.FindSystemTimeZoneById(identifiant);
                    return TimeZoneInfo.ConvertTimeFromUtc(utc, fuseau).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                }
            }
            var debutEte = DernierDimanche(utc.Year, 3).AddHours(1);
            var finEte = DernierDimanche(utc.Year, 10).AddHours(1);
            var decalage = utc >= debutEte && utc < finEte ? 2 : 1;
            return utc.AddHours(decalage).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static DateTime DernierDimanche(int annee, int mois)
        {
            var jour = new DateTime(annee, mois, DateTime.DaysInMonth(annee, mois), 0, 0, 0, DateTimeKind.Utc);
            return jour.AddDays(-(int)jour.DayOfWeek);
        }

        // Envoie ce qui attend, par la fonction d'envoi (vraie quand le message est
        // parti). Un point dont l'envoi échoue, ou lève une faute, reste en
        // attente pour le tour suivant. Rend le nombre de points partis.
        public int Vider(ulong joueur, Func<byte[], bool> envoyer)
        {
            if (envoyer == null || joueur == 0)
                return 0;
            string[] cles;
            lock (m_verrou)
            {
                if (m_enAttente.Count == 0)
                    return 0;
                cles = m_enAttente.ToArray();
            }
            var partis = 0;
            foreach (var cle in cles)
            {
                var parti = false;
                try
                {
                    parti = envoyer(Message(joueur, cle));
                }
                catch (Exception)
                {
                }
                if (!parti)
                    continue;
                lock (m_verrou)
                    m_enAttente.Remove(cle);
                partis++;
            }
            return partis;
        }

        // Ce qui est jeté : un joueur qui quitte nos serveurs ne garde pas de
        // point pour un autre jour.
        public void Oublier()
        {
            lock (m_verrou)
                m_enAttente.Clear();
        }

        // Le message envoyé au serveur : « <identifiant Steam>;<produit>.<fonctionnalité> »
        // en UTF-8. Le serveur ignore un message dont l'identifiant n'est pas celui
        // de l'expéditeur.
        public static byte[] Message(ulong joueur, string cle)
        {
            return Encoding.UTF8.GetBytes(joueur.ToString(System.Globalization.CultureInfo.InvariantCulture) + Separateur + cle);
        }
    }
}
