using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SirHolomap.Tests
{
    // The colour of a terrain on the small globes of the system view comes,
    // when the game gives no far colour, from the mean of the terrain's own
    // texture file: one small level of its mipmap chain, in the formats the
    // game and the mods ship.
    public class TextureColorTests
    {
        private const float Tolerance = 1.5f / 255f;

        // A DDS file: its header, then the given levels one after the other.
        private static byte[] Dds(int width, int height, int mips, uint fourCc, uint dxgi, bool bgra, params byte[][] levels)
        {
            var header = new byte[128];
            Put(header, 0, 0x20534444);
            Put(header, 4, 124);
            Put(header, 8, 0x1 | 0x2 | 0x4 | 0x1000 | 0x20000);
            Put(header, 12, (uint)height);
            Put(header, 16, (uint)width);
            Put(header, 28, (uint)mips);
            Put(header, 76, 32);
            if (fourCc != 0)
            {
                Put(header, 80, 0x4);
                Put(header, 84, fourCc);
            }
            else
            {
                Put(header, 80, 0x40 | 0x1);
                Put(header, 88, 32);
                Put(header, 92, bgra ? 0x00ff0000u : 0x000000ffu);
                Put(header, 96, 0x0000ff00u);
                Put(header, 100, bgra ? 0x000000ffu : 0x00ff0000u);
                Put(header, 104, 0xff000000u);
            }
            Put(header, 108, 0x1000);

            var bytes = new List<byte>(header);
            if (fourCc == 0x30315844)
            {
                var extended = new byte[20];
                Put(extended, 0, dxgi);
                Put(extended, 4, 3);
                Put(extended, 12, 1);
                bytes.AddRange(extended);
            }
            foreach (var level in levels)
                bytes.AddRange(level);
            return bytes.ToArray();
        }

        private static void Put(byte[] data, int at, uint value)
        {
            data[at] = (byte)value;
            data[at + 1] = (byte)(value >> 8);
            data[at + 2] = (byte)(value >> 16);
            data[at + 3] = (byte)(value >> 24);
        }

        private static byte[] Filled(int width, int height, byte c0, byte c1, byte c2)
        {
            var data = new byte[width * height * 4];
            for (var i = 0; i < data.Length; i += 4)
            {
                data[i] = c0;
                data[i + 1] = c1;
                data[i + 2] = c2;
                data[i + 3] = 255;
            }
            return data;
        }

        private static byte[] Repeat(byte[] block, int count)
        {
            var data = new byte[block.Length * count];
            for (var i = 0; i < count; i++)
                Array.Copy(block, 0, data, i * block.Length, block.Length);
            return data;
        }

        // Bits written low first, as BC7 reads them.
        private sealed class BitWriter
        {
            private readonly byte[] m_block = new byte[16];
            private int m_position;

            public BitWriter Put(int value, int count)
            {
                for (var i = 0; i < count; i++)
                {
                    if (((value >> i) & 1) != 0)
                        m_block[(m_position + i) / 8] |= (byte)(1 << ((m_position + i) % 8));
                }
                m_position += count;
                return this;
            }

            public byte[] Block()
            {
                Assert.Equal(128, m_position);
                return m_block;
            }
        }

        private static void AssertColor(byte[] file, double r, double g, double b)
        {
            float mr, mg, mb;
            Assert.True(TextureColor.TryMean(new MemoryStream(file), out mr, out mg, out mb));
            Assert.InRange(mr, r / 255 - Tolerance, r / 255 + Tolerance);
            Assert.InRange(mg, g / 255 - Tolerance, g / 255 + Tolerance);
            Assert.InRange(mb, b / 255 - Tolerance, b / 255 + Tolerance);
        }

        [Fact]
        public void UncompressedTextureGivesItsColour()
        {
            AssertColor(Dds(8, 8, 1, 0, 0, false, Filled(8, 8, 200, 100, 50)), 200, 100, 50);
            // Blue first in the file: still read as red, green, blue.
            AssertColor(Dds(8, 8, 1, 0, 0, true, Filled(8, 8, 50, 100, 200)), 200, 100, 50);
            // The same, announced by the extended header.
            AssertColor(Dds(8, 8, 1, 0x30315844, 29, false, Filled(8, 8, 10, 220, 30)), 10, 220, 30);
        }

        [Fact]
        public void ReadsTheSmallLevelOfTheMipmapChain()
        {
            // 128 wide, then 64: the second level is the one read, the large
            // one is skipped, whether the stream can seek or not.
            var file = Dds(128, 128, 2, 0, 0, false, Filled(128, 128, 255, 0, 0), Filled(64, 64, 0, 255, 0));
            AssertColor(file, 0, 255, 0);
            float r, g, b;
            Assert.True(TextureColor.TryMean(new ForwardOnly(file), out r, out g, out b));
            Assert.InRange(g, 1 - Tolerance, 1f);
            Assert.InRange(r, 0f, Tolerance);
        }

        [Fact]
        public void Bc1BlocksGiveTheirColour()
        {
            // Pure red against pure blue, every pixel on the red one.
            var red = new byte[] { 0x00, 0xF8, 0x1F, 0x00, 0, 0, 0, 0 };
            AssertColor(Dds(8, 8, 1, 0x31545844, 0, false, Repeat(red, 4)), 255, 0, 0);

            // Three-colour mode: half the pixels transparent, left out; the
            // other half on the blue one.
            var holes = new byte[] { 0x1F, 0x00, 0x00, 0xF8, 0xCC, 0xCC, 0xCC, 0xCC };
            AssertColor(Dds(4, 4, 1, 0x31545844, 0, false, holes), 0, 0, 255);
        }

        [Fact]
        public void Bc3BlocksGiveTheColourOfTheirColourPart()
        {
            var block = new byte[16];
            block[0] = 255;
            block[1] = 255;
            // Pure green (0x07E0) on every pixel.
            block[8] = 0xE0;
            block[9] = 0x07;
            AssertColor(Dds(4, 4, 1, 0x35545844, 0, false, block), 0, 255, 0);
        }

        [Fact]
        public void Bc7SingleSubsetBlocksAreDecodedPixelByPixel()
        {
            // Mode 6: both endpoints alike, every pixel the same colour.
            var flat = new BitWriter().Put(1 << 6, 7)
                .Put(100, 7).Put(100, 7).Put(50, 7).Put(50, 7).Put(10, 7).Put(10, 7).Put(127, 7).Put(127, 7)
                .Put(1, 1).Put(1, 1)
                .Put(0, 3).Put(0, 60).Block();
            AssertColor(Dds(4, 4, 1, 0x30315844, 99, false, flat), 201, 101, 21);

            // Mode 6, black to white: the first pixel black, fifteen white.
            var ramp = new BitWriter().Put(1 << 6, 7)
                .Put(0, 7).Put(127, 7).Put(0, 7).Put(127, 7).Put(0, 7).Put(127, 7).Put(127, 7).Put(127, 7)
                .Put(0, 1).Put(1, 1)
                .Put(0, 3);
            for (var i = 1; i < 16; i++)
                ramp.Put(15, 4);
            var white = 255.0 * 15 / 16;
            AssertColor(Dds(4, 4, 1, 0x30315844, 98, false, ramp.Block()), white, white, white);

            // Mode 5 with the red and alpha channels swapped: the colour
            // comes out of the alpha endpoints.
            var rotated = new BitWriter().Put(1 << 5, 6).Put(1, 2)
                .Put(0, 7).Put(0, 7).Put(64, 7).Put(64, 7).Put(32, 7).Put(32, 7)
                .Put(200, 8).Put(200, 8)
                .Put(0, 31).Put(0, 31).Block();
            AssertColor(Dds(4, 4, 1, 0x30315844, 98, false, rotated), 200, 129, 64);
        }

        [Fact]
        public void Bc7SplitBlocksAreEstimatedFromTheirSubsets()
        {
            // Mode 1: one subset white, the other black; the mean is grey
            // whatever the shape of the subsets.
            var split = new BitWriter().Put(1 << 1, 2).Put(13, 6);
            foreach (var channel in new[] { 0, 1, 2 })
                split.Put(63, 6).Put(63, 6).Put(0, 6).Put(0, 6);
            split.Put(1, 1).Put(0, 1).Put(0, 46);
            AssertColor(Dds(4, 4, 1, 0x30315844, 98, false, split.Block()), 127.5, 127.5, 127.5);
        }

        [Fact]
        public void UnknownOrBrokenFilesAreRefused()
        {
            float r, g, b;
            Assert.False(TextureColor.TryMean(new MemoryStream(new byte[] { 1, 2, 3 }), out r, out g, out b));
            Assert.False(TextureColor.TryMean(new MemoryStream(new byte[300]), out r, out g, out b));

            // A format it does not read (BC5, through the extended header).
            Assert.False(TextureColor.TryMean(new MemoryStream(Dds(4, 4, 1, 0x30315844, 83, false, new byte[16])), out r, out g, out b));

            // Cut short: its level is missing.
            var cut = Dds(8, 8, 1, 0, 0, false, Filled(8, 8, 1, 2, 3));
            Array.Resize(ref cut, cut.Length - 10);
            Assert.False(TextureColor.TryMean(new MemoryStream(cut), out r, out g, out b));
        }

        // A stream read from start to end only, like a file inside a zip.
        private sealed class ForwardOnly : Stream
        {
            private readonly MemoryStream m_inner;

            public ForwardOnly(byte[] data)
            {
                m_inner = new MemoryStream(data);
            }

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return m_inner.Read(buffer, offset, count);
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
