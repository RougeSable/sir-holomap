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

                reporter.ErrorLine("terrain of EarthLike not read", T0);
                var later = T0.AddMinutes(10);

                // Solo, a hosted game, any other server: nothing leaves, the
                // report waits in the plugin's folder.
                Assert.Equal(0, reporter.Pump(false, send, later, false));
                Assert.Equal(0, reporter.Pump(false, send, later, true));
                Assert.Empty(sent);
                Assert.Single(Directory.GetFiles(Path.Combine(folder.Path, ReportQueue.FolderName), "*.json"));

                // Even after a restart of the game it is still there ...
                var restarted = new ErrorReporter(folder.Path, "1.0.0.0", later, new ReportScrubber());
                Assert.Single(restarted.Queue.Waiting());

                // ... and leaves at the first connection to one of ours.
                Assert.Equal(1, restarted.Pump(true, send, later.AddSeconds(10), false));
                Assert.Single(sent);
                using (var document = JsonDocument.Parse(sent[0]))
                {
                    Assert.Equal("erreur", document.RootElement.GetProperty("genre").GetString());
                    Assert.Equal("terrain of EarthLike not read", document.RootElement.GetProperty("message").GetString());
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
            Assert.False(OurServers.IsOurs("Some other server", true));
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
                reporter.ErrorLine("planet material rules not found", T0);
                var later = T0.AddMinutes(10);

                // The game refuses the message.
                Assert.Equal(0, reporter.Pump(true, bytes => false, later, false));
                // The send itself throws.
                Assert.Equal(0, reporter.Pump(true, bytes => { throw new InvalidOperationException("no channel"); }, later, true));
                // The report is still there for the next try, and goes then.
                Assert.Single(reporter.Queue.Waiting());
                Assert.Equal(1, reporter.Pump(true, bytes => true, later.AddMinutes(1), false));

                // Preparing a report never throws either: a null exception,
                // a text that cannot be cleaned, a folder that cannot be
                // written.
                reporter.Caught("update", null, T0);
                reporter.CaughtLine(null, null, T0);
                reporter.ErrorLine(null, T0);
                reporter.Note(null);

                var blocked = Path.Combine(folder.Path, "blocked");
                File.WriteAllText(blocked, "a file where a folder should be");
                var broken = new ErrorReporter(blocked, "1.0.0.0", Session, new ReportScrubber());
                broken.ErrorLine("any error", T0);
                broken.Flush();
                Assert.Equal(0, broken.Pump(true, bytes => true, later, true));
                broken.Enqueue(new ErrorReport());

                // The library code's hook swallows what its listener throws.
                Faults.CaughtHandler = (where, e) => { throw new InvalidOperationException("listener"); };
                try
                {
                    Faults.Caught("somewhere", new Exception("caught"));
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
