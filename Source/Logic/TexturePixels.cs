using System;
using System.Collections.Generic;

namespace SirHolomap
{
    // How a picture of the map is handed to the game's renderer.
    //
    // The renderer (VRage.Render11, MyGeneratedTextureManager) reads exactly
    // what the size announces, straight from the bytes it was given, on its
    // own thread and later than the call: one level of width x height x 4
    // bytes, or, when the texture has mipmaps, every level one after the
    // other, each half the size of the one before, down to one pixel. Fewer
    // bytes than that and it reads past the end of the array, which is what
    // brought the game down. So every picture sent is a new array of exactly
    // the announced length, never touched again once sent.
    //
    // The game draws every sprite with premultiplied alpha
    // (MySpritesRenderer, premultipliedAlpha: true): a pixel that is
    // transparent must also be black, or the "transparent" corners of a disc
    // show as a coloured square. The map paints its pictures with straight
    // alpha; they are premultiplied here, in linear light, as the texture is
    // sRGB.
    public static class TexturePixels
    {
        public const int BytesPerPixel = 4;

        // Only square pictures whose side is a power of two get mipmaps: their
        // levels are exactly halves down to one pixel, the same sizes the
        // renderer walks through, so its reading never goes past the end.
        public static bool UsesMipmaps(int width, int height)
        {
            return width == height && width >= 2 && (width & (width - 1)) == 0;
        }

        public static int MipLevels(int width, int height)
        {
            if (!UsesMipmaps(width, height))
                return 1;
            var levels = 1;
            for (var size = width; size > 1; size >>= 1)
                levels++;
            return levels;
        }

        // The number of bytes the renderer reads for a texture of this size.
        public static int DeclaredLength(int width, int height)
        {
            CheckSize(width, height);
            var levels = MipLevels(width, height);
            long total = 0;
            int w = width, h = height;
            for (var level = 0; level < levels; level++)
            {
                total += (long)w * h * BytesPerPixel;
                w = Math.Max(1, w >> 1);
                h = Math.Max(1, h >> 1);
            }
            if (total > int.MaxValue)
                throw new ArgumentException("picture too large: " + width + " x " + height);
            return (int)total;
        }

        private static void CheckSize(int width, int height)
        {
            if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
                throw new ArgumentException("bad picture size: " + width + " x " + height);
        }

        // A new array, ready for the renderer: premultiplied, with every
        // mipmap level when the size has them, exactly DeclaredLength bytes.
        // The picture given is only read, never kept.
        public static byte[] Prepare(int width, int height, byte[] straight)
        {
            CheckSize(width, height);
            if (straight == null)
                throw new ArgumentNullException("straight");
            var level0 = width * height * BytesPerPixel;
            if (straight.Length != level0)
                throw new ArgumentException("picture of " + width + " x " + height + " needs " + level0 + " bytes, got " + straight.Length);

            var result = new byte[DeclaredLength(width, height)];
            var levels = MipLevels(width, height);
            if (levels == 1)
            {
                for (var i = 0; i < level0; i += 4)
                    Premultiply(straight, i, result, i);
                return result;
            }

            // Levels averaged in linear light, premultiplied: the edges of a
            // disc fade out as they should when it is drawn small.
            var linear = new float[level0];
            for (var i = 0; i < level0; i += 4)
            {
                var a = straight[i + 3] / 255f;
                linear[i] = ToLinear[straight[i]] * a;
                linear[i + 1] = ToLinear[straight[i + 1]] * a;
                linear[i + 2] = ToLinear[straight[i + 2]] * a;
                linear[i + 3] = a;
            }
            var offset = 0;
            int w = width, h = height;
            for (var level = 0; level < levels; level++)
            {
                if (level > 0)
                {
                    var nw = Math.Max(1, w >> 1);
                    var nh = Math.Max(1, h >> 1);
                    linear = HalfSize(linear, w, h, nw, nh);
                    w = nw;
                    h = nh;
                }
                var count = w * h * BytesPerPixel;
                for (var i = 0; i < count; i += 4)
                {
                    // Never brighter than the alpha once both are rounded: a
                    // pixel that ends up transparent is black.
                    var alpha = (byte)Math.Round(Clamp01(linear[i + 3]) * 255);
                    var most = alpha / 255f;
                    result[offset + i] = ToSrgbByte(Math.Min(linear[i], most));
                    result[offset + i + 1] = ToSrgbByte(Math.Min(linear[i + 1], most));
                    result[offset + i + 2] = ToSrgbByte(Math.Min(linear[i + 2], most));
                    result[offset + i + 3] = alpha;
                }
                offset += count;
            }
            return result;
        }

        private static float[] HalfSize(float[] source, int w, int h, int nw, int nh)
        {
            var result = new float[nw * nh * BytesPerPixel];
            for (var y = 0; y < nh; y++)
            {
                for (var x = 0; x < nw; x++)
                {
                    var x0 = Math.Min(w - 1, x * 2);
                    var x1 = Math.Min(w - 1, x * 2 + 1);
                    var y0 = Math.Min(h - 1, y * 2);
                    var y1 = Math.Min(h - 1, y * 2 + 1);
                    var o = (y * nw + x) * 4;
                    for (var c = 0; c < 4; c++)
                    {
                        result[o + c] = (source[(y0 * w + x0) * 4 + c] + source[(y0 * w + x1) * 4 + c]
                            + source[(y1 * w + x0) * 4 + c] + source[(y1 * w + x1) * 4 + c]) * 0.25f;
                    }
                }
            }
            return result;
        }

        private static void Premultiply(byte[] source, int i, byte[] target, int j)
        {
            var alpha = source[i + 3];
            if (alpha == 255)
            {
                target[j] = source[i];
                target[j + 1] = source[i + 1];
                target[j + 2] = source[i + 2];
                target[j + 3] = 255;
                return;
            }
            var a = alpha / 255f;
            target[j] = ToSrgbByte(ToLinear[source[i]] * a);
            target[j + 1] = ToSrgbByte(ToLinear[source[i + 1]] * a);
            target[j + 2] = ToSrgbByte(ToLinear[source[i + 2]] * a);
            target[j + 3] = alpha;
        }

        private static readonly float[] ToLinear = BuildToLinear();
        private const int SrgbSteps = 4096;
        private static readonly byte[] ToSrgb = BuildToSrgb();

        private static float[] BuildToLinear()
        {
            var table = new float[256];
            for (var i = 0; i < 256; i++)
            {
                var c = i / 255.0;
                table[i] = (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
            }
            return table;
        }

        private static byte[] BuildToSrgb()
        {
            var table = new byte[SrgbSteps + 1];
            for (var i = 0; i <= SrgbSteps; i++)
            {
                var l = (double)i / SrgbSteps;
                var c = l <= 0.0031308 ? l * 12.92 : 1.055 * Math.Pow(l, 1 / 2.4) - 0.055;
                table[i] = (byte)Math.Round(Clamp01((float)c) * 255);
            }
            return table;
        }

        private static byte ToSrgbByte(float linear)
        {
            return ToSrgb[(int)Math.Round(Clamp01(linear) * SrgbSteps)];
        }

        private static float Clamp01(float v)
        {
            return v < 0 ? 0 : (v > 1 ? 1 : v);
        }
    }

    // What to tell the renderer for one picture.
    public enum TextureAction
    {
        // A new name: create it.
        Create,
        // A name already created with this size: replace its pixels.
        Reset,
        // A name already created with another size: destroy it, create it
        // again. A creation under a name that exists is ignored by the
        // renderer, which would keep the old picture.
        Recreate,
    }

    public sealed class TextureUpload
    {
        public string Name;
        public int Width;
        public int Height;
        public bool Mipmaps;
        public TextureAction Action;
        // A new array, exactly TexturePixels.DeclaredLength bytes, owned by
        // the renderer from the moment it is sent.
        public byte[] Pixels;
    }

    // The textures the map has handed to the renderer, with their sizes, so
    // that every later picture under the same name is sent the right way.
    public sealed class TextureLedger
    {
        private struct Entry
        {
            public int Width;
            public int Height;
        }

        private readonly Dictionary<string, Entry> m_entries = new Dictionary<string, Entry>();

        public int Count
        {
            get { return m_entries.Count; }
        }

        public bool Contains(string name)
        {
            return m_entries.ContainsKey(name);
        }

        public IEnumerable<string> Names
        {
            get { return m_entries.Keys; }
        }

        public TextureUpload Plan(string name, int width, int height, byte[] straight)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("a texture needs a name");
            var pixels = TexturePixels.Prepare(width, height, straight);
            Entry entry;
            TextureAction action;
            if (!m_entries.TryGetValue(name, out entry))
                action = TextureAction.Create;
            else if (entry.Width == width && entry.Height == height)
                action = TextureAction.Reset;
            else
                action = TextureAction.Recreate;
            m_entries[name] = new Entry { Width = width, Height = height };
            return new TextureUpload
            {
                Name = name,
                Width = width,
                Height = height,
                Mipmaps = TexturePixels.UsesMipmaps(width, height),
                Action = action,
                Pixels = pixels,
            };
        }

        public void Clear()
        {
            m_entries.Clear();
        }
    }
}
