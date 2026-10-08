using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using VRage.Game;
using VRageMath;

namespace SirHolomap
{
    // The small globes of the system view, painted from the real planet: its
    // relief, read from the planet's own shape, and the colour the game gives
    // each terrain material when seen from far away. Any planet works,
    // modded ones included: nothing is prepared in advance. The sampling is
    // spread over frames so that the game never stalls.
    internal sealed class PlanetGlobes
    {
        public const int Columns = 96;
        public const int Rows = 48;
        public const int ImageSize = 256;
        private const int SamplesPerFrame = 160;

        private delegate MyVoxelMaterialDefinition MaterialAtPosition(ref Vector3 storagePosition, float lodSize);

        // The planet's own material lookup, found by name: when a version of
        // the game renames it, the globes keep their relief, painted grey.
        private static readonly MaterialAtPosition MissingLookup = (ref Vector3 p, float l) => null;

        private static MaterialAtPosition MaterialLookup(MyPlanet planet)
        {
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var provider = planet.GetType().GetProperty("Provider", flags);
                var storage = provider != null ? provider.GetValue(planet, null) : null;
                var materialProperty = storage != null ? storage.GetType().GetProperty("Material", flags) : null;
                var materials = materialProperty != null ? materialProperty.GetValue(storage, null) : null;
                var method = materials != null
                    ? materials.GetType().GetMethod("GetMaterialForPosition", flags, null,
                        new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, null)
                    : null;
                if (method != null && method.ReturnType == typeof(MyVoxelMaterialDefinition))
                    return (MaterialAtPosition)Delegate.CreateDelegate(typeof(MaterialAtPosition), materials, method);
                HolomapPlugin.Log("planet material lookup not found: globes painted grey");
            }
            catch (Exception e)
            {
                HolomapPlugin.Log("planet material lookup failed: " + e.Message);
            }
            return MissingLookup;
        }

        private sealed class Job
        {
            public MaterialAtPosition MaterialAt;
            public Body Body;
            public int Next;
            public readonly float[] Colors = new float[Columns * Rows * 3];
            public readonly float[] Heights = new float[Columns * Rows];
            public string Texture;
            public bool Failed;
        }

        private readonly Dictionary<long, Job> m_jobs = new Dictionary<long, Job>();

        // The texture of a body's globe, or null while it is being painted.
        public string TextureOf(Body body)
        {
            Job job;
            if (!m_jobs.TryGetValue(body.Id, out job))
            {
                job = new Job { Body = body };
                m_jobs[body.Id] = job;
            }
            job.Body = body;
            return job.Texture;
        }

        public void Clear()
        {
            m_jobs.Clear();
        }

        // The globe of view A painted by the map itself, as the map's camera
        // sees it, when the game's camera cannot be borrowed. Painted off the
        // game thread, again only when the camera or the light moved; null
        // until the first picture of this body is ready. The picture covers
        // the whole screen.
        public const string ViewTexture = "SirHolomapGlobeView";
        public const int ViewWidth = 512;
        public const int ViewHeight = 288;

        private struct ViewKey
        {
            public long Body;
            public Vector3D Eye;
            public Vector3D Forward;
            public Vector3D Up;
            public Vector3D Light;
            public double TanX;
            public double TanY;

            public bool Near(ViewKey other)
            {
                return Body == other.Body
                    && (Eye - other.Eye).LengthSquared() < 1e-12
                    && (Forward - other.Forward).LengthSquared() < 1e-10
                    && (Up - other.Up).LengthSquared() < 1e-10
                    && (Light - other.Light).LengthSquared() < 1e-6
                    && Math.Abs(TanX - other.TanX) < 1e-5
                    && Math.Abs(TanY - other.TanY) < 1e-5;
            }
        }

        private Task<byte[]> m_viewTask;
        private ViewKey m_viewPainting;
        private ViewKey m_viewShown;
        private bool m_viewAny;

        public string ViewOf(Body body, MapCamera camera, Vector3D toLight)
        {
            if (body == null || TextureOf(body) == null || body.Planet == null || body.Planet.Closed)
                return null;
            var job = m_jobs[body.Id];

            if (m_viewTask != null && m_viewTask.IsCompleted)
            {
                if (m_viewTask.Status == TaskStatus.RanToCompletion && m_viewTask.Result != null)
                {
                    if (GameTextures.Send(ViewTexture, ViewWidth, ViewHeight, m_viewTask.Result))
                    {
                        m_viewShown = m_viewPainting;
                        m_viewAny = true;
                    }
                }
                m_viewTask = null;
            }

            // Into the planet's own frame, the one the samples were taken in.
            var world = body.Planet.WorldMatrix;
            var toLocal = MatrixD.Transpose(world.GetOrientation());
            var radius = Math.Max(body.Radius, 1);
            var key = new ViewKey
            {
                Body = body.Id,
                Eye = Vector3D.TransformNormal(camera.Position - body.Centre, toLocal) / radius,
                Forward = Vector3D.TransformNormal(camera.Forward, toLocal),
                Up = Vector3D.TransformNormal(camera.Up, toLocal),
                Light = Vector3D.TransformNormal(toLight, toLocal),
                TanX = camera.TanHalfFov * camera.Aspect,
                TanY = camera.TanHalfFov,
            };

            if (m_viewTask == null && !(m_viewAny && key.Near(m_viewShown)))
            {
                var right = Vector3D.TransformNormal(camera.Right, toLocal);
                var atmosphere = body.HasAtmosphere ? new[] { 0.55f, 0.75f, 1.0f } : null;
                var colors = job.Colors;
                var heights = job.Heights;
                var k = key;
                m_viewPainting = key;
                m_viewTask = Task.Run(() => Images.GlobeViewImage(ViewWidth, ViewHeight, Columns, Rows, colors, heights, radius,
                    ToVec(k.Eye), ToVec(k.Forward), ToVec(right), ToVec(k.Up), k.TanX, k.TanY, ToVec(k.Light), atmosphere));
            }

            return m_viewAny && m_viewShown.Body == body.Id ? ViewTexture : null;
        }

        private static Vec3 ToVec(Vector3D v)
        {
            return new Vec3(v.X, v.Y, v.Z);
        }

        // A little work every frame, for the globes still unpainted.
        public void Work()
        {
            var budget = SamplesPerFrame;
            foreach (var job in m_jobs.Values)
            {
                if (job.Texture != null || job.Failed || job.Body.Planet == null || job.Body.Planet.Closed)
                    continue;
                while (budget-- > 0 && job.Next < Columns * Rows)
                    Sample(job, job.Next++);
                if (job.Next >= Columns * Rows)
                    Finish(job);
                if (budget <= 0)
                    return;
            }
        }

        private static void Sample(Job job, int index)
        {
            var planet = job.Body.Planet;
            var column = index % Columns;
            var row = index / Columns;
            var lat = Math.PI / 2 - (row + 0.5) * Math.PI / Rows;
            var lon = (column + 0.5) * 2 * Math.PI / Columns;
            var local = new Vector3D(Math.Cos(lat) * Math.Cos(lon), Math.Sin(lat), Math.Cos(lat) * Math.Sin(lon));
            var direction = Vector3D.TransformNormal(local, planet.WorldMatrix);

            var centre = planet.PositionComp.GetPosition();
            float r = 0.45f, g = 0.43f, b = 0.40f;
            double height = 0;
            try
            {
                var above = centre + direction * (planet.MaximumRadius + 100);
                var surface = planet.GetClosestSurfacePointGlobal(ref above);
                height = (surface - centre).Length() - planet.AverageRadius;

                // A metre under the ground, in the planet's storage frame.
                var inside = surface - direction * 1.0;
                var storage = (Vector3)(inside - planet.PositionLeftBottomCorner);
                var materialAt = job.MaterialAt ?? (job.MaterialAt = MaterialLookup(planet));
                var material = materialAt != MissingLookup ? materialAt(ref storage, 1f) : null;
                if (material != null)
                    ColorOf(material, ref r, ref g, ref b);
            }
            catch (Exception)
            {
            }

            job.Colors[index * 3] = r;
            job.Colors[index * 3 + 1] = g;
            job.Colors[index * 3 + 2] = b;
            job.Heights[index] = (float)height;
        }

        // The colour of a terrain seen from orbit: the game's own far colour of
        // the material, linear, brought to sRGB; its colour key otherwise.
        private static void ColorOf(MyVoxelMaterialDefinition material, ref float r, ref float g, ref float b)
        {
            var far = material.RenderParams.Far3Color;
            if (far.X + far.Y + far.Z > 0.02f)
            {
                r = (float)Math.Pow(MathHelper.Clamp(far.X, 0, 1), 1 / 2.2);
                g = (float)Math.Pow(MathHelper.Clamp(far.Y, 0, 1), 1 / 2.2);
                b = (float)Math.Pow(MathHelper.Clamp(far.Z, 0, 1), 1 / 2.2);
                return;
            }
            if (material.ColorKey.HasValue)
            {
                var rgb = material.ColorKey.Value.HSVtoColor();
                r = rgb.R / 255f;
                g = rgb.G / 255f;
                b = rgb.B / 255f;
            }
        }

        private static void Finish(Job job)
        {
            float[] atmosphere = null;
            if (job.Body.HasAtmosphere)
                atmosphere = new[] { 0.55f, 0.75f, 1.0f };

            // Seen from the side the sun lights, at the start.
            var image = Images.GlobeImage(ImageSize, Columns, Rows, job.Colors, job.Heights, job.Body.Radius, 0.3, atmosphere);
            var name = "SirHolomapGlobe" + (job.Body.Id < 0 ? "n" + (-job.Body.Id) : job.Body.Id.ToString());
            if (GameTextures.Send(name, ImageSize, ImageSize, image))
                job.Texture = name;
            else
                job.Failed = true;
        }
    }
}
