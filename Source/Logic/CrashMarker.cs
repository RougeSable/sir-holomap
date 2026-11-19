using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace SirHolomap
{
    // Comment un plantage se reconnaît au démarrage suivant : une marque est posée dans le
    // dossier du greffon quand une session commence et retirée quand elle se termine
    // proprement. Une marque trouvée au démarrage suivant, c'est une session qui n'a pas fini.
    public static class CrashMarker
    {
        public const string FileName = "session.mark";

        // Pose la marque de cette session. Rend l'heure de début de la session qui a laissé
        // sa marque, ou null quand la dernière session s'est terminée proprement
        // (ou qu'il n'y en avait pas).
        public static DateTime? Begin(string folder, DateTime nowUtc)
        {
            var path = Path.Combine(folder, FileName);
            DateTime? previous = null;
            if (File.Exists(path))
            {
                DateTime parsed;
                var text = "";
                try
                {
                    text = File.ReadAllText(path, Encoding.UTF8).Trim();
                }
                catch (Exception)
                {
                }
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed))
                    previous = parsed;
                else
                    previous = File.GetLastWriteTimeUtc(path);
            }
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, ErrorReportJson.Time(nowUtc), new UTF8Encoding(false));
            return previous;
        }

        // La session s'est terminée proprement : aucune marque ne reste.
        public static void End(string folder)
        {
            var path = Path.Combine(folder, FileName);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
