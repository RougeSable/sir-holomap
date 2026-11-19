using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using VRage.Game;
using VRageMath;

namespace SirHolomap
{
    // Les petits globes de la vue Système, peints d'après la vraie planète : son
    // relief, lu dans la forme propre de la planète, et le matériau de terrain que le
    // jeu lui-même met à chaque endroit, coloré comme vu de loin (voir
    // TerrainColors). Toute planète marche, celles des mods comprises : rien n'est
    // préparé d'avance.
    //
    // Ils sont peints dès qu'un monde est rejoint, carte ouverte ou non, quelques
    // millisecondes par image pour que le jeu ne se fige jamais : un globe grossier
    // d'abord, prêt en quelques images, puis le complet. Le temps que le
    // joueur ouvre la vue Système, les astres sont peints, jamais des disques gris.
    // L'image elle-même est composée hors du fil du jeu.
    internal sealed class PlanetGlobes
    {
        public const int Columns = 96;
        public const int Rows = 48;
        public const int ImageSize = 256;

        // Un échantillon sur quatre dans chaque direction fait le globe grossier.
        private const int CoarseStep = 4;

        // Temps donné à l'échantillonnage par image, en millisecondes.
        private const double BudgetClosed = 1.5;
        private const double BudgetOpen = 5.0;

        // Un seul échantillon plus lent que cela met l'échantillonnage au repos pendant que la carte
        // est fermée, pour ce nombre d'images.
        private const double SlowSample = 1.0;
        private const int RestFrames = 30;
        private int m_rest;

        private delegate MyVoxelMaterialDefinition MaterialAtPosition(ref Vector3 storagePosition, float lodSize);
        private delegate void PrepareRules(ref BoundingBox storageBox);
        private delegate void PrepareShape();

        // La recherche de matériau propre à la planète, trouvée par son nom : quand une version du
        // jeu la renomme, les globes gardent leur relief, peints en gris.
        private static readonly MaterialAtPosition MissingLookup = (ref Vector3 p, float l) => null;

        // Les matériaux de la planète, tels que le jeu les lit pour son propre terrain.
        // Le jeu garde les règles des biomes par fil, dans une liste
        // partagée par toutes les planètes, et seuls les fils qui construisent le terrain
        // la remplissent : sur le fil du jeu elle manque, et chaque recherche du
        // matériau d'un biome échoue. Les hauteurs que la recherche lit passent par
        // le cache de coefficients de la forme, lui aussi gardé par fil et préparé seulement
        // par MyPlanetShapeProvider.PrepareCache, que le jeu appelle lui-même
        // avant de lire les matériaux (MyPlanetEnvironmentComponent) : sans
        // lui chaque recherche échoue sur le fil du jeu, et un cache laissé par
        // une autre planète donnerait les hauteurs de cette planète-là. Prime prépare le
        // cache de cette planète, puis remplit les règles avec chaque règle de
        // cette planète, avant que les échantillons d'une image soient pris.
        private sealed class MaterialSource
        {
            public MyPlanet Planet;
            public MaterialAtPosition Lookup = MissingLookup;
            public PrepareShape Cache;
            public PrepareRules Prime;
        }

        private static MaterialSource MaterialLookup(MyPlanet planet)
        {
            var source = new MaterialSource { Planet = planet };
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var provider = planet.GetType().GetProperty("Provider", flags | BindingFlags.DeclaredOnly)
                    ?? planet.GetType().GetProperty("Provider", flags);
                var storage = provider != null ? provider.GetValue(planet, null) : null;
                var shapeProperty = storage != null ? storage.GetType().GetProperty("Shape", flags) : null;
                var shape = shapeProperty != null ? shapeProperty.GetValue(storage, null) : null;
                var cache = shape != null
                    ? shape.GetType().GetMethod("PrepareCache", flags, null, Type.EmptyTypes, null)
                    : null;
                var materialProperty = storage != null ? storage.GetType().GetProperty("Material", flags) : null;
                var materials = materialProperty != null ? materialProperty.GetValue(storage, null) : null;
                var method = materials != null
                    ? materials.GetType().GetMethod("GetMaterialForPosition", flags, null,
                        new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, null)
                    : null;
                var prepare = materials != null
                    ? materials.GetType().GetMethod("PrepareRulesForBox", flags, null,
                        new[] { typeof(BoundingBox).MakeByRefType() }, null)
                    : null;
                if (method != null && method.ReturnType == typeof(MyVoxelMaterialDefinition))
                    source.Lookup = (MaterialAtPosition)Delegate.CreateDelegate(typeof(MaterialAtPosition), materials, method);
                else
                    HolomapPlugin.LogError("recherche de matériau de planète introuvable : globes peints en gris");
                if (cache != null && cache.ReturnType == typeof(void))
                    source.Cache = (PrepareShape)Delegate.CreateDelegate(typeof(PrepareShape), shape, cache);
                else
                    HolomapPlugin.LogError("cache de forme de planète introuvable : les globes peuvent être peints en gris");
                if (prepare != null && prepare.ReturnType == typeof(void))
                    source.Prime = (PrepareRules)Delegate.CreateDelegate(typeof(PrepareRules), materials, prepare);
                else
                    HolomapPlugin.LogError("règles de matériaux de planète introuvables : les biomes peuvent être peints avec leur matériau par défaut");
            }
            catch (Exception e)
            {
                HolomapPlugin.LogCaught("recherche de matériau de planète échouée : " + e.Message, e);
            }
            return source;
        }

        private static MaterialSource SourceOf(Job job)
        {
            var planet = job.Body.Planet;
            if (job.Materials == null || !ReferenceEquals(job.Materials.Planet, planet))
                job.Materials = MaterialLookup(planet);
            return job.Materials;
        }

        // Le cache de forme de cette planète d'abord, comme le fait le jeu avant
        // ses propres recherches. Peu coûteux quand c'est déjà celui de cette planète.
        private static void PrimeShape(Job job, MaterialSource source)
        {
            if (source.Cache == null)
                return;
            try
            {
                source.Cache();
            }
            catch (Exception e)
            {
                HolomapPlugin.LogCaught("cache de forme de la planète " + job.Body.Name + " pas prêt : " + e.Message, e);
                source.Cache = null;
            }
        }

        // Une boîte aussi petite demande au jeu chaque règle de la planète, pas
        // seulement celles d'une région (MyPlanetMaterialProvider.PrepareRulesForBox).
        private static void Prime(Job job)
        {
            var source = SourceOf(job);
            PrimeShape(job, source);
            if (source.Prime == null)
                return;
            try
            {
                var box = new BoundingBox(Vector3.Zero, Vector3.One);
                source.Prime(ref box);
            }
            catch (Exception e)
            {
                HolomapPlugin.LogCaught("règles de matériaux de la planète " + job.Body.Name + " pas prêtes : " + e.Message, e);
                source.Prime = null;
            }
        }

        private sealed class Job
        {
            public MaterialSource Materials;
            public Body Body;
            public int Next;
            public readonly MyVoxelMaterialDefinition[] Terrain = new MyVoxelMaterialDefinition[Columns * Rows];
            public readonly float[] Colors = new float[Columns * Rows * 3];
            public readonly float[] Heights = new float[Columns * Rows];
            public readonly bool[] Done = new bool[Columns * Rows];
            public string Texture;
            public bool Coarse;
            public bool Full;
            public bool Failed;
            public bool SampleFailureLogged;
            public Task<byte[]> Painting;
            public bool PaintingFull;
            public GlobeIdentity Identity;
        }

        // L'ordre où les échantillons sont pris : le réseau grossier d'abord.
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
        private readonly Dictionary<long, Job> m_kept = new Dictionary<long, Job>();
        private readonly TerrainColors m_terrain = new TerrainColors();
        private readonly System.Diagnostics.Stopwatch m_watch = new System.Diagnostics.Stopwatch();

        // La texture du globe d'un astre, ou null pendant qu'il est peint.
        public string TextureOf(Body body)
        {
            return JobOf(body).Texture;
        }

        private Job JobOf(Body body)
        {
            Job job;
            if (!m_jobs.TryGetValue(body.Id, out job))
            {
                job = Kept(body) ?? new Job { Body = body };
                m_jobs[body.Id] = job;
            }
            job.Body = body;
            return job;
        }

        private static GlobeIdentity IdentityOf(Body body)
        {
            return new GlobeIdentity(body.Id, body.Type, body.Radius, new Vec3(body.Centre.X, body.Centre.Y, body.Centre.Z));
        }

        // Un globe peint avant que le joueur quitte le monde, encore juste pour
        // cet astre : montré de nouveau tout de suite, sa texture n'a jamais quitté le jeu.
        private Job Kept(Body body)
        {
            Job job;
            if (!m_kept.TryGetValue(body.Id, out job))
                return null;
            m_kept.Remove(body.Id);
            if (!job.Identity.Matches(IdentityOf(body)))
                return null;
            job.Body = body;
            job.Materials = null;
            return job;
        }

        // Chaque astre du monde voit son globe lancé, les plus proches d'abord.
        public void Prepare(IList<Body> bodies)
        {
            foreach (var body in bodies)
                JobOf(body);
        }

        // En quittant ou rejoignant un monde : les globes en cours de peinture repartent
        // de zéro, mais les terminés sont gardés, pour être montrés tout de suite si le joueur
        // rejoint le même monde.
        public void Clear()
        {
            foreach (var pair in m_jobs)
            {
                var job = pair.Value;
                if (job.Full && job.Texture != null && !job.Failed && job.Painting == null)
                {
                    job.Materials = null;
                    m_kept[pair.Key] = job;
                }
            }
            m_jobs.Clear();
            m_terrain.Clear();
        }

        // Le globe de la vue A peint par la carte elle-même, tel que la caméra de la carte
        // le voit, quand la caméra du jeu ne peut pas être empruntée. Peint hors du
        // fil du jeu, de nouveau seulement quand la caméra ou la lumière a bougé ; null
        // jusqu'à ce que la première image de cet astre soit prête. L'image couvre
        // tout l'écran.
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

            // Dans le repère propre de la planète, celui où les échantillons ont été pris.
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

        // Un peu de travail à chaque image, pour les globes pas encore peints : les
        // globes grossiers de chaque astre d'abord, puis les complets. Un globe est
        // peint une fois que la couleur de chaque terrain qu'il montre est connue.
        public void Work(bool mapOpen)
        {
            foreach (var job in m_jobs.Values)
                Collect(job);

            // Un échantillon qui a pris du temps (le sol d'une planète lointaine pas encore chargé)
            // met l'échantillonnage au repos quelques images pendant que la carte est fermée :
            // le jeu ne saccade jamais pour un globe que personne ne regarde.
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
                    if (job.Next < goal)
                        Prime(job);
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
                    {
                        if (m_terrain.AllReady(job.Terrain))
                            Paint(job, true);
                    }
                    else if (!job.Coarse && job.Next >= CoarseCount)
                    {
                        if (m_terrain.AllReady(job.Terrain))
                            Paint(job, false);
                    }
                    if (m_watch.Elapsed.TotalMilliseconds > budget)
                        return;
                }
            }
        }

        private void Sample(Job job, int index)
        {
            var planet = job.Body.Planet;
            var column = index % Columns;
            var row = index / Columns;
            var lat = Math.PI / 2 - (row + 0.5) * Math.PI / Rows;
            var lon = (column + 0.5) * 2 * Math.PI / Columns;
            var local = new Vector3D(Math.Cos(lat) * Math.Cos(lon), Math.Sin(lat), Math.Cos(lat) * Math.Sin(lon));
            var direction = Vector3D.TransformNormal(local, planet.WorldMatrix);

            var centre = planet.PositionComp.GetPosition();
            MyVoxelMaterialDefinition material = null;
            double height = 0;
            try
            {
                var above = centre + direction * (planet.MaximumRadius + 100);
                var surface = planet.GetClosestSurfacePointGlobal(ref above);
                height = (surface - centre).Length() - planet.AverageRadius;

                // Juste sous le sol, dans le repère de stockage de la planète, lu
                // comme le jeu lit son terrain d'un peu loin : le matériau
                // de surface, pas la roche dessous.
                var inside = surface - direction * 0.25;
                var storage = (Vector3)(inside - planet.PositionLeftBottomCorner);
                var source = SourceOf(job);
                PrimeShape(job, source);
                material = source.Lookup != MissingLookup ? source.Lookup(ref storage, 4f) : null;
                m_terrain.Request(material);
            }
            catch (Exception e)
            {
                if (!job.SampleFailureLogged)
                {
                    job.SampleFailureLogged = true;
                    HolomapPlugin.LogCaught("terrain de " + job.Body.Name + " non lu : " + e.GetType().Name + " " + e.Message, e);
                }
            }

            job.Terrain[index] = material;
            job.Heights[index] = (float)height;
            job.Done[index] = true;
        }

        // L'image est composée hors du fil du jeu, à partir d'une copie des
        // échantillons : la grossière remplit chaque cellule depuis son échantillon le plus proche.
        private void Paint(Job job, bool full)
        {
            for (var i = 0; i < Columns * Rows; i++)
            {
                float r, g, b;
                m_terrain.ColorOf(job.Terrain[i], out r, out g, out b);
                job.Colors[i * 3] = r;
                job.Colors[i * 3 + 1] = g;
                job.Colors[i * 3 + 2] = b;
            }
            var colors = (float[])job.Colors.Clone();
            var heights = (float[])job.Heights.Clone();
            if (!full)
                FillFromCoarse(colors, heights, job.Done);
            var radius = job.Body.Radius;
            var atmosphere = job.Body.HasAtmosphere ? new[] { 0.55f, 0.75f, 1.0f } : null;
            job.PaintingFull = full;
            job.Identity = IdentityOf(job.Body);
            if (!full)
                job.Coarse = true;
            // Vu du côté que le soleil éclaire, au début.
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
