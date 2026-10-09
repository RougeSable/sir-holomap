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
    // modded ones included: nothing is prepared in advance.
    //
    // They are painted as soon as a world is joined, map open or not, a few
    // milliseconds per frame so that the game never stalls: a coarse globe
    // first, ready within a few frames, then the full one. By the time the
    // player opens the system view the bodies are painted, never grey discs.
    // The picture itself is composed off the game thread.
    internal sealed class PlanetGlobes
    {
        public const int Columns = 96;
        public const int Rows = 48;
        public const int ImageSize = 256;

        // Every fourth sample in each direction makes the coarse globe.
        private const int CoarseStep = 4;

        // Time given to the sampling per frame, in milliseconds.
        private const double BudgetClosed = 1.5;
        private const double BudgetOpen = 5.0;

        // A single sample slower than this rests the sampling while the map
        // is closed, for this many frames.
        private const double SlowSample = 1.0;
        private const int RestFrames = 30;
        private int m_rest;

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
            public readonly bool[] Done = new bool[Columns * Rows];
            public string Texture;
            public bool Coarse;
            public bool Full;
            public bool Failed;
            public Task<byte[]> Painting;
            public bool PaintingFull;
        }

        // The order samples are taken in: the coarse lattice first.
        private static readonly int[] Order = BuildOrder();

        private static int[] BuildOrder()
        {
            var order = new List<int>(Columns * Rows);
            for (var row = CoarseStep / 2; row < Rows; row += CoarseStep)
            {
                for (var column = CoarseStep / 2; column < Columns; column += CoarseStep)
                    order.Add(row * Columns + column);
            }
            var coarse = new HashSet<int>(order);
            for (var i = 0; i < Columns * Rows; i++)
            {
                if (!coarse.Contains(i))
                    order.Add(i);
            }
            return order.ToArray();
        }

        private static readonly int CoarseCount = ((Rows - CoarseStep / 2 + CoarseStep - 1) / CoarseStep)
            * ((Columns - CoarseStep / 2 + CoarseStep - 1) / CoarseStep);

        private readonly Dictionary<long, Job> m_jobs = new Dictionary<long, Job>();
        private readonly System.Diagnostics.Stopwatch m_watch = new System.Diagnostics.Stopwatch();

        // The texture of a body's globe, or null while it is being painted.
        public string TextureOf(Body body)
        {
            return JobOf(body).Texture;
        }

        private Job JobOf(Body body)
        {
            Job job;
            if (!m_jobs.TryGetValue(body.Id, out job))
            {
                job = new Job { Body = body };
                m_jobs[body.Id] = job;
            }
            job.Body = body;
            return job;
        }

        // Every body of the world gets its globe started, nearest first.
        public void Prepare(IList<Body> bodies)
        {
            foreach (var body in bodies)
                JobOf(body);
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
            if (!job.Full)
                return null;

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

        // A little work every frame, for the globes still unpainted: the
        // coarse globes of every body first, then the full ones.
        public void Work(bool mapOpen)
        {
            foreach (var job in m_jobs.Values)
                Collect(job);

            // A sample that took long (the ground of a far planet not loaded
            // yet) rests the sampling a few frames while the map is closed:
            // the game never stutters for a globe nobody looks at.
            if (m_rest > 0 && !mapOpen)
            {
                m_rest--;
                return;
            }
            m_rest = 0;

            var budget = mapOpen ? BudgetOpen : BudgetClosed;
            m_watch.Restart();
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var job in m_jobs.Values)
                {
                    if (job.Failed || job.Full || job.Painting != null || job.Body.Planet == null || job.Body.Planet.Closed)
                        continue;
                    var goal = pass == 0 ? CoarseCount : Columns * Rows;
                    if (pass == 0 && job.Coarse)
                        continue;
                    while (job.Next < goal)
                    {
                        var before = m_watch.Elapsed.TotalMilliseconds;
                        Sample(job, Order[job.Next]);
                        job.Next++;
                        var now = m_watch.Elapsed.TotalMilliseconds;
                        if (now - before > SlowSample)
                            m_rest = RestFrames;
                        if (now > budget || m_rest > 0)
                            return;
                    }
                    if (job.Next >= Columns * Rows)
                        Paint(job, true);
                    else if (!job.Coarse && job.Next >= CoarseCount)
                        Paint(job, false);
                    if (m_watch.Elapsed.TotalMilliseconds > budget)
                        return;
                }
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
            job.Done[index] = true;
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

        // The picture is composed off the game thread, from a copy of the
        // samples: the coarse one fills every cell from its nearest sample.
        private static void Paint(Job job, bool full)
        {
            var colors = (float[])job.Colors.Clone();
            var heights = (float[])job.Heights.Clone();
            if (!full)
                FillFromCoarse(colors, heights, job.Done);
            var radius = job.Body.Radius;
            var atmosphere = job.Body.HasAtmosphere ? new[] { 0.55f, 0.75f, 1.0f } : null;
            job.PaintingFull = full;
            if (!full)
                job.Coarse = true;
            // Seen from the side the sun lights, at the start.
            job.Painting = Task.Run(() => Images.GlobeImage(ImageSize, Columns, Rows, colors, heights, radius, 0.3, atmosphere));
        }

        private static void Collect(Job job)
        {
            var painting = job.Painting;
            if (painting == null || !painting.IsCompleted)
                return;
            job.Painting = null;
            if (painting.Status != TaskStatus.RanToCompletion || painting.Result == null)
            {
                job.Failed = true;
                return;
            }
            var name = "SirHolomapGlobe" + (job.Body.Id < 0 ? "n" + (-job.Body.Id) : job.Body.Id.ToString());
            if (GameTextures.Send(name, ImageSize, ImageSize, painting.Result))
            {
                job.Texture = name;
                if (job.PaintingFull)
                    job.Full = true;
            }
            else
            {
                job.Failed = true;
            }
        }

        private static void FillFromCoarse(float[] colors, float[] heights, bool[] done)
        {
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    var index = row * Columns + column;
                    if (done[index])
                        continue;
                    var r = Nearest(row, Rows);
                    var c = Nearest(column, Columns);
                    var from = r * Columns + c;
                    colors[index * 3] = colors[from * 3];
                    colors[index * 3 + 1] = colors[from * 3 + 1];
                    colors[index * 3 + 2] = colors[from * 3 + 2];
                    heights[index] = heights[from];
                }
            }
        }

        private static int Nearest(int value, int count)
        {
            var first = CoarseStep / 2;
            var step = (int)Math.Round((value - first) / (double)CoarseStep);
            var nearest = first + step * CoarseStep;
            while (nearest >= count)
                nearest -= CoarseStep;
            return Math.Max(first, nearest);
        }
    }
}
