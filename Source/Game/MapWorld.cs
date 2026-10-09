using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace SirHolomap
{
    // A planet or a moon, read from the real planet entity.
    internal sealed class Body
    {
        public MyPlanet Planet;
        public long Id;
        public string Name = "";
        public string Type = "";
        public Vector3D Centre;
        public double Radius;
        public double MinRadius;
        public double MaxRadius;
        public double AtmosphereRadius;
        public bool HasAtmosphere;
        public Body Parent;
        public readonly List<Body> Moons = new List<Body>();

        public bool IsMoon
        {
            get { return Parent != null; }
        }

        public bool Contains(Vector3D position, double radii)
        {
            return Vector3D.DistanceSquared(position, Centre) < MaxRadius * radii * MaxRadius * radii;
        }
    }

    // Something shown on the map: a grid, a player, a GPS point, live or
    // remembered.
    internal sealed class Marker
    {
        public long Id;
        public ContactKind Kind;
        public ContactRelation Relation;
        public string Name = "";
        public Vector3D Position;
        public Vector3 Velocity;
        public double Radius;
        public int Blocks;
        public bool Live;
        public DateTime LastSeenUtc;
        public string Body = "";
        public bool IsSelf;
        public bool IsGps;
        public Color GpsColor = Color.White;

        public bool IsGrid
        {
            get { return !IsGps && Kind != ContactKind.Character; }
        }
    }

    // What the map knows of the world: what the game sends to the player now,
    // what the player saw before (the memory of this server), the planets,
    // the GPS points. Read on the game thread only.
    internal sealed class MapWorld
    {
        public const string StorageFolder = "Memory";

        private readonly string m_folder;
        private readonly MemoryStore m_store;
        private readonly HashSet<IMyEntity> m_entities = new HashSet<IMyEntity>();
        private readonly List<IMyPlayer> m_players = new List<IMyPlayer>();
        private readonly Dictionary<long, Marker> m_markerCache = new Dictionary<long, Marker>();
        private readonly List<IMyGps> m_gps = new List<IMyGps>();
        private readonly Registry<Body> m_bodyCache = new Registry<Body>(id => new Body { Id = id });
        private readonly List<MyPlanet> m_planets = new List<MyPlanet>();
        private DateTime m_lastBodies = DateTime.MinValue;
        private DateTime m_lastGps = DateTime.MinValue;
        private DateTime m_lastSave = DateTime.UtcNow;

        public ServerIdentity Identity;
        public MapMemory Memory;
        public ServerHistory History;

        public Vector3D PlayerPosition;
        public long PlayerGridId;
        public long PlayerCharacterId;
        public long PlayerIdentityId;
        public Vector3D PlayerForward = Vector3D.Forward;
        public Vector3D PlayerUp = Vector3D.Up;
        public double SyncRadius = double.MaxValue;
        public Vector3D DirectionToSun = Vector3D.Forward;

        public readonly List<Body> Bodies = new List<Body>();
        public readonly List<Marker> Markers = new List<Marker>();

        public MapWorld(string folder)
        {
            m_folder = folder;
            m_store = new MemoryStore(System.IO.Path.Combine(folder, StorageFolder));
            History = ServerHistory.LoadFrom(folder);
        }

        public bool InWorld
        {
            get { return Memory != null; }
        }

        // A world was loaded, or left.
        public void Track()
        {
            var session = MyAPIGateway.Session;
            var inWorld = session != null && session.Player != null && MyAPIGateway.Entities != null;
            if (!inWorld)
            {
                if (Memory != null)
                    Leave();
                return;
            }
            if (Memory != null)
                return;

            Identity = ServerIdentity.Current();
            Memory = m_store.Load(Identity.Key);
            if (Identity.IsRemembered)
            {
                History.RecordVisit(Identity.Key, Identity.Name, Identity.ConnectionString, DateTime.UtcNow);
                SaveHistory();
            }
            m_lastBodies = DateTime.MinValue;
            HolomapPlugin.Log("world joined, memory " + Identity.Key + ": " + Memory.Count + " contacts");
        }

        public void Leave()
        {
            if (Memory == null)
                return;
            Memory.MarkAllRemembered();
            SaveMemory();
            if (Identity != null && Identity.IsRemembered)
            {
                History.RecordVisit(Identity.Key, Identity.Name, Identity.ConnectionString, DateTime.UtcNow);
                SaveHistory();
            }
            Memory = null;
            Identity = null;
            Bodies.Clear();
            m_bodyCache.Clear();
            Markers.Clear();
            m_markerCache.Clear();
        }

        public void SaveMemory()
        {
            if (Memory == null)
                return;
            try
            {
                m_store.Save(Memory);
            }
            catch (Exception e)
            {
                HolomapPlugin.Log("memory not saved: " + e.Message);
            }
            m_lastSave = DateTime.UtcNow;
        }

        private void SaveHistory()
        {
            try
            {
                History.SaveTo(m_folder);
            }
            catch (Exception e)
            {
                HolomapPlugin.Log("server history not saved: " + e.Message);
            }
        }

        public void SaveIfDue()
        {
            if (Memory != null && Memory.Dirty && (DateTime.UtcNow - m_lastSave).TotalSeconds > 60)
                SaveMemory();
        }

        private void ReadPlayer()
        {
            var session = MyAPIGateway.Session;
            var player = session.Player;
            PlayerPosition = player.GetPosition();
            PlayerIdentityId = player.IdentityId;
            PlayerCharacterId = player.Character != null ? player.Character.EntityId : 0;
            PlayerGridId = 0;
            var controlled = session.ControlledObject as IMyCubeBlock;
            if (controlled != null && controlled.CubeGrid != null)
                PlayerGridId = controlled.CubeGrid.EntityId;

            var camera = session.Camera;
            if (camera != null)
            {
                PlayerForward = camera.WorldMatrix.Forward;
                PlayerUp = camera.WorldMatrix.Up;
            }

            var settings = session.SessionSettings;
            var multiplayer = MyAPIGateway.Multiplayer;
            var client = multiplayer != null && multiplayer.MultiplayerActive && !multiplayer.IsServer;
            SyncRadius = client && settings != null && settings.SyncDistance > 0 ? settings.SyncDistance : double.MaxValue;

            DirectionToSun = MySector.DirectionToSunNormalized;
        }

        // Everything the game sends now is seen: its position and the time
        // are written to the memory. Called a few times a second, map open
        // or not.
        public void Scan()
        {
            if (Memory == null)
                return;
            ReadPlayer();
            var now = DateTime.UtcNow;
            if (Bodies.Count == 0 || (now - m_lastBodies).TotalSeconds > 10)
                ReadBodies();

            m_entities.Clear();
            MyAPIGateway.Entities.GetEntities(m_entities, e => e is IMyCubeGrid || e is IMyCharacter);
            m_players.Clear();
            MyAPIGateway.Players.GetPlayers(m_players);

            Memory.BeginScan();
            foreach (var entity in m_entities)
            {
                Sighting sighting;
                if (TryRead(entity, out sighting))
                    Memory.See(sighting, now);
            }
            Memory.EndScan(ToVec(PlayerPosition), SyncRadius, now);
            m_entities.Clear();
        }

        private bool TryRead(IMyEntity entity, out Sighting sighting)
        {
            sighting = new Sighting();
            if (entity == null || entity.MarkedForClose || entity.Closed)
                return false;

            var grid = entity as IMyCubeGrid;
            if (grid != null)
            {
                var full = grid as MyCubeGrid;
                if (grid.Physics == null || (full != null && (full.IsPreview || full.Projector != null)))
                    return false;
                var volume = grid.WorldVolume;
                sighting.Id = grid.EntityId;
                sighting.Name = grid.CustomName ?? grid.DisplayName ?? "";
                sighting.Blocks = full != null ? full.BlocksCount : 1;
                sighting.Radius = volume.Radius;
                sighting.Position = ToVec(volume.Center);
                sighting.Kind = grid.IsStatic ? ContactKind.Station
                    : grid.GridSizeEnum == MyCubeSize.Large ? ContactKind.LargeShip : ContactKind.SmallShip;
                sighting.Relation = RelationOf(grid.BigOwners != null && grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0);
                sighting.Body = BodyNameAt(volume.Center);
                return true;
            }

            var character = entity as IMyCharacter;
            if (character != null)
            {
                if (character.EntityId == PlayerCharacterId || character.IsDead)
                    return false;
                IMyPlayer owner = null;
                foreach (var p in m_players)
                {
                    if (p.Character == character)
                    {
                        owner = p;
                        break;
                    }
                }
                // Only players: animals and robots are not worth remembering.
                if (owner == null || owner.IsBot)
                    return false;
                sighting.Id = character.EntityId;
                sighting.Name = owner.DisplayName ?? "";
                sighting.Blocks = 0;
                sighting.Radius = 1;
                sighting.Position = ToVec(character.GetPosition());
                sighting.Kind = ContactKind.Character;
                sighting.Relation = RelationOf(owner.IdentityId);
                sighting.Body = BodyNameAt(character.GetPosition());
                return true;
            }
            return false;
        }

        private ContactRelation RelationOf(long owner)
        {
            if (owner == 0)
                return ContactRelation.Unowned;
            var player = MyAPIGateway.Session.Player;
            if (player == null)
                return ContactRelation.Neutral;
            switch (player.GetRelationTo(owner))
            {
                case MyRelationsBetweenPlayerAndBlock.Owner:
                    return ContactRelation.Own;
                case MyRelationsBetweenPlayerAndBlock.FactionShare:
                case MyRelationsBetweenPlayerAndBlock.Friends:
                    return ContactRelation.Faction;
                case MyRelationsBetweenPlayerAndBlock.Enemies:
                    return ContactRelation.Enemy;
                case MyRelationsBetweenPlayerAndBlock.NoOwnership:
                    return ContactRelation.Unowned;
                default:
                    return ContactRelation.Neutral;
            }
        }

        public static Vec3 ToVec(Vector3D v)
        {
            return new Vec3(v.X, v.Y, v.Z);
        }

        public static Vector3D ToVector(Vec3 v)
        {
            return new Vector3D(v.X, v.Y, v.Z);
        }

        // The planets are read again every few seconds, but each keeps its
        // own Body for as long as it exists: what the map selected, hovered or
        // dived into stays the same object, so its highlight never drops.
        private void ReadBodies()
        {
            m_lastBodies = DateTime.UtcNow;
            m_entities.Clear();
            MyAPIGateway.Entities.GetEntities(m_entities, e => e is MyPlanet);
            m_planets.Clear();
            foreach (var entity in m_entities)
            {
                var planet = entity as MyPlanet;
                if (planet != null && !planet.Closed)
                    m_planets.Add(planet);
            }
            m_entities.Clear();
            // Always in the same order, so that two planets of the same name
            // keep their numbers.
            m_planets.Sort((a, b) => a.EntityId.CompareTo(b.EntityId));

            Bodies.Clear();
            m_bodyCache.BeginRefresh();
            var names = new Dictionary<string, int>();
            foreach (var planet in m_planets)
            {
                var body = m_bodyCache.Keep(planet.EntityId);
                body.Planet = planet;
                body.Centre = planet.PositionComp.GetPosition();
                body.Radius = planet.AverageRadius;
                body.MinRadius = planet.MinimumRadius;
                body.MaxRadius = planet.MaximumRadius;
                body.AtmosphereRadius = planet.AtmosphereRadius;
                body.HasAtmosphere = planet.HasAtmosphere;
                body.Type = planet.Generator != null ? planet.Generator.Id.SubtypeName : "";
                body.Parent = null;
                body.Moons.Clear();
                var name = PlanetName(planet);
                int count;
                names.TryGetValue(name, out count);
                names[name] = count + 1;
                body.Name = count == 0 ? name : name + " " + (count + 1);
                Bodies.Add(body);
            }
            m_planets.Clear();

            // Planets gone from the world are forgotten.
            m_bodyCache.EndRefresh();

            // Moons follow their planet, the same way the flat view lists them.
            var infos = new List<BodyInfo>();
            var byInfo = new Dictionary<BodyInfo, Body>();
            foreach (var body in Bodies)
            {
                var info = new BodyInfo { Id = body.Id, Name = body.Name, Position = ToVec(body.Centre), Radius = body.Radius };
                infos.Add(info);
                byInfo[info] = body;
            }
            foreach (var row in BodyOrdering.Order(infos))
            {
                var planet = byInfo[row.Planet];
                foreach (var moon in row.Moons)
                {
                    var m = byInfo[moon];
                    m.Parent = planet;
                    planet.Moons.Add(m);
                }
            }
        }

        // "EarthLike-123456d120000" is shown "EarthLike".
        private static string PlanetName(MyPlanet planet)
        {
            var name = planet.StorageName ?? "";
            var dash = name.IndexOf('-');
            if (dash > 0)
                name = name.Substring(0, dash);
            if (name.Length == 0 && planet.Generator != null)
                name = planet.Generator.Id.SubtypeName;
            return name.Length == 0 ? "Planet" : name;
        }

        public Body BodyAt(Vector3D position, double radii)
        {
            Body best = null;
            var bestDistance = double.MaxValue;
            foreach (var body in Bodies)
            {
                if (!body.Contains(position, radii))
                    continue;
                var distance = Vector3D.Distance(position, body.Centre) - body.Radius;
                if (distance < bestDistance)
                {
                    best = body;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private string BodyNameAt(Vector3D position)
        {
            var body = BodyAt(position, 3);
            return body != null ? body.Name : "";
        }

        public Body FindBody(string name)
        {
            foreach (var body in Bodies)
            {
                if (body.Name == name)
                    return body;
            }
            return null;
        }

        public Body FindBody(long id)
        {
            foreach (var body in Bodies)
            {
                if (body.Id == id)
                    return body;
            }
            return null;
        }

        // A body the map holds on to (selected, hovered, dived into), as it
        // is now: the same object while the planet exists, null once gone.
        public Body Follow(Body body)
        {
            return m_bodyCache.Follow(body, b => b.Id);
        }

        // The planet whose gravity holds the player, if any.
        public Body PlanetUnderPlayer()
        {
            float interference;
            var gravity = MyAPIGateway.Physics != null
                ? MyAPIGateway.Physics.CalculateNaturalGravityAt(PlayerPosition, out interference)
                : Vector3.Zero;
            if (gravity.LengthSquared() < 1e-6f)
                return BodyAt(PlayerPosition, 1.05);
            return BodyAt(PlayerPosition, 10) ?? NearestBody(PlayerPosition);
        }

        public Body NearestBody(Vector3D position)
        {
            Body best = null;
            var bestDistance = double.MaxValue;
            foreach (var body in Bodies)
            {
                var distance = Vector3D.Distance(position, body.Centre) - body.Radius;
                if (distance < bestDistance)
                {
                    best = body;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // The markers of this frame: live ones where the game has them now,
        // memories where they were last seen, the player, the GPS points.
        public void Refresh()
        {
            if (Memory == null)
                return;
            ReadPlayer();
            if (Bodies.Count == 0 || (DateTime.UtcNow - m_lastBodies).TotalSeconds > 10)
                ReadBodies();

            Markers.Clear();
            foreach (var contact in Memory.Contacts)
            {
                if (contact.Id == PlayerCharacterId)
                    continue;
                Marker marker;
                if (!m_markerCache.TryGetValue(contact.Id, out marker))
                {
                    marker = new Marker { Id = contact.Id };
                    m_markerCache[contact.Id] = marker;
                }
                marker.Kind = contact.Kind;
                marker.Relation = contact.Relation;
                marker.Name = contact.Name;
                marker.Blocks = contact.Blocks;
                marker.Radius = contact.Radius;
                marker.LastSeenUtc = contact.LastSeenUtc;
                marker.Body = contact.Body;
                marker.Live = contact.IsLive;
                marker.IsSelf = false;
                marker.IsGps = false;
                marker.Position = ToVector(contact.Position);
                marker.Velocity = Vector3.Zero;

                // Live: where it is at this very frame, so that ships move
                // smoothly on the map.
                if (contact.IsLive)
                {
                    IMyEntity entity;
                    if (MyAPIGateway.Entities.TryGetEntityById(contact.Id, out entity) && entity != null && !entity.MarkedForClose)
                    {
                        var grid = entity as IMyCubeGrid;
                        marker.Position = grid != null ? grid.WorldVolume.Center : entity.GetPosition();
                        if (entity.Physics != null)
                            marker.Velocity = entity.Physics.LinearVelocity;
                    }
                }
                Markers.Add(marker);
            }

            Marker self;
            if (!m_markerCache.TryGetValue(long.MinValue, out self))
            {
                self = new Marker { Id = long.MinValue, Kind = ContactKind.Character, Relation = ContactRelation.Own, IsSelf = true };
                m_markerCache[long.MinValue] = self;
            }
            self.Name = MyAPIGateway.Session.Player.DisplayName ?? Texts.You;
            self.Position = PlayerPosition;
            self.Live = true;
            self.LastSeenUtc = DateTime.UtcNow;
            self.Radius = 1;
            var selfBody = BodyAt(PlayerPosition, 3);
            self.Body = selfBody != null ? selfBody.Name : "";
            Markers.Add(self);

            ReadGps();
        }

        private void ReadGps()
        {
            var now = DateTime.UtcNow;
            if ((now - m_lastGps).TotalSeconds > 2)
            {
                m_lastGps = now;
                m_gps.Clear();
                try
                {
                    var gps = MyAPIGateway.Session.GPS;
                    if (gps != null)
                        m_gps.AddRange(gps.GetGpsList(PlayerIdentityId));
                }
                catch (Exception)
                {
                }
            }

            var index = 0;
            foreach (var point in m_gps)
            {
                var id = long.MinValue + 1 + index++;
                Marker marker;
                if (!m_markerCache.TryGetValue(id, out marker))
                {
                    marker = new Marker { Id = id, IsGps = true, Live = true };
                    m_markerCache[id] = marker;
                }
                marker.Name = point.Name ?? "";
                marker.Position = point.Coords;
                marker.GpsColor = point.GPSColor;
                marker.Kind = ContactKind.Station;
                marker.Relation = ContactRelation.Own;
                marker.Live = true;
                marker.IsGps = true;
                var body = BodyAt(point.Coords, 3);
                marker.Body = body != null ? body.Name : "";
                Markers.Add(marker);
            }
        }
    }
}
