using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace SirHolomap
{
    // How a crash is told at the next start: a mark is laid in the plugin's
    // folder when a session begins and taken away when it ends cleanly. A mark
    // found at the next start is a session that did not end.
    public static class CrashMarker
    {
        public const string FileName = "session.mark";

        // Lays the mark of this session. Returns when the session that left
        // its mark behind began, or null when the last session ended cleanly
        // (or there was none).
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

        // The session ended cleanly: no mark is left.
        public static void End(string folder)
        {
            var path = Path.Combine(folder, FileName);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
