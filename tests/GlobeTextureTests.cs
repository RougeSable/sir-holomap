using System;
using Xunit;

namespace SirHolomap.Tests
{
    // Les images remises au moteur de rendu du jeu : le moteur lit exactement
    // ce que la taille annonce (chaque niveau de mipmap compris), depuis le tableau
    // qu'on lui a donné, plus tard et sur son propre fil.
    public class GlobeTextureTests
    {
        // Ce que le moteur lit (MyGeneratedTextureManager.Reset) : depuis le
        // début du tableau, niveau après niveau, largeur x hauteur x 4 octets,
        // chaque niveau moitié du précédent, autant de niveaux que la
        // texture en donne.
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
            // Chaque taille que la carte utilise : le carré blanc, les formes de repères,
            // le soleil, les globes, la galaxie, le globe vu par la caméra
            // de la carte.
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

            // Des mipmaps seulement là où leurs niveaux sont des moitiés exactes.
            Assert.True(TexturePixels.UsesMipmaps(256, 256));
            Assert.False(TexturePixels.UsesMipmaps(512, 288));
            Assert.False(TexturePixels.UsesMipmaps(768, 768));
            Assert.Equal(9, TexturePixels.MipLevels(256, 256));

            // Une image dont les octets ne correspondent pas à sa taille est refusée, jamais
            // envoyée.
            Assert.Throws<ArgumentException>(() => TexturePixels.Prepare(64, 64, new byte[64 * 64 * 4 - 1]));
            Assert.Throws<ArgumentException>(() => ledger.Plan("bad", 64, 64, new byte[32 * 32 * 4]));
            Assert.Throws<ArgumentException>(() => TexturePixels.Prepare(0, 64, new byte[0]));
            Assert.False(ledger.Contains("bad"));

            // Les vraies images de la carte tiennent leurs tailles.
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

            // Jamais le tableau de l'appelant, jamais le tableau d'un envoi précédent.
            Assert.NotSame(straight, first.Pixels);
            Assert.NotSame(first.Pixels, second.Pixels);
            Assert.Equal(TextureAction.Create, first.Action);
            Assert.Equal(TextureAction.Reset, second.Action);

            // Ce qui a été envoyé ne change pas quand l'image dont il vient est
            // repeinte, ni quand une autre image est envoyée.
            var copy = (byte[])first.Pixels.Clone();
            for (var i = 0; i < straight.Length; i++)
                straight[i] = 7;
            ledger.Plan("disc", size, size, straight);
            ledger.Plan("other", size, size, straight);
            Assert.Equal(copy, first.Pixels);

            // Une autre taille sous un nom déjà utilisé : créée de nouveau, car le
            // moteur ignore une seconde création sous le même nom.
            Assert.Equal(TextureAction.Recreate, ledger.Plan("disc", 8, 8, new byte[8 * 8 * 4]).Action);
        }

        // Le jeu dessine ses sprites avec un alpha prémultiplié : un pixel
        // transparent doit aussi être noir, sinon les coins d'une image ronde se montrent comme
        // un carré coloré autour d'elle.
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

            // Le centre du globe garde sa couleur.
            var centre = (128 * 256 + 128) * 4;
            Assert.Equal(255, sent[centre + 3]);
            Assert.True(sent[centre] > 100);
        }

        // La galaxie repose sur le champ d'étoiles de la carte : l'image est
        // transparente partout où la galaxie est sombre, tout le long de ses bords, pour
        // qu'aucun carré ne se montre autour d'elle quand elle est dézoomée.
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
            // Le renflement brillant reste brillant.
            var centre = (size / 2 * size + size / 2) * 4;
            Assert.True(sent[centre + 3] > 200);

            // L'étoile du ciel : un cœur rond, un halo léger, rien dans
            // les coins.
            var star = TexturePixels.Prepare(Images.ShapeSize, Images.ShapeSize, Images.ShapeImage(Images.Shape.Star));
            AssertNoSquare(star);
            var middle = (Images.ShapeSize / 2 * Images.ShapeSize + Images.ShapeSize / 2) * 4;
            var halfway = (Images.ShapeSize / 2 * Images.ShapeSize + Images.ShapeSize * 3 / 4) * 4;
            Assert.True(star[middle + 3] > 240, "core " + star[middle + 3]);
            Assert.InRange(star[halfway + 3], 5, 60);
        }

        private static void AssertNoSquare(byte[] pixels)
        {
            // La texture est en sRGB : la couleur est comparée à l'alpha en
            // lumière linéaire, comme le moteur la lit.
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i + 3] / 255.0;
                for (var c = 0; c < 3; c++)
                    Assert.True(Linear(pixels[i + c]) <= alpha + 0.01, "pixel " + (i / 4) + " plus clair que son alpha");
                if (pixels[i + 3] == 0)
                    Assert.True(pixels[i] == 0 && pixels[i + 1] == 0 && pixels[i + 2] == 0, "pixel transparent " + (i / 4) + " pas noir");
            }
            // Le coin du premier niveau est entièrement transparent.
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
