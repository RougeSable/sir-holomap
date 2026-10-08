using System;
using Xunit;

namespace SirHolomap.Tests
{
    // The pictures handed to the game's renderer: the renderer reads exactly
    // what the size announces (every mipmap level included), from the array
    // it was given, later and on its own thread.
    public class GlobeTextureTests
    {
        // What the renderer reads (MyGeneratedTextureManager.Reset): from the
        // start of the array, level after level, width x height x 4 bytes,
        // each level half the one before, as many levels as it gives the
        // texture.
        private static int RendererReads(int width, int height, bool mipmaps)
        {
            var levels = 1;
            if (mipmaps)
            {
                // MyResourceUtils.GetMipLevels(max(width, height)).
                var size = Math.Max(width, height);
                var steps = 0;
                var odd = 0;
                while (size > 1)
                {
                    if ((size & 1) == 1)
                        odd = 1;
                    steps++;
                    size /= 2;
                }
                levels = 1 + steps + odd;
            }
            var total = 0;
            int w = width, h = height;
            for (var i = 0; i < levels; i++)
            {
                total += w * h * 4;
                w >>= 1;
                h >>= 1;
            }
            return total;
        }

        [Fact]
        public void UploadedImageMatchesDeclaredSize()
        {
            // Every size the map uses: the white square, the marker shapes,
            // the sun, the globes, the galaxy, the globe seen through the map's
            // camera.
            var sizes = new[]
            {
                new[] { 8, 8 }, new[] { Images.ShapeSize, Images.ShapeSize }, new[] { 256, 256 }, new[] { 1024, 1024 },
                new[] { 512, 288 }, new[] { 768, 768 }, new[] { 3, 5 },
            };
            var ledger = new TextureLedger();
            foreach (var size in sizes)
            {
                int w = size[0], h = size[1];
                var straight = new byte[w * h * 4];
                var upload = ledger.Plan("t" + w + "x" + h, w, h, straight);
                Assert.Equal(TexturePixels.DeclaredLength(w, h), upload.Pixels.Length);
                Assert.Equal(RendererReads(w, h, upload.Mipmaps), upload.Pixels.Length);
                Assert.Equal(w, upload.Width);
                Assert.Equal(h, upload.Height);
            }

            // Mipmaps only where their levels are exact halves.
            Assert.True(TexturePixels.UsesMipmaps(256, 256));
            Assert.False(TexturePixels.UsesMipmaps(512, 288));
            Assert.False(TexturePixels.UsesMipmaps(768, 768));
            Assert.Equal(9, TexturePixels.MipLevels(256, 256));

            // A picture whose bytes do not match its size is refused, never
            // sent.
            Assert.Throws<ArgumentException>(() => TexturePixels.Prepare(64, 64, new byte[64 * 64 * 4 - 1]));
            Assert.Throws<ArgumentException>(() => ledger.Plan("bad", 64, 64, new byte[32 * 32 * 4]));
            Assert.Throws<ArgumentException>(() => TexturePixels.Prepare(0, 64, new byte[0]));
            Assert.False(ledger.Contains("bad"));

            // The real pictures of the map fit their sizes.
            Assert.Equal(TexturePixels.DeclaredLength(Images.ShapeSize, Images.ShapeSize),
                TexturePixels.Prepare(Images.ShapeSize, Images.ShapeSize, Images.ShapeImage(Images.Shape.Disc)).Length);
            Assert.Equal(TexturePixels.DeclaredLength(128, 128), TexturePixels.Prepare(128, 128, Images.SunImage(128)).Length);
        }

        [Fact]
        public void UploadedImageIsNotModifiedAfterSend()
        {
            var ledger = new TextureLedger();
            var straight = Images.ShapeImage(Images.Shape.Disc);
            var size = Images.ShapeSize;

            var first = ledger.Plan("disc", size, size, straight);
            var second = ledger.Plan("disc", size, size, straight);

            // Never the caller's array, never the array of an earlier send.
            Assert.NotSame(straight, first.Pixels);
            Assert.NotSame(first.Pixels, second.Pixels);
            Assert.Equal(TextureAction.Create, first.Action);
            Assert.Equal(TextureAction.Reset, second.Action);

            // What was sent does not change when the picture it came from is
            // painted again, nor when another picture is sent.
            var copy = (byte[])first.Pixels.Clone();
            for (var i = 0; i < straight.Length; i++)
                straight[i] = 7;
            ledger.Plan("disc", size, size, straight);
            ledger.Plan("other", size, size, straight);
            Assert.Equal(copy, first.Pixels);

            // Another size under a name already used: created again, as the
            // renderer ignores a second creation under the same name.
            Assert.Equal(TextureAction.Recreate, ledger.Plan("disc", 8, 8, new byte[8 * 8 * 4]).Action);
        }

        // The game draws its sprites with premultiplied alpha: a transparent
        // pixel must also be black, or the corners of a round picture show as
        // a coloured square around it.
        [Fact]
        public void TransparentCornersStayTransparent()
        {
            var size = Images.ShapeSize;
            foreach (Images.Shape shape in Enum.GetValues(typeof(Images.Shape)))
                AssertNoSquare(TexturePixels.Prepare(size, size, Images.ShapeImage(shape)));
            AssertNoSquare(TexturePixels.Prepare(256, 256, Images.SunImage(256)));

            var colors = new float[8 * 4 * 3];
            for (var i = 0; i < colors.Length; i++)
                colors[i] = 0.8f;
            var globe = Images.GlobeImage(256, 8, 4, colors, new float[8 * 4], 60000, 0, new[] { 0.5f, 0.7f, 1f });
            var sent = TexturePixels.Prepare(256, 256, globe);
            AssertNoSquare(sent);

            // The centre of the globe keeps its colour.
            var centre = (128 * 256 + 128) * 4;
            Assert.Equal(255, sent[centre + 3]);
            Assert.True(sent[centre] > 100);
        }

        // The galaxy lies on the star field of the map: the picture is
        // transparent wherever the galaxy is dark, all along its edges, so
        // that no square shows around it when it is zoomed out.
        [Fact]
        public void GalaxyHasNoSquareAroundIt()
        {
            const int size = 128;
            var sent = TexturePixels.Prepare(size, size, Images.GalaxyImage(size));
            AssertNoSquare(sent);
            for (var i = 0; i < size; i++)
            {
                Assert.Equal(0, sent[(0 * size + i) * 4 + 3]);
                Assert.Equal(0, sent[((size - 1) * size + i) * 4 + 3]);
                Assert.Equal(0, sent[(i * size + 0) * 4 + 3]);
                Assert.Equal(0, sent[(i * size + size - 1) * 4 + 3]);
            }
            // The bright bulge stays bright.
            var centre = (size / 2 * size + size / 2) * 4;
            Assert.True(sent[centre + 3] > 200);

            // The sky tile: stars on a transparent ground.
            var stars = TexturePixels.Prepare(256, 256, Images.StarFieldImage(256, 1));
            AssertNoSquare(stars);
            var clear = 0;
            var lit = 0;
            for (var i = 3; i < 256 * 256 * 4; i += 4)
            {
                if (stars[i] == 0)
                    clear++;
                else
                    lit++;
            }
            Assert.True(clear > lit * 4, clear + " clear, " + lit + " lit");
            Assert.True(lit > 100, lit + " lit");
        }

        private static void AssertNoSquare(byte[] pixels)
        {
            // The texture is sRGB: the colour is compared to the alpha in
            // linear light, as the renderer reads it.
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i + 3] / 255.0;
                for (var c = 0; c < 3; c++)
                    Assert.True(Linear(pixels[i + c]) <= alpha + 0.01, "pixel " + (i / 4) + " brighter than its alpha");
                if (pixels[i + 3] == 0)
                    Assert.True(pixels[i] == 0 && pixels[i + 1] == 0 && pixels[i + 2] == 0, "transparent pixel " + (i / 4) + " not black");
            }
            // The corner of the first level is fully transparent.
            Assert.Equal(0, pixels[3]);
            Assert.Equal(0, pixels[0]);
        }

        private static double Linear(byte value)
        {
            var c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}
