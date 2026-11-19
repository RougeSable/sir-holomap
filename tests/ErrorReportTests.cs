using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace SirHolomap.Tests
{
    public class ErrorReportTests
    {
        private static readonly DateTime Session = new DateTime(2026, 10, 27, 19, 3, 12, DateTimeKind.Utc);
        private static readonly DateTime T0 = new DateTime(2026, 10, 27, 19, 10, 0, DateTimeKind.Utc);

        // Une lettre hors ASCII, pour prouver que le rapport est en UTF-8.
        private static readonly string Accent = ((char)0xE9).ToString();

        private readonly ITestOutputHelper m_output;

        public ErrorReportTests(ITestOutputHelper output)
        {
            m_output = output;
        }

        private static ErrorReporter NewReporter(TempFolder folder)
        {
            return new ErrorReporter(folder.Path, "1.0.0.0", Session, new ReportScrubber());
        }

        private static List<JsonDocument> Written(ErrorReporter reporter)
        {
            var documents = new List<JsonDocument>();
            foreach (var id in reporter.Queue.Waiting())
                documents.Add(JsonDocument.Parse(reporter.Queue.Read(id)));
            return documents;
        }

        [Fact]
        public void ReportHasTheRuleShape()
        {
            var report = new ErrorReport
            {
                Version = "1.0.0.0",
                Kind = ReportRule.KindError,
                Message = "terrain de EarthLike non lu : caf" + Accent + " \"quoted\"",
                Stack = "   at A.B()\n   at C.D()",
                FirstUtc = T0,
                LastUtc = T0.AddMinutes(2),
                Count = 3,
                SessionUtc = Session,
            };
            report.Lines.Add("[sir-holomap] first");
            report.Lines.Add("[sir-holomap] second");

            var bytes = ErrorReportJson.ToBytes(report);
            var json = new UTF8Encoding(false, true).GetString(bytes);
            m_output.WriteLine(json);

            // UTF-8, sans marque d'ordre des octets.
            Assert.Equal((byte)'{', bytes[0]);

            using (var document = JsonDocument.Parse(bytes))
            {
                var root = document.RootElement;
                var names = root.EnumerateObject().Select(p => p.Name).ToArray();
                Assert.Equal(new[] { "greffon", "version", "genre", "message", "pile", "premiere", "derniere", "nombre", "session", "lignes" }, names);
                Assert.Equal("Sir Holomap", root.GetProperty("greffon").GetString());
                Assert.Equal("1.0.0.0", root.GetProperty("version").GetString());
                Assert.Equal("erreur", root.GetProperty("genre").GetString());
                Assert.Equal("terrain de EarthLike non lu : caf" + Accent + " \"quoted\"", root.GetProperty("message").GetString());
                Assert.Equal("   at A.B()\n   at C.D()", root.GetProperty("pile").GetString());
                Assert.Equal("2026-10-27T19:10:00.000Z", root.GetProperty("premiere").GetString());
                Assert.Equal("2026-10-27T19:12:00.000Z", root.GetProperty("derniere").GetString());
                Assert.Equal(JsonValueKind.Number, root.GetProperty("nombre").ValueKind);
                Assert.Equal(3, root.GetProperty("nombre").GetInt32());
                Assert.Equal("2026-10-27T19:03:12.000Z", root.GetProperty("session").GetString());
                var lines = root.GetProperty("lignes");
                Assert.Equal(JsonValueKind.Array, lines.ValueKind);
                Assert.Equal(2, lines.GetArrayLength());
                Assert.Equal("[sir-holomap] second", lines[1].GetString());
            }

            // Les heures sont en UTC, ISO 8601.
            Assert.Matches(new Regex("\"premiere\":\"\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z\""), json);
            Assert.Matches(new Regex("\"derniere\":\"\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z\""), json);

            // La règle : le nom du greffon, le canal unique.
            Assert.Equal("Sir Holomap", ReportRule.PluginName);
            Assert.Equal(5465479001L, ReportRule.Channel);
            Assert.Equal("plantage", ReportRule.KindCrash);
        }

        [Fact]
        public void CaughtExceptionProducesErrorReport()
        {
            using (var folder = new TempFolder())
            {
                var reporter = NewReporter(folder);
                Exception caught = null;
                try
                {
                    throw new InvalidOperationException("globe pas prêt");
                }
                catch (Exception e)
                {
                    caught = e;
                    reporter.Caught("faute dans la mise à jour", e, T0);
                }
                reporter.Flush();

                var reports = Written(reporter);
                Assert.Single(reports);
                var root = reports[0].RootElement;
                m_output.WriteLine(root.GetRawText());
                Assert.Equal("erreur", root.GetProperty("genre").GetString());
                Assert.Contains("System.InvalidOperationException", root.GetProperty("message").GetString());
                Assert.Contains("globe pas prêt", root.GetProperty("message").GetString());
                Assert.Contains("CaughtExceptionProducesErrorReport", root.GetProperty("pile").GetString());
                Assert.Equal(1, root.GetProperty("nombre").GetInt32());
                Assert.NotNull(caught);
            }
        }

        [Fact]
        public void LoggedErrorLineProducesErrorReport()
        {
            using (var folder = new TempFolder())
            {
                var reporter = NewReporter(folder);
                reporter.Note("[sir-holomap] Sir Holomap chargé ; caméra accrochée, 3 serveurs dans l'historique");
                reporter.ErrorLine("cache de forme de planète introuvable : les globes peuvent être peints en gris", T0);
                reporter.Flush();

                var reports = Written(reporter);
                Assert.Single(reports);
                var root = reports[0].RootElement;
                Assert.Equal("erreur", root.GetProperty("genre").GetString());
                Assert.Equal("cache de forme de planète introuvable : les globes peuvent être peints en gris", root.GetProperty("message").GetString());
                Assert.Equal("1.0.0.0", root.GetProperty("version").GetString());
                Assert.Equal("2026-10-27T19:03:12.000Z", root.GetProperty("session").GetString());
                // Les dernières lignes que le greffon a écrites l'accompagnent.
                Assert.Equal("[sir-holomap] Sir Holomap chargé ; caméra accrochée, 3 serveurs dans l'historique", root.GetProperty("lignes")[0].GetString());
            }
        }

        [Fact]
        public void SameErrorIsSentOncePerSessionWithCount()
        {
            using (var folder = new TempFolder())
            {
                var reporter = NewReporter(folder);
                var sent = new List<string>();
                Func<byte[], bool> send = bytes =>
                {
                    sent.Add(Encoding.UTF8.GetString(bytes));
                    return true;
                };

                // La même ligne cinq fois, puis une autre erreur.
                for (var i = 0; i < 5; i++)
                    reporter.ErrorLine("terrain de EarthLike non lu : NullReferenceException", T0.AddSeconds(i * 10));
                reporter.ErrorLine("règles de matériaux de planète introuvables", T0.AddSeconds(60));

                // La même exception du même endroit, trois fois.
                for (var i = 0; i < 3; i++)
                    Throwing(reporter, T0.AddSeconds(i));
                // Le même message d'un autre endroit est une autre erreur.
                Elsewhere(reporter, T0);

                Assert.Equal(4, reporter.Kinds);

                // Les erreurs réglées partent une fois chacune, avec leur compte.
                var later = T0.AddMinutes(10);
                Assert.Equal(4, reporter.Pump(true, send, later, false));
                // Rien ne part deux fois, quoi qu'il arrive ensuite.
                reporter.ErrorLine("terrain de EarthLike non lu : NullReferenceException", later);
                Assert.Equal(0, reporter.Pump(true, send, later.AddMinutes(10), true));
                Assert.Equal(4, sent.Count);

                var terrain = sent.Select(s => JsonDocument.Parse(s).RootElement)
                    .Single(r => r.GetProperty("message").GetString().StartsWith("terrain de EarthLike"));
                m_output.WriteLine(terrain.GetRawText());
                Assert.Equal(5, terrain.GetProperty("nombre").GetInt32());
                Assert.Equal("2026-10-27T19:10:00.000Z", terrain.GetProperty("premiere").GetString());
                Assert.Equal("2026-10-27T19:10:40.000Z", terrain.GetProperty("derniere").GetString());

                var thrown = sent.Select(s => JsonDocument.Parse(s).RootElement)
                    .Single(r => r.GetProperty("pile").GetString().Contains("Throwing") && r.GetProperty("nombre").GetInt32() == 3);
                Assert.Equal("erreur", thrown.GetProperty("genre").GetString());
            }
        }

        private static void Throwing(ErrorReporter reporter, DateTime when)
        {
            try
            {
                throw new InvalidOperationException("même message");
            }
            catch (Exception e)
            {
                reporter.Caught("update", e, when);
            }
        }

        private static void Elsewhere(ErrorReporter reporter, DateTime when)
        {
            try
            {
                throw new InvalidOperationException("même message");
            }
            catch (Exception e)
            {
                reporter.Caught("update", e, when);
            }
        }
    }
}
