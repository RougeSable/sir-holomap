using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SirHolomap
{
    // The rule of the client-side plugins for reporting errors: one JSON
    // document, UTF-8, on one channel. Nothing about the player goes in it.
    public static class ReportRule
    {
        public const string PluginName = "Sir Holomap";

        // What the plugin writes in front of every line it adds to the
        // game's log.
        public const string LogTag = "[sir-holomap]";

        // The channel of the rule. The game's multiplayer API addresses a
        // message with 16 bits only, so the channel it is sent on is the low
        // 16 bits of this number.
        public const long Channel = 5465479001;
        public const ushort ModApiChannel = unchecked((ushort)Channel);

        public const string KindError = "erreur";
        public const string KindCrash = "plantage";

        // How many lines of the game's log a crash report carries of each
        // sort.
        public const int TailLines = 20;

        public const int MaxMessage = 1000;
        public const int MaxStack = 8000;
        public const int MaxLines = 120;
        public const int MaxLine = 600;
    }

    // One report: what goes to the server.
    public sealed class ErrorReport
    {
        public string Plugin = ReportRule.PluginName;
        public string Version = "";
        public string Kind = ReportRule.KindError;
        public string Message = "";
        public string Stack = "";
        public DateTime FirstUtc;
        public DateTime LastUtc;
        public int Count = 1;
        public DateTime SessionUtc;
        public readonly List<string> Lines = new List<string>();
    }

    public static class ErrorReportJson
    {
        // The names of the rule, in the order they are written.
        public static readonly string[] FieldNames =
        {
            "greffon", "version", "genre", "message", "pile", "premiere", "derniere", "nombre", "session", "lignes",
        };

        public static string Time(DateTime when)
        {
            var utc = when.Kind == DateTimeKind.Local ? when.ToUniversalTime() : DateTime.SpecifyKind(when, DateTimeKind.Utc);
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        public static string ToJson(ErrorReport report)
        {
            var text = new StringBuilder();
            text.Append('{');
            Field(text, "greffon", report.Plugin);
            text.Append(',');
            Field(text, "version", report.Version);
            text.Append(',');
            Field(text, "genre", report.Kind);
            text.Append(',');
            Field(text, "message", Cut(report.Message, ReportRule.MaxMessage));
            text.Append(',');
            Field(text, "pile", Cut(report.Stack, ReportRule.MaxStack));
            text.Append(',');
            Field(text, "premiere", Time(report.FirstUtc));
            text.Append(',');
            Field(text, "derniere", Time(report.LastUtc));
            text.Append(',');
            text.Append("\"nombre\":").Append(report.Count.ToString(CultureInfo.InvariantCulture));
            text.Append(',');
            Field(text, "session", Time(report.SessionUtc));
            text.Append(",\"lignes\":[");
            var first = true;
            var written = 0;
            foreach (var line in report.Lines)
            {
                if (written++ >= ReportRule.MaxLines)
                    break;
                if (!first)
                    text.Append(',');
                first = false;
                Quote(text, Cut(line ?? "", ReportRule.MaxLine));
            }
            text.Append("]}");
            return text.ToString();
        }

        // UTF-8 without a byte order mark: what goes on the channel.
        public static byte[] ToBytes(ErrorReport report)
        {
            return new UTF8Encoding(false).GetBytes(ToJson(report));
        }

        private static string Cut(string text, int maximum)
        {
            text = text ?? "";
            return text.Length <= maximum ? text : text.Substring(0, maximum);
        }

        private static void Field(StringBuilder text, string name, string value)
        {
            text.Append('"').Append(name).Append("\":");
            Quote(text, value ?? "");
        }

        private static void Quote(StringBuilder text, string value)
        {
            text.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (c < ' ' || (int)c == 0x2028 || (int)c == 0x2029)
                            text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            text.Append(c);
                        break;
                }
            }
            text.Append('"');
        }
    }
}
