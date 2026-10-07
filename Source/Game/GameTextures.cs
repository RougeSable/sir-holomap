using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VRageRender;
using VRageRender.Messages;

namespace SirHolomap
{
    // The map's pictures, handed to the game's renderer as generated
    // textures: computed on the player's machine, nothing shipped.
    internal static class GameTextures
    {
        public const string White = "SirHolomapWhite";
        public const string Sun = "SirHolomapSun";
        public const string Galaxy = "SirHolomapGalaxy";
        public const int GalaxySize = 768;

        private static readonly Dictionary<Images.Shape, string> ShapeNames = new Dictionary<Images.Shape, string>();
        private static readonly List<string> Created = new List<string>();
        private static bool s_basics;
        private static Task<byte[]> s_galaxyTask;
        private static bool s_galaxyReady;

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
            Create(White, 8, 8, white);

            foreach (Images.Shape shape in Enum.GetValues(typeof(Images.Shape)))
                Create(Shape(shape), Images.ShapeSize, Images.ShapeSize, Images.ShapeImage(shape));

            Create(Sun, 128, 128, Images.SunImage(128));
        }

        // The galaxy takes a moment to paint: done off the game thread, shown
        // when ready.
        public static bool GalaxyReady()
        {
            if (s_galaxyReady)
                return true;
            if (s_galaxyTask == null)
                s_galaxyTask = Task.Run(() => Images.GalaxyImage(GalaxySize));
            if (!s_galaxyTask.IsCompleted)
                return false;
            if (s_galaxyTask.Status == TaskStatus.RanToCompletion)
                Create(Galaxy, GalaxySize, GalaxySize, s_galaxyTask.Result);
            s_galaxyReady = true;
            return true;
        }

        public static void Create(string name, int width, int height, byte[] data)
        {
            MyRenderProxy.CreateGeneratedTexture(name, width, height, MyGeneratedTextureType.RGBA, 1, data, true, true);
            if (!Created.Contains(name))
                Created.Add(name);
        }

        public static void Replace(string name, int width, int height, byte[] data)
        {
            if (Created.Contains(name))
            {
                MyRenderProxy.ResetGeneratedTexture(name, data);
                return;
            }
            Create(name, width, height, data);
        }

        public static void DestroyAll()
        {
            foreach (var name in Created)
            {
                try
                {
                    MyRenderProxy.DestroyGeneratedTexture(name);
                }
                catch (Exception)
                {
                }
            }
            Created.Clear();
            s_basics = false;
            s_galaxyReady = false;
            s_galaxyTask = null;
        }
    }
}
