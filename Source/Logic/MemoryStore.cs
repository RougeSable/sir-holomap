using System;
using System.IO;
using System.Text;

namespace SirHolomap
{
    // Where the memories live: one file per server, in the player's own
    // folder. A server never sees them, and one server's memory never shows
    // on another.
    public sealed class MemoryStore
    {
        public const string Extension = ".map";

        private readonly string m_directory;

        public MemoryStore(string directory)
        {
            m_directory = directory;
        }

        public string Directory
        {
            get { return m_directory; }
        }

        public string PathFor(string serverKey)
        {
            return Path.Combine(m_directory, FileNameFor(serverKey) + Extension);
        }

        // Readable, safe on every file system, and never shared by two keys:
        // the cleaned name carries a hash of the exact key.
        public static string FileNameFor(string serverKey)
        {
            var key = serverKey ?? "";
            var clean = new StringBuilder();
            foreach (var c in key)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-')
                    clean.Append(c);
                else
                    clean.Append('_');
                if (clean.Length >= 60)
                    break;
            }
            return clean + "-" + Hashing.Fnv1a64(key).ToString("x16");
        }

        public MapMemory Load(string serverKey)
        {
            var path = PathFor(serverKey);
            try
            {
                if (!File.Exists(path))
                    return new MapMemory(serverKey);
                return MapMemory.Load(serverKey, File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception)
            {
                return new MapMemory(serverKey);
            }
        }

        // Written next to the file, then swapped in: a crash never leaves a
        // half-written memory.
        public void Save(MapMemory memory)
        {
            System.IO.Directory.CreateDirectory(m_directory);
            var path = PathFor(memory.ServerKey);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, memory.Save(), new UTF8Encoding(false));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
            memory.Dirty = false;
        }
    }

    public static class Hashing
    {
        public static ulong Fnv1a64(string text)
        {
            var hash = 14695981039346656037UL;
            if (text == null)
                return hash;
            foreach (var c in text)
            {
                hash ^= (byte)(c & 0xFF);
                hash *= 1099511628211UL;
                hash ^= (byte)(c >> 8);
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
