using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SirHolomap
{
    // La règle des greffons côté joueur pour rapporter les erreurs : un document JSON,
    // en UTF-8, sur un seul canal. Rien sur le joueur n'y entre.
    public static class ReportRule
    {
        public const string PluginName = "Sir Holomap";

        // Ce que le greffon écrit devant chaque ligne qu'il ajoute au
        // journal du jeu.
        public const string LogTag = "[sir-holomap]";

        // Le canal de la règle. L'API multijoueur du jeu adresse un message sur
        // 16 bits seulement : le canal d'envoi est donc les 16 bits de poids
        // faible de ce nombre.
        public const long Channel = 5465479001;
        public const ushort ModApiChannel = unchecked((ushort)Channel);

        public const string KindError = "erreur";
        public const string KindCrash = "plantage";

        // Combien de lignes du journal du jeu un rapport de plantage porte de chaque
        // sorte.
        public const int TailLines = 20;

        public const int MaxMessage = 1000;
        public const int MaxStack = 8000;
        public const int MaxLines = 120;
        public const int MaxLine = 600;
    }

    // Un rapport : ce qui part vers le serveur.
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
        // Les noms de la règle, dans l'ordre où ils s'écrivent.
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

        // UTF-8 sans marque d'ordre des octets : ce qui part sur le canal.
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
