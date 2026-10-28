using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace SirHolomap.Tests
{
    public class CrashDetectionTests
    {
        private readonly ITestOutputHelper m_output;

        public CrashDetectionTests(ITestOutputHelper output)
        {
            m_output = output;
        }

        private static ReportScrubber PlayerScrubber()
        {
            // What the plugin knows of the player when it scrubs.
            var scrubber = new ReportScrubber();
            scrubber.AddPrivate(GameLogOf20261027.Nickname);
            scrubber.AddPrivate(GameLogOf20261027.GridName);
            scrubber.AddPrivate(GameLogOf20261027.WindowsUser);
            scrubber.AddPrivate(GameLogOf20261027.ServerName);
            return scrubber;
        }

        [Fact]
        public void CleanExitProducesNoCrashReport()
        {
            using (var folder = new TempFolder())
            {
                var first = new DateTime(2026, 10, 27, 19, 3, 12, DateTimeKind.Utc);
                var second = first.AddHours(2);

                // The very first start: no mark behind it.
                Assert.Null(CrashMarker.Begin(folder.Path, first));
                Assert.True(File.Exists(Path.Combine(folder.Path, CrashMarker.FileName)));

                // The session ends cleanly: the mark is taken away.
                CrashMarker.End(folder.Path);
                Assert.False(File.Exists(Path.Combine(folder.Path, CrashMarker.FileName)));

                // The next start finds nothing: no report of a crash.
                Assert.Null(CrashMarker.Begin(folder.Path, second));
                CrashMarker.End(folder.Path);

                // This one does not end: the start after it finds its mark.
                var third = second.AddHours(1);
                var crashed = CrashMarker.Begin(folder.Path, third);
                Assert.Null(crashed);
                var found = CrashMarker.Begin(folder.Path, third.AddHours(1));
                Assert.Equal(third, found.Value);
            }
        }

        [Fact]
        public void CrashReportIsBuiltFromPreviousGameLog()
        {
            using (var folder = new TempFolder())
            {
                // %AppData%\SpaceEngineers with the log of the crashed
                // session and the log of the session running now.
                var logs = Path.Combine(folder.Path, "SpaceEngineers");
                Directory.CreateDirectory(logs);
                var crashedLog = Path.Combine(logs, "SpaceEngineers_20261027_210311_405.log");
                var currentLog = Path.Combine(logs, "SpaceEngineers_20261027_211900_001.log");
                File.WriteAllText(crashedLog, GameLogOf20261027.Text(), new UTF8Encoding(false));
                File.WriteAllText(currentLog, "2026-10-27 21:19:00.100 - Thread:   1 ->  Version: SpaceEngineers 01_208_015\n", new UTF8Encoding(false));
                File.SetCreationTimeUtc(crashedLog, GameLogOf20261027.SessionStartUtc.AddSeconds(-1));
                File.SetCreationTimeUtc(currentLog, GameLogOf20261027.CrashedAtUtc.AddMinutes(4));
                File.SetLastWriteTimeUtc(crashedLog, GameLogOf20261027.CrashedAtUtc);

                var picked = GameLogFiles.Pick(GameLogFiles.List(logs), GameLogOf20261027.SessionStartUtc);
                Assert.Equal("SpaceEngineers_20261027_210311_405.log", picked.Name);

                var text = GameLogFiles.ReadTail(picked.Path, GameLogFiles.TailBytes);
                var report = GameLog.BuildCrashReport(text, "1.0.0.0", GameLogOf20261027.SessionStartUtc, picked.LastWriteUtc, PlayerScrubber());
                var json = ErrorReportJson.ToJson(report);
                m_output.WriteLine(json);

                Assert.Equal("plantage", report.Kind);
                Assert.Equal(1, report.Count);
                Assert.Equal(GameLogOf20261027.SessionStartUtc, report.SessionUtc);

                // The exception that brought the game down, and its stack.
                Assert.Contains("DXGI_ERROR_DEVICE_REMOVED", report.Message);
                Assert.StartsWith("Exception occurred: SharpDX.SharpDXException", report.Message);
                Assert.Contains("at SharpDX.DXGI.SwapChain.Present", report.Stack);
                Assert.Contains("at VRageRender.MyRender11.Present()", report.Stack);

                // The CRASH INFO block.
                var lines = report.Lines;
                var crashInfo = lines.IndexOf("-- CRASH INFO --");
                var exception = lines.IndexOf("-- EXCEPTION --");
                var gameErrors = lines.IndexOf("-- LAST 20 GAME ERROR LINES --");
                var pluginLines = lines.IndexOf("-- LAST 20 PLUGIN LINES --");
                Assert.True(crashInfo >= 0 && crashInfo < exception && exception < gameErrors && gameErrors < pluginLines);
                var block = lines.GetRange(crashInfo + 1, exception - crashInfo - 1);
                Assert.Contains(block, l => l.Contains("CRASH INFO"));
                Assert.Contains(block, l => l.Contains("GPU: NVIDIA GeForce RTX 3070"));

                // The 20 last error lines of the game: the exception that
                // closes the log, the CRASH INFO line, and audio banks 13 to 30.
                var errors = lines.GetRange(gameErrors + 1, pluginLines - gameErrors - 1);
                Assert.Equal(20, errors.Count);
                Assert.Contains(errors, l => l.Contains("Error loading audio bank 30:"));
                Assert.DoesNotContain(errors, l => l.Contains("Error loading audio bank 10:"));
                Assert.Contains(errors, l => l.Contains("DXGI_ERROR_DEVICE_REMOVED"));

                // The 20 last lines of the plugin, the two errors included.
                var plugin = lines.GetRange(pluginLines + 1, lines.Count - pluginLines - 1);
                Assert.Equal(20, plugin.Count);
                Assert.All(plugin, l => Assert.Contains("[sir-holomap]", l));
                Assert.Contains(plugin, l => l.Contains(GameLogOf20261027.ShapeCacheLine));
                Assert.Contains(plugin, l => l.Contains(GameLogOf20261027.TerrainLine));
                Assert.DoesNotContain(plugin, l => l.Contains("scan 1:"));

                // Nothing of the player.
                Assert.DoesNotContain(GameLogOf20261027.WindowsUser, json);
            }
        }

        [Fact]
        public void EarthLikeIncidentOf20261027IsReported()
        {
            using (var folder = new TempFolder())
            {
                // The session: the plugin writes its two errors ...
                var reporter = new ErrorReporter(folder.Path, "1.0.0.0", GameLogOf20261027.SessionStartUtc, PlayerScrubber());
                var at = new DateTime(2026, 10, 27, 19, 14, 2, DateTimeKind.Utc);
                reporter.ErrorLine(GameLogOf20261027.ShapeCacheLine, at);
                reporter.ErrorLine(GameLogOf20261027.TerrainLine, at.AddMilliseconds(20));
                reporter.Flush();

                // ... and the game goes down on the graphics card.
                var crash = GameLog.BuildCrashReport(
                    GameLogOf20261027.Text(), "1.0.0.0", GameLogOf20261027.SessionStartUtc, GameLogOf20261027.CrashedAtUtc, PlayerScrubber());
                reporter.Enqueue(crash);

                // The next start, on PTU: everything waiting leaves.
                var sent = new List<JsonElement>();
                var next = new ErrorReporter(folder.Path, "1.0.0.0", GameLogOf20261027.CrashedAtUtc.AddMinutes(5), PlayerScrubber());
                var count = next.Pump(true, bytes =>
                {
                    sent.Add(JsonDocument.Parse(bytes).RootElement.Clone());
                    return true;
                }, GameLogOf20261027.CrashedAtUtc.AddMinutes(5), false);
                foreach (var report in sent)
                    m_output.WriteLine(report.GetRawText());

                Assert.Equal(3, count);
                var errorReports = sent.Where(r => r.GetProperty("genre").GetString() == "erreur").ToList();
                Assert.Equal(2, errorReports.Count);
                Assert.Contains(errorReports, r => r.GetProperty("message").GetString().Contains("planet shape cache of EarthLike not ready"));
                Assert.Contains(errorReports, r => r.GetProperty("message").GetString().Contains("terrain of EarthLike not read"));

                var crashReports = sent.Where(r => r.GetProperty("genre").GetString() == "plantage").ToList();
                Assert.Single(crashReports);
                Assert.Contains("DXGI_ERROR_DEVICE_REMOVED", crashReports[0].GetProperty("message").GetString());

                // Nothing waits any more.
                Assert.Empty(next.Queue.Waiting());
            }
        }

        [Fact]
        public void TheLogOfTheCrashedSessionIsTheNewestOneCreatedBeforeIt()
        {
            var start = new DateTime(2026, 10, 27, 19, 3, 12, DateTimeKind.Utc);
            var files = new List<LogFileInfo>
            {
                new LogFileInfo { Name = "older", CreatedUtc = start.AddDays(-2) },
                new LogFileInfo { Name = "crashed", CreatedUtc = start.AddSeconds(-4) },
                new LogFileInfo { Name = "running now", CreatedUtc = start.AddHours(1) },
            };
            Assert.Equal("crashed", GameLogFiles.Pick(files, start).Name);
            Assert.Null(GameLogFiles.Pick(new List<LogFileInfo>(), start));
        }

        [Fact]
        public void ALogWithoutAnExceptionStillMakesACrashReport()
        {
            var text = "2026-10-27 21:03:11.100 - Thread:   1 ->  Version: SpaceEngineers 01_208_015\n"
                + "2026-10-27 21:03:12.100 - Thread:   1 ->  [sir-holomap] loaded; camera hooked, 2 servers in history\n";
            var report = GameLog.BuildCrashReport(text, "1.0.0.0", GameLogOf20261027.SessionStartUtc, GameLogOf20261027.CrashedAtUtc, new ReportScrubber());
            Assert.Equal("plantage", report.Kind);
            Assert.Equal("The game stopped without a clean exit", report.Message);
            Assert.Contains(report.Lines, l => l.Contains("[sir-holomap] loaded"));
        }
    }
}
