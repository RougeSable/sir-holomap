using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SirHolomap.Tests
{
    public class ReportQueueTests
    {
        private static readonly DateTime Session = new DateTime(2026, 10, 27, 19, 3, 12, DateTimeKind.Utc);
        private static readonly DateTime T0 = new DateTime(2026, 10, 27, 19, 10, 0, DateTimeKind.Utc);

        [Fact]
        public void ReportsWaitOutsideOurServersAndAreSentOnNextConnection()
        {
            using (var folder = new TempFolder())
            {
                var reporter = new ErrorReporter(folder.Path, "1.0.0.0", Session, new ReportScrubber());
                var sent = new List<byte[]>();
                Func<byte[], bool> send = bytes =>
                {
                    sent.Add(bytes);
                    return true;
                };

                reporter.ErrorLine("terrain de EarthLike non lu", T0);
                var later = T0.AddMinutes(10);

                // Solo, une partie hébergée, tout autre serveur : rien ne part, le
                // rapport attend dans le dossier du greffon.
                Assert.Equal(0, reporter.Pump(false, send, later, false));
                Assert.Equal(0, reporter.Pump(false, send, later, true));
                Assert.Empty(sent);
                Assert.Single(Directory.GetFiles(Path.Combine(folder.Path, ReportQueue.FolderName), "*.json"));

                // Même après un redémarrage du jeu il est encore là ...
                var restarted = new ErrorReporter(folder.Path, "1.0.0.0", later, new ReportScrubber());
                Assert.Single(restarted.Queue.Waiting());

                // ... et part à la première connexion à l'un des nôtres.
                Assert.Equal(1, restarted.Pump(true, send, later.AddSeconds(10), false));
                Assert.Single(sent);
                using (var document = JsonDocument.Parse(sent[0]))
                {
                    Assert.Equal("erreur", document.RootElement.GetProperty("genre").GetString());
                    Assert.Equal("terrain de EarthLike non lu", document.RootElement.GetProperty("message").GetString());
                }
                Assert.Empty(restarted.Queue.Waiting());
                Assert.Empty(Directory.GetFiles(Path.Combine(folder.Path, ReportQueue.FolderName), "*.json"));
            }
        }

        [Fact]
        public void OnlyOurServersAreOurs()
        {
            Assert.True(OurServers.IsOurs("Sirius Immersion PTU", true));
            Assert.True(OurServers.IsOurs("[FR] SIRIUS IMMERSION - PU", true));
            Assert.False(OurServers.IsOurs("Un autre serveur", true));
            Assert.False(OurServers.IsOurs("Sirius Immersion PTU", false));
            Assert.False(OurServers.IsOurs("", true));
            Assert.False(OurServers.IsOurs(null, true));
        }

        [Fact]
        public void SendFailureIsSwallowed()
        {
            using (var folder = new TempFolder())
            {
                var reporter = new ErrorReporter(folder.Path, "1.0.0.0", Session, new ReportScrubber());
                reporter.ErrorLine("règles de matériaux de planète introuvables", T0);
                var later = T0.AddMinutes(10);

                // Le jeu refuse le message.
                Assert.Equal(0, reporter.Pump(true, bytes => false, later, false));
                // L'envoi lui-même lève une exception.
                Assert.Equal(0, reporter.Pump(true, bytes => { throw new InvalidOperationException("pas de canal"); }, later, true));
                // Le rapport est encore là pour l'essai suivant, et part alors.
                Assert.Single(reporter.Queue.Waiting());
                Assert.Equal(1, reporter.Pump(true, bytes => true, later.AddMinutes(1), false));

                // Préparer un rapport ne lève jamais non plus : une exception nulle,
                // un texte qu'on ne peut pas nettoyer, un dossier où l'on ne peut pas
                // écrire.
                reporter.Caught("update", null, T0);
                reporter.CaughtLine(null, null, T0);
                reporter.ErrorLine(null, T0);
                reporter.Note(null);

                var blocked = Path.Combine(folder.Path, "blocked");
                File.WriteAllText(blocked, "un fichier là où un dossier devrait être");
                var broken = new ErrorReporter(blocked, "1.0.0.0", Session, new ReportScrubber());
                broken.ErrorLine("une erreur", T0);
                broken.Flush();
                Assert.Equal(0, broken.Pump(true, bytes => true, later, true));
                broken.Enqueue(new ErrorReport());

                // Le crochet du code de la bibliothèque avale ce que son écouteur lève.
                Faults.CaughtHandler = (where, e) => { throw new InvalidOperationException("listener"); };
                try
                {
                    Faults.Caught("quelque part", new Exception("rattrapée"));
                }
                finally
                {
                    Faults.CaughtHandler = null;
                }
            }
        }

        [Fact]
        public void TheQueueIsBounded()
        {
            using (var folder = new TempFolder())
            {
                var queue = new ReportQueue(folder.Path);
                for (var i = 0; i < ReportQueue.MaximumFiles + 20; i++)
                    queue.Save("report-" + i.ToString("000"), new ErrorReport { FirstUtc = T0, LastUtc = T0, SessionUtc = Session });
                Assert.Equal(ReportQueue.MaximumFiles, queue.Waiting().Count);
            }
        }
    }
}
