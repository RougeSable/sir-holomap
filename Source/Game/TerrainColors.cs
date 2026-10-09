using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VRage.FileSystem;
using VRage.Game;
using VRageMath;

namespace SirHolomap
{
    // The colour of each terrain material seen from orbit, worked out once
    // per material: the far colour the game itself paints it with when it
    // has one, otherwise the mean colour of the material's own texture (read
    // off the game thread, from the game's files or the mod's), otherwise
    // its colour key. Any material works, modded ones included.
    internal sealed class TerrainColors
    {
        private sealed class Swatch
        {
            public float R = 0.45f;
            public float G = 0.43f;
            public float B = 0.40f;
            public Task<float[]> Reading;
            public bool Ready;
        }

        private readonly Dictionary<MyVoxelMaterialDefinition, Swatch> m_swatches =
            new Dictionary<MyVoxelMaterialDefinition, Swatch>();

        // Starts working out the colour of a material, if not done already.
        public void Request(MyVoxelMaterialDefinition material)
        {
            if (material == null || m_swatches.ContainsKey(material))
                return;
            var swatch = new Swatch();
            m_swatches[material] = swatch;

            float r, g, b;
            if (FarColor(material, out r, out g, out b))
            {
                Set(swatch, r, g, b);
                return;
            }
            if (KeyColor(material, out r, out g, out b))
                Set(swatch, r, g, b);
            swatch.Ready = false;

            var files = TextureFiles(material);
            if (files.Count == 0)
            {
                swatch.Ready = true;
                return;
            }
            var name = material.Id.SubtypeName;
            swatch.Reading = Task.Run(() => ReadMean(files, name));
        }

        // True when every material asked for has its colour.
        public bool AllReady(IEnumerable<MyVoxelMaterialDefinition> materials)
        {
            var ready = true;
            foreach (var material in materials)
            {
                if (material == null)
                    continue;
                Swatch swatch;
                if (!m_swatches.TryGetValue(material, out swatch))
                {
                    Request(material);
                    swatch = m_swatches[material];
                }
                Collect(swatch);
                if (!swatch.Ready)
                    ready = false;
            }
            return ready;
        }

        // The colour of a material, sRGB, 0..1; grey when it has none.
        public void ColorOf(MyVoxelMaterialDefinition material, out float r, out float g, out float b)
        {
            r = 0.45f;
            g = 0.43f;
            b = 0.40f;
            Swatch swatch;
            if (material == null || !m_swatches.TryGetValue(material, out swatch))
                return;
            r = swatch.R;
            g = swatch.G;
            b = swatch.B;
        }

        public void Clear()
        {
            m_swatches.Clear();
        }

        private static void Set(Swatch swatch, float r, float g, float b)
        {
            swatch.R = r;
            swatch.G = g;
            swatch.B = b;
            swatch.Ready = true;
        }

        private static void Collect(Swatch swatch)
        {
            var reading = swatch.Reading;
            if (reading == null || !reading.IsCompleted)
                return;
            swatch.Reading = null;
            if (reading.Status == TaskStatus.RanToCompletion && reading.Result != null)
                Set(swatch, reading.Result[0], reading.Result[1], reading.Result[2]);
            swatch.Ready = true;
        }

        // The colour the game paints the material with from far away, linear,
        // brought to sRGB.
        private static bool FarColor(MyVoxelMaterialDefinition material, out float r, out float g, out float b)
        {
            r = g = b = 0;
            try
            {
                var far = material.RenderParams.Far3Color;
                if (far.X + far.Y + far.Z <= 0.02f)
                    return false;
                r = (float)Math.Pow(MathHelper.Clamp(far.X, 0, 1), 1 / 2.2);
                g = (float)Math.Pow(MathHelper.Clamp(far.Y, 0, 1), 1 / 2.2);
                b = (float)Math.Pow(MathHelper.Clamp(far.Z, 0, 1), 1 / 2.2);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool KeyColor(MyVoxelMaterialDefinition material, out float r, out float g, out float b)
        {
            r = g = b = 0;
            if (!material.ColorKey.HasValue)
                return false;
            var rgb = material.ColorKey.Value.HSVtoColor();
            r = rgb.R / 255f;
            g = rgb.G / 255f;
            b = rgb.B / 255f;
            return true;
        }

        // The colour textures of the material, the ones seen from farthest
        // first, and the top faces before the sides: what a globe shows.
        private static List<string> TextureFiles(MyVoxelMaterialDefinition material)
        {
            var files = new List<string>();
            try
            {
                var sets = material.RenderParams.TextureSets;
                if (sets == null)
                    return files;
                string modPath = null;
                if (material.Context != null && !material.Context.IsBaseGame)
                    modPath = material.Context.ModPath;
                string contentPath = null;
                try
                {
                    contentPath = MyFileSystem.ContentPath;
                }
                catch (Exception)
                {
                }
                for (var i = sets.Length - 1; i >= 0; i--)
                {
                    AddFile(files, sets[i].ColorMetalY, modPath, contentPath);
                    AddFile(files, sets[i].ColorMetalXZnY, modPath, contentPath);
                }
            }
            catch (Exception e)
            {
                HolomapPlugin.Log("terrain textures of " + material.Id.SubtypeName + " not found: " + e.Message);
            }
            return files;
        }

        private static void AddFile(List<string> files, string path, string modPath, string contentPath)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            var candidates = new List<string>();
            if (Path.IsPathRooted(path))
                candidates.Add(path);
            if (!string.IsNullOrEmpty(modPath))
                candidates.Add(Path.Combine(modPath, path));
            if (!string.IsNullOrEmpty(contentPath))
                candidates.Add(Path.Combine(contentPath, path));
            foreach (var candidate in candidates)
            {
                if (!files.Contains(candidate))
                    files.Add(candidate);
            }
        }

        private static float[] ReadMean(List<string> files, string name)
        {
            foreach (var file in files)
            {
                try
                {
                    if (!MyFileSystem.FileExists(file))
                        continue;
                    using (var stream = MyFileSystem.OpenRead(file))
                    {
                        float r, g, b;
                        if (stream != null && TextureColor.TryMean(stream, out r, out g, out b))
                            return new[] { r, g, b };
                    }
                }
                catch (Exception)
                {
                }
            }
            HolomapPlugin.Log("no readable colour texture for terrain " + name + ": painted with its colour key or grey");
            return null;
        }
    }
}
