using System;
using System.IO;

namespace SirHolomap
{
    // The mean colour of a texture file (DDS), as it looks from very far
    // away: the colour a terrain takes on a small globe. Only one small
    // level of the mipmap chain is read, never the whole picture.
    //
    // Formats read: 32-bit uncompressed (RGBA, BGRA, BGRX), BC1, BC2, BC3
    // and BC7. For BC7 blocks split in several subsets, the mean of the
    // block is estimated from the middle of each subset's endpoints: a mean
    // colour does not need the exact shape of the subsets.
    public static class TextureColor
    {
        // The level read is the first one no wider than this.
        public const int TargetSize = 64;

        private const uint Magic = 0x20534444; // "DDS "
        private const uint FourCcDxt1 = 0x31545844;
        private const uint FourCcDxt2 = 0x32545844;
        private const uint FourCcDxt3 = 0x33545844;
        private const uint FourCcDxt4 = 0x34545844;
        private const uint FourCcDxt5 = 0x35545844;
        private const uint FourCcDx10 = 0x30315844;
        private const uint FlagFourCc = 0x4;
        private const uint FlagRgb = 0x40;

        private enum Format
        {
            Unknown,
            Rgba,
            Bgra,
            Bc1,
            Bc2,
            Bc3,
            Bc7,
        }

        // Mean colour, sRGB, 0..1 per channel. False when the file is not a
        // texture this reader knows.
        public static bool TryMean(Stream stream, out float r, out float g, out float b)
        {
            r = g = b = 0;
            try
            {
                return Read(stream, out r, out g, out b);
            }
            catch (Exception)
            {
                r = g = b = 0;
                return false;
            }
        }

        private static bool Read(Stream stream, out float r, out float g, out float b)
        {
            r = g = b = 0;
            var header = new byte[128];
            if (!ReadExactly(stream, header, 128))
                return false;
            if (U32(header, 0) != Magic || U32(header, 4) != 124)
                return false;
            var height = (int)U32(header, 12);
            var width = (int)U32(header, 16);
            var mips = (int)Math.Max(1, U32(header, 28));
            var pixelFlags = U32(header, 80);
            var fourCc = U32(header, 84);
            var bitCount = U32(header, 88);
            var redMask = U32(header, 92);
            var greenMask = U32(header, 96);
            var blueMask = U32(header, 100);
            if (width <= 0 || height <= 0 || width > 65536 || height > 65536)
                return false;
            long dataStart = 128;

            var format = Format.Unknown;
            if ((pixelFlags & FlagFourCc) != 0)
            {
                if (fourCc == FourCcDxt1)
                    format = Format.Bc1;
                else if (fourCc == FourCcDxt2 || fourCc == FourCcDxt3)
                    format = Format.Bc2;
                else if (fourCc == FourCcDxt4 || fourCc == FourCcDxt5)
                    format = Format.Bc3;
                else if (fourCc == FourCcDx10)
                {
                    var extended = new byte[20];
                    if (!ReadExactly(stream, extended, 20))
                        return false;
                    dataStart += 20;
                    format = FromDxgi(U32(extended, 0));
                }
            }
            else if ((pixelFlags & FlagRgb) != 0 && bitCount == 32)
            {
                if (redMask == 0x000000ff && greenMask == 0x0000ff00 && blueMask == 0x00ff0000)
                    format = Format.Rgba;
                else if (redMask == 0x00ff0000 && greenMask == 0x0000ff00 && blueMask == 0x000000ff)
                    format = Format.Bgra;
            }
            if (format == Format.Unknown)
                return false;

            // The first level no wider than the target, or the last one.
            long offset = dataStart;
            int w = width, h = height;
            for (var level = 0; level < mips - 1 && Math.Max(w, h) > TargetSize; level++)
            {
                offset += LevelBytes(format, w, h);
                w = Math.Max(1, w / 2);
                h = Math.Max(1, h / 2);
            }

            var size = LevelBytes(format, w, h);
            var data = new byte[size];
            if (stream.CanSeek)
            {
                stream.Seek(offset, SeekOrigin.Begin);
            }
            else
            {
                var skip = offset - dataStart;
                var buffer = new byte[4096];
                while (skip > 0)
                {
                    var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, skip));
                    if (read <= 0)
                        return false;
                    skip -= read;
                }
            }
            if (!ReadExactly(stream, data, size))
                return false;

            var sum = new double[4];
            switch (format)
            {
                case Format.Rgba:
                case Format.Bgra:
                    for (var i = 0; i + 3 < data.Length; i += 4)
                    {
                        var red = format == Format.Rgba ? data[i] : data[i + 2];
                        var blue = format == Format.Rgba ? data[i + 2] : data[i];
                        Add(sum, red, data[i + 1], blue, 1);
                    }
                    break;
                case Format.Bc1:
                    for (var i = 0; i + 8 <= data.Length; i += 8)
                        ColourBlock(data, i, true, sum);
                    break;
                case Format.Bc2:
                case Format.Bc3:
                    for (var i = 0; i + 16 <= data.Length; i += 16)
                        ColourBlock(data, i + 8, false, sum);
                    break;
                case Format.Bc7:
                    for (var i = 0; i + 16 <= data.Length; i += 16)
                        Bc7Block(data, i, sum);
                    break;
            }
            if (sum[3] <= 0)
                return false;
            r = (float)(sum[0] / sum[3] / 255.0);
            g = (float)(sum[1] / sum[3] / 255.0);
            b = (float)(sum[2] / sum[3] / 255.0);
            return true;
        }

        private static Format FromDxgi(uint dxgi)
        {
            switch (dxgi)
            {
                case 27: // R8G8B8A8_TYPELESS
                case 28: // R8G8B8A8_UNORM
                case 29: // R8G8B8A8_UNORM_SRGB
                    return Format.Rgba;
                case 87: // B8G8R8A8_UNORM
                case 88: // B8G8R8X8_UNORM
                case 90: // B8G8R8A8_TYPELESS
                case 91: // B8G8R8A8_UNORM_SRGB
                case 92: // B8G8R8X8_TYPELESS
                case 93: // B8G8R8X8_UNORM_SRGB
                    return Format.Bgra;
                case 70: // BC1_TYPELESS
                case 71: // BC1_UNORM
                case 72: // BC1_UNORM_SRGB
                    return Format.Bc1;
                case 73: // BC2_TYPELESS
                case 74: // BC2_UNORM
                case 75: // BC2_UNORM_SRGB
                    return Format.Bc2;
                case 76: // BC3_TYPELESS
                case 77: // BC3_UNORM
                case 78: // BC3_UNORM_SRGB
                    return Format.Bc3;
                case 97: // BC7_TYPELESS
                case 98: // BC7_UNORM
                case 99: // BC7_UNORM_SRGB
                    return Format.Bc7;
                default:
                    return Format.Unknown;
            }
        }

        private static int LevelBytes(Format format, int w, int h)
        {
            var blocks = Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4);
            switch (format)
            {
                case Format.Bc1:
                    return blocks * 8;
                case Format.Bc2:
                case Format.Bc3:
                case Format.Bc7:
                    return blocks * 16;
                default:
                    return w * h * 4;
            }
        }

        private static void Add(double[] sum, double r, double g, double b, double weight)
        {
            sum[0] += r * weight;
            sum[1] += g * weight;
            sum[2] += b * weight;
            sum[3] += weight;
        }

        // The colour part of BC1, BC2 and BC3: two 5:6:5 colours and a 2-bit
        // index per pixel. Only BC1 knows the three-colour mode, whose
        // fourth index is a transparent pixel, left out of the mean.
        private static void ColourBlock(byte[] data, int at, bool canBeTransparent, double[] sum)
        {
            var c0 = data[at] | (data[at + 1] << 8);
            var c1 = data[at + 2] | (data[at + 3] << 8);
            var palette = new double[4, 3];
            Expand565(c0, palette, 0);
            Expand565(c1, palette, 1);
            var transparent = false;
            for (var c = 0; c < 3; c++)
            {
                if (c0 > c1 || !canBeTransparent)
                {
                    palette[2, c] = (2 * palette[0, c] + palette[1, c]) / 3;
                    palette[3, c] = (palette[0, c] + 2 * palette[1, c]) / 3;
                }
                else
                {
                    palette[2, c] = (palette[0, c] + palette[1, c]) / 2;
                    palette[3, c] = 0;
                    transparent = true;
                }
            }
            for (var i = 0; i < 4; i++)
            {
                var row = data[at + 4 + i];
                for (var p = 0; p < 4; p++)
                {
                    var index = (row >> (2 * p)) & 3;
                    if (transparent && index == 3)
                        continue;
                    Add(sum, palette[index, 0], palette[index, 1], palette[index, 2], 1);
                }
            }
        }

        private static void Expand565(int colour, double[,] palette, int slot)
        {
            var r = (colour >> 11) & 31;
            var g = (colour >> 5) & 63;
            var b = colour & 31;
            palette[slot, 0] = (r << 3) | (r >> 2);
            palette[slot, 1] = (g << 2) | (g >> 4);
            palette[slot, 2] = (b << 3) | (b >> 2);
        }

        // BC7, mode by mode (the format's own table):
        // subsets, partition bits, rotation bits, index selection bit,
        // colour bits, alpha bits, endpoint p-bits, shared p-bits,
        // index bits, second index bits.
        private static readonly int[,] Bc7Modes =
        {
            { 3, 4, 0, 0, 4, 0, 1, 0, 3, 0 },
            { 2, 6, 0, 0, 6, 0, 0, 1, 3, 0 },
            { 3, 6, 0, 0, 5, 0, 0, 0, 2, 0 },
            { 2, 6, 0, 0, 7, 0, 1, 0, 2, 0 },
            { 1, 0, 2, 1, 5, 6, 0, 0, 2, 3 },
            { 1, 0, 2, 0, 7, 8, 0, 0, 2, 2 },
            { 1, 0, 0, 0, 7, 7, 1, 0, 4, 0 },
            { 2, 6, 0, 0, 5, 5, 1, 0, 2, 0 },
        };

        private static readonly int[] Weights2 = { 0, 21, 43, 64 };
        private static readonly int[] Weights3 = { 0, 9, 18, 27, 37, 46, 55, 64 };
        private static readonly int[] Weights4 = { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };

        private sealed class Bits
        {
            private readonly byte[] m_data;
            private readonly int m_start;
            private int m_position;

            public Bits(byte[] data, int start)
            {
                m_data = data;
                m_start = start;
            }

            public int Take(int count)
            {
                var value = 0;
                for (var i = 0; i < count; i++)
                {
                    var bit = m_position + i;
                    if ((m_data[m_start + bit / 8] >> (bit % 8) & 1) != 0)
                        value |= 1 << i;
                }
                m_position += count;
                return value;
            }
        }

        private static void Bc7Block(byte[] data, int at, double[] sum)
        {
            var first = data[at];
            if (first == 0)
                return; // Reserved mode: no colour.
            var mode = 0;
            while ((first & (1 << mode)) == 0)
                mode++;

            var bits = new Bits(data, at);
            bits.Take(mode + 1);
            var subsets = Bc7Modes[mode, 0];
            bits.Take(Bc7Modes[mode, 1]);
            var rotation = bits.Take(Bc7Modes[mode, 2]);
            var indexSelection = bits.Take(Bc7Modes[mode, 3]);
            var colourBits = Bc7Modes[mode, 4];
            var alphaBits = Bc7Modes[mode, 5];
            var endpointPBits = Bc7Modes[mode, 6];
            var sharedPBits = Bc7Modes[mode, 7];
            var indexBits = Bc7Modes[mode, 8];
            var secondIndexBits = Bc7Modes[mode, 9];

            var endpoints = subsets * 2;
            var raw = new int[endpoints, 4];
            for (var c = 0; c < 3; c++)
            {
                for (var e = 0; e < endpoints; e++)
                    raw[e, c] = bits.Take(colourBits);
            }
            for (var e = 0; e < endpoints; e++)
                raw[e, 3] = alphaBits > 0 ? bits.Take(alphaBits) : 255;

            var pBits = new int[endpoints];
            if (endpointPBits > 0)
            {
                for (var e = 0; e < endpoints; e++)
                    pBits[e] = bits.Take(1);
            }
            else if (sharedPBits > 0)
            {
                for (var s = 0; s < subsets; s++)
                {
                    var p = bits.Take(1);
                    pBits[s * 2] = p;
                    pBits[s * 2 + 1] = p;
                }
            }
            var hasP = endpointPBits > 0 || sharedPBits > 0;

            var colours = new int[endpoints, 4];
            for (var e = 0; e < endpoints; e++)
            {
                for (var c = 0; c < 3; c++)
                    colours[e, c] = Unquantize(raw[e, c], colourBits, hasP, pBits[e]);
                colours[e, 3] = alphaBits > 0 ? Unquantize(raw[e, 3], alphaBits, hasP, pBits[e]) : 255;
            }

            if (subsets > 1)
            {
                // The middle of each subset, every subset counted alike.
                for (var s = 0; s < subsets; s++)
                {
                    Add(sum, (colours[s * 2, 0] + colours[s * 2 + 1, 0]) / 2.0,
                        (colours[s * 2, 1] + colours[s * 2 + 1, 1]) / 2.0,
                        (colours[s * 2, 2] + colours[s * 2 + 1, 2]) / 2.0, 16.0 / subsets);
                }
                return;
            }

            // One subset: every pixel from its own index. The first pixel's
            // index has one bit less (its top bit is always zero).
            var primary = new int[16];
            for (var i = 0; i < 16; i++)
                primary[i] = bits.Take(i == 0 ? indexBits - 1 : indexBits);
            int[] secondary = null;
            if (secondIndexBits > 0)
            {
                secondary = new int[16];
                for (var i = 0; i < 16; i++)
                    secondary[i] = bits.Take(i == 0 ? secondIndexBits - 1 : secondIndexBits);
            }

            for (var i = 0; i < 16; i++)
            {
                int colourIndex = primary[i], colourIndexBits = indexBits;
                int alphaIndex = primary[i], alphaIndexBits = indexBits;
                if (secondary != null)
                {
                    if (indexSelection == 0)
                    {
                        alphaIndex = secondary[i];
                        alphaIndexBits = secondIndexBits;
                    }
                    else
                    {
                        colourIndex = secondary[i];
                        colourIndexBits = secondIndexBits;
                    }
                }
                var pixel = new int[4];
                for (var c = 0; c < 3; c++)
                    pixel[c] = Interpolate(colours[0, c], colours[1, c], colourIndex, colourIndexBits);
                pixel[3] = Interpolate(colours[0, 3], colours[1, 3], alphaIndex, alphaIndexBits);
                if (rotation > 0)
                {
                    var swap = pixel[rotation - 1];
                    pixel[rotation - 1] = pixel[3];
                    pixel[3] = swap;
                }
                Add(sum, pixel[0], pixel[1], pixel[2], 1);
            }
        }

        private static int Unquantize(int value, int bits, bool hasP, int p)
        {
            if (hasP)
            {
                value = (value << 1) | p;
                bits++;
            }
            if (bits >= 8)
                return value & 255;
            value <<= 8 - bits;
            return value | (value >> bits);
        }

        private static int Interpolate(int e0, int e1, int index, int bits)
        {
            var weights = bits == 2 ? Weights2 : bits == 3 ? Weights3 : Weights4;
            var w = weights[index];
            return ((64 - w) * e0 + w * e1 + 32) >> 6;
        }

        private static uint U32(byte[] data, int at)
        {
            return (uint)(data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24));
        }

        private static bool ReadExactly(Stream stream, byte[] buffer, int count)
        {
            var done = 0;
            while (done < count)
            {
                var read = stream.Read(buffer, done, count - done);
                if (read <= 0)
                    return false;
                done += read;
            }
            return true;
        }
    }
}
