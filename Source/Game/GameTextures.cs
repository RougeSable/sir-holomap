using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VRageRender;
using VRageRender.Messages;

namespace SirHolomap
{
    // Les images de la carte, remises au moteur de rendu du jeu comme des
    // textures générées : calculées sur la machine du joueur, rien n'est livré. Chaque
    // image passe par le registre : un nouveau tableau de la taille exacte que le
    // moteur lit, prémultiplié comme les sprites du jeu l'attendent, jamais
    // retouché une fois envoyé (voir TexturePixels).
    internal static class GameTextures
    {
        public const string White = "SirHolomapWhite";
        public const string Sun = "SirHolomapSun";
        public const string Galaxy = "SirHolomapGalaxy";
        public const int GalaxySize = 1024;
        public const int SunSize = 256;

        private static readonly Dictionary<Images.Shape, string> ShapeNames = new Dictionary<Images.Shape, string>();
        private static readonly TextureLedger Ledger = new TextureLedger();
        private static bool s_basics;
        private static Task<byte[]> s_galaxyTask;
        private static bool s_galaxyReady;
        private static bool s_galaxyFailed;

        public static string Shape(Images.Shape shape)
        {
            string name;
            if (!ShapeNames.TryGetValue(shape, out name))
            {
                name = "SirHolomap" + shape;
                ShapeNames[shape] = name;
            }
            return name;
        }

        public static void EnsureBasics()
        {
            if (s_basics)
                return;
            s_basics = true;

            var white = new byte[8 * 8 * 4];
            for (var i = 0; i < white.Length; i++)
                white[i] = 255;
            Send(White, 8, 8, white);

            foreach (Images.Shape shape in Enum.GetValues(typeof(Images.Shape)))
                Send(Shape(shape), Images.ShapeSize, Images.ShapeSize, Images.ShapeImage(shape));

            Send(Sun, SunSize, SunSize, Images.SunImage(SunSize));
        }

        // La galaxie met un moment à se peindre : faite hors du fil du jeu, montrée
        // quand elle est prête. Rien n'est dessiné avec elle avant.
        public static bool GalaxyReady()
        {
            if (s_galaxyReady)
                return true;
            if (s_galaxyFailed)
                return false;
            if (s_galaxyTask == null)
                s_galaxyTask = Task.Run(() => Images.GalaxyImage(GalaxySize));
            if (!s_galaxyTask.IsCompleted)
                return false;
            var ok = s_galaxyTask.Status == TaskStatus.RanToCompletion && s_galaxyTask.Result != null
                && Send(Galaxy, GalaxySize, GalaxySize, s_galaxyTask.Result);
            s_galaxyTask = null;
            s_galaxyReady = ok;
            s_galaxyFailed = !ok;
            return ok;
        }

        // Crée la texture, ou remplace son image. Faux quand l'image
        // a été refusée (mauvaise taille) : le moteur est alors laissé tranquille.
        public static bool Send(string name, int width, int height, byte[] straight)
        {
            TextureUpload upload;
            try
            {
                upload = Ledger.Plan(name, width, height, straight);
            }
            catch (ArgumentException e)
            {
                HolomapPlugin.LogCaught("image " + name + " non envoyée : " + e.Message, e);
                return false;
            }

            switch (upload.Action)
            {
                case TextureAction.Reset:
                    MyRenderProxy.ResetGeneratedTexture(upload.Name, upload.Pixels);
                    break;
                case TextureAction.Recreate:
                    MyRenderProxy.DestroyGeneratedTexture(upload.Name);
                    Create(upload);
                    break;
                default:
                    Create(upload);
                    break;
            }
            return true;
        }

        private static void Create(TextureUpload upload)
        {
            MyRenderProxy.CreateGeneratedTexture(upload.Name, upload.Width, upload.Height, MyGeneratedTextureType.RGBA,
                TexturePixels.MipLevels(upload.Width, upload.Height), upload.Pixels, upload.Mipmaps, true);
        }

        public static void DestroyAll()
        {
            foreach (var name in Ledger.Names)
            {
                try
                {
                    MyRenderProxy.DestroyGeneratedTexture(name);
                }
                catch (Exception ex)
                {
                    HolomapPlugin.Caught("GameTextures.DestroyAll", ex);
                }
            }
            Ledger.Clear();
            s_basics = false;
            s_galaxyReady = false;
            s_galaxyFailed = false;
            s_galaxyTask = null;
        }
    }
}
