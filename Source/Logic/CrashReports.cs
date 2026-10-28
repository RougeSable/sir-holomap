using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SirHolomap
{
    // A game log file on disk.
    public sealed class LogFileInfo
    {
        public string Path = "";
        public string Name = "";
        public DateTime CreatedUtc;
        public DateTime LastWriteUtc;
    }

    // Finding the log of the session that crashed, and reading its end.
    public static class GameLogFiles
    {
        public const string Pattern = "SpaceEngineers_*.log";

        // How much of the end of a log is read: a crash is at the end.
        public const int TailBytes = 2 * 1024 * 1024;

        // The log of the session that began at this time: the newest one
        // created before the plugin of that session started. The log of the
        // session running now was created later.
        public static LogFileInfo Pick(IEnumerable<LogFileInfo> files, DateTime sessionStartUtc)
        {
            LogFileInfo best = null;
            foreach (var file in files)
            {
                if (file.CreatedUtc > sessionStartUtc)
                    continue;
                if (best == null || file.CreatedUtc > best.CreatedUtc)
                    best = file;
            }
            return best;
        }

        public static List<LogFileInfo> List(string directory)
        {
            var found = new List<LogFileInfo>();
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return found;
            foreach (var path in Directory.GetFiles(directory, Pattern))
            {
                found.Add(new LogFileInfo
                {
                    Path = path,
                    Name = System.IO.Path.GetFileName(path),
                    CreatedUtc = File.GetCreationTimeUtc(path),
                    LastWriteUtc = File.GetLastWriteTimeUtc(path),
                });
            }
            return found;
        }

        // The end of a file, whole lines only. The game may still hold the
        // file open.
        public static string ReadTail(string path, int maximumBytes)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var start = Math.Max(0, stream.Length - maximumBytes);
                stream.Seek(start, SeekOrigin.Begin);
                var bytes = new byte[stream.Length - start];
                var read = 0;
                while (read < bytes.Length)
                {
                    var step = stream.Read(bytes, read, bytes.Length - read);
                    if (step <= 0)
                        break;
                    read += step;
                }
                var text = Encoding.UTF8.GetString(bytes, 0, read);
                if (start > 0)
                {
                    var newline = text.IndexOf('\n');
                    text = newline >= 0 ? text.Substring(newline + 1) : "";
                }
                return text;
            }
        }
    }

    // The game's log, read for what a crash report needs.
    public static class GameLog
    {
        // "2026-10-27 21:14:02.331 - Thread:   1 ->  text", with or without
        // the date.
        private static readonly Regex Header = new Regex(
            @"^(?:\d{4}-\d{2}-\d{2}\s+)?\d{1,2}:\d{2}:\d{2}(?:[.,]\d+)?\s+-\s+Thread:\s*\d+\s+->\s?(?<text>.*)$", RegexOptions.CultureInvariant);

        private static readonly Regex ErrorWords = new Regex(
            @"exception|\berrors?\b|\bfail(?:ed|ure|s)?\b|\bfatal\b|\bcrash(?:ed)?\b|DXGI_ERROR|device removed|access violation|stack ?trace",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ExceptionLine = new Regex(
            @"(?<![\w.])(?:[A-Za-z_]\w*\.)*[A-Za-z_]\w*(?:Exception|Error)\b\s*[:(]|DXGI_ERROR_\w+|\bHRESULT\b", RegexOptions.CultureInvariant);

        private static readonly Regex StackLine = new Regex(@"^\s*(?:at\s|---|in\s)", RegexOptions.CultureInvariant);

        private static readonly Regex CrashInfo = new Regex(@"CRASH\s+INFO", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private const int MaxBlock = 40;
        private const int MaxStackLines = 40;

        public static List<string> Split(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
                return lines;
            foreach (var line in text.Split('\n'))
                lines.Add(line.TrimEnd('\r'));
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        // The text of a line without the time and the thread the game puts
        // in front of it.
        public static string TextOf(string line)
        {
            var match = Header.Match(line ?? "");
            return match.Success ? match.Groups["text"].Value : (line ?? "");
        }

        public static bool IsHeader(string line)
        {
            return line != null && Header.IsMatch(line);
        }

        public static bool IsPluginLine(string line)
        {
            return line != null && line.IndexOf(ReportRule.LogTag, StringComparison.Ordinal) >= 0;
        }

        public static bool IsErrorLine(string line)
        {
            return IsHeader(line) && !IsPluginLine(line) && ErrorWords.IsMatch(TextOf(line));
        }

        // The report of a game that did not leave cleanly, from the log of
        // that session. Nothing about the player: the scrubber has the last
        // word on every line.
        public static ErrorReport BuildCrashReport(
            string logText,
            string version,
            DateTime sessionStartUtc,
            DateTime crashedAtUtc,
            ReportScrubber scrubber)
        {
            var lines = Split(logText);
            var report = new ErrorReport
            {
                Version = version,
                Kind = ReportRule.KindCrash,
                FirstUtc = crashedAtUtc,
                LastUtc = crashedAtUtc,
                Count = 1,
                SessionUtc = sessionStartUtc,
            };

            // The block the game writes when it goes down.
            var blockStart = -1;
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                if (CrashInfo.IsMatch(lines[i]))
                {
                    blockStart = i;
                    break;
                }
            }
            var block = new List<string>();
            if (blockStart >= 0)
            {
                block.Add(lines[blockStart]);
                for (var i = blockStart + 1; i < lines.Count && block.Count < MaxBlock; i++)
                {
                    if (IsHeader(lines[i]) || lines[i].Trim().Length == 0)
                        break;
                    block.Add(lines[i]);
                }
            }

            // The exception that brought it down, and its stack: in the
            // block, after it, or else the last one of the log.
            var exceptionAt = -1;
            for (var i = Math.Max(blockStart, 0); i < lines.Count && blockStart >= 0; i++)
            {
                if (ExceptionLine.IsMatch(lines[i]))
                {
                    exceptionAt = i;
                    break;
                }
            }
            if (exceptionAt < 0)
            {
                for (var i = lines.Count - 1; i >= 0; i--)
                {
                    if (!IsPluginLine(lines[i]) && ExceptionLine.IsMatch(lines[i]))
                    {
                        exceptionAt = i;
                        break;
                    }
                }
            }
            var exception = new List<string>();
            if (exceptionAt >= 0)
            {
                exception.Add(lines[exceptionAt]);
                for (var i = exceptionAt + 1; i < lines.Count && exception.Count <= MaxStackLines; i++)
                {
                    if (!StackLine.IsMatch(lines[i]))
                        break;
                    exception.Add(lines[i]);
                }
            }

            var gameErrors = Tail(lines, IsErrorLine, ReportRule.TailLines);
            var pluginLines = Tail(lines, IsPluginLine, ReportRule.TailLines);

            // What the report says about it.
            var firstLine = exception.Count > 0 ? scrubber.Clean(TextOf(exception[0])) : null;
            var stackLines = scrubber.CleanLines(exception.GetRange(Math.Min(1, exception.Count), Math.Max(0, exception.Count - 1)));
            report.Message = firstLine != null
                ? firstLine.Trim()
                : (exception.Count > 0 ? ReportScrubber.Removed : "The game stopped without a clean exit");
            report.Stack = string.Join("\n", stackLines.ToArray());

            report.Lines.Add("-- CRASH INFO --");
            report.Lines.AddRange(scrubber.CleanLines(block));
            report.Lines.Add("-- EXCEPTION --");
            if (firstLine != null)
                report.Lines.Add(firstLine);
            report.Lines.AddRange(stackLines);
            report.Lines.Add("-- LAST " + ReportRule.TailLines + " GAME ERROR LINES --");
            report.Lines.AddRange(scrubber.CleanLines(gameErrors));
            report.Lines.Add("-- LAST " + ReportRule.TailLines + " PLUGIN LINES --");
            report.Lines.AddRange(scrubber.CleanLines(pluginLines));
            return report;
        }

        private static List<string> Tail(List<string> lines, Func<string, bool> wanted, int count)
        {
            var found = new List<string>();
            for (var i = lines.Count - 1; i >= 0 && found.Count < count; i--)
            {
                if (wanted(lines[i]))
                    found.Add(lines[i]);
            }
            found.Reverse();
            return found;
        }
    }
}
