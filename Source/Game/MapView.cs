using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using VRageMath;

namespace SirHolomap
{
    // The four views, in the order of their buttons at the top.
    public enum MapMode
    {
        Planet,
        Local,
        System,
        Galaxy,
    }

    // The two tabs of the system view.
    public enum SystemTab
    {
        Bodies,
        Orrery,
    }

    // One view of the map. Every view answers the same gestures the same way:
    // left drag turns, middle drag (or WASD) moves, the wheel zooms, a click
    // selects, a double click dives into what is under the cursor.
    internal abstract class MapView
    {
        protected readonly MapScreen Map;

        protected MapView(MapScreen map)
        {
            Map = map;
        }

        protected MapWorld World
        {
            get { return Map.World; }
        }

        protected MapCamera Camera
        {
            get { return Map.Camera; }
        }

        protected MapSettings Settings
        {
            get { return Map.Settings; }
        }

        public abstract string Title { get; }
        public abstract string Subtitle { get; }
        public abstract string Help { get; }

        // False for the flat views, drawn over a plain background.
        public virtual bool Uses3D
        {
            get { return true; }
        }

        public virtual double NeededFar
        {
            get { return Camera.Distance * 4 + 50000; }
        }

        public abstract void Update(double dt);
        public abstract void DrawMap();

        public virtual void Rotate(Vector2 delta)
        {
        }

        public virtual void Pan(Vector2 delta)
        {
        }

        public virtual void Wheel(double notches, bool ctrl)
        {
        }

        public virtual void Move(Vector2 keys, double dt)
        {
        }

        public virtual void Recentre()
        {
        }

        public virtual object Pick(Vector2 mouse)
        {
            return null;
        }

        public virtual void DoubleClick(object target)
        {
        }

        // Selecting from the list on the right: the view shows it.
        public virtual void Focus(object target)
        {
        }

        public abstract void DefaultInfo(Panel panel);
        public abstract void Filters(Panel panel);
        public abstract void List(Panel panel);

        // Shared helpers.

        protected bool Passes(Marker marker, int threshold)
        {
            if (marker.IsSelf)
                return true;
            if (marker.IsGps)
                return Settings.ShowGps;
            if (!marker.Live && !Settings.ShowMemories)
                return false;
            switch (marker.Kind)
            {
                case ContactKind.Character:
                    return Settings.ShowPlayers;
                case ContactKind.Station:
                    return Settings.ShowStations && GridListing.Shown(marker.Blocks, threshold, marker.SubGrid);
                case ContactKind.LargeShip:
                    return Settings.ShowShips && GridListing.Shown(marker.Blocks, threshold, marker.SubGrid);
                default:
                    return Settings.ShowSmallGrids && GridListing.Shown(marker.Blocks, threshold, marker.SubGrid);
            }
        }

        protected void MarkerFilters(Panel panel, bool planet)
        {
            panel.Checkbox(Texts.ShowStations, null, Settings.ShowStations, v => Settings.ShowStations = v, 0);
            panel.Checkbox(Texts.ShowShips, null, Settings.ShowShips, v => Settings.ShowShips = v, 1);
            panel.Checkbox(Texts.ShowSmallGrids, null, Settings.ShowSmallGrids, v => Settings.ShowSmallGrids = v, 0);
            panel.Checkbox(Texts.ShowPlayers, null, Settings.ShowPlayers, v => Settings.ShowPlayers = v, 1);
            panel.Checkbox(Texts.ShowGps, null, Settings.ShowGps, v => Settings.ShowGps = v, 0);
            panel.Checkbox(Texts.ShowMemories, null, Settings.ShowMemories, v => Settings.ShowMemories = v, 1);
            if (planet)
            {
                // Another plugin holds the game's light: the box stays, and
                // its help says why it does nothing.
                var help = RenderHooks.LightAvailable ? Texts.DaylightHelp : Texts.DaylightUnavailableHelp;
                panel.Checkbox(Texts.Daylight, help, Settings.DaylightGlobe, v => Settings.DaylightGlobe = v, 0);
                panel.NumberField("thresholdA", Texts.ThresholdPlanet, Texts.ThresholdHelp, Settings.PlanetBlockThreshold,
                    v => Settings.PlanetBlockThreshold = v);
            }
            else
            {
                panel.NumberField("thresholdB", Texts.ThresholdSpace, Texts.ThresholdHelp, Settings.SpaceBlockThreshold,
                    v => Settings.SpaceBlockThreshold = v);
            }
        }

        public static string Distance(double metres)
        {
            if (double.IsNaN(metres) || double.IsInfinity(metres))
                return "-";
            if (metres < 1000)
                return ((int)Math.Round(metres)) + " m";
            if (metres < 100000)
                return (metres / 1000).ToString("0.0") + " km";
            if (metres < 10000000)
                return ((int)Math.Round(metres / 1000)).ToString("N0") + " km";
            return (metres / 1e6).ToString("0.0") + " Mm";
        }

        public static string KindText(Marker marker)
        {
            if (marker.IsGps)
                return Texts.KindGps;
            switch (marker.Kind)
            {
                case ContactKind.Station:
                    return Texts.KindStation;
                case ContactKind.LargeShip:
                    return Texts.KindLargeShip;
                case ContactKind.SmallShip:
                    return Texts.KindSmallShip;
                default:
                    return Texts.KindCharacter;
            }
        }

        public static string RelationText(ContactRelation relation)
        {
            switch (relation)
            {
                case ContactRelation.Own:
                    return Texts.RelationOwn;
                case ContactRelation.Faction:
                    return Texts.RelationFaction;
                case ContactRelation.Enemy:
                    return Texts.RelationEnemy;
                case ContactRelation.Neutral:
                    return Texts.RelationNeutral;
                default:
                    return Texts.RelationUnowned;
            }
        }

        public static string SeenText(Marker marker)
        {
            return marker.Live ? Texts.Live : string.Format(Texts.SeenAgo, TimeAgo.Format(DateTime.UtcNow - marker.LastSeenUtc));
        }

        // The info of a selected marker: the same lines in every view.
        public void MarkerInfo(Panel panel, Marker marker)
        {
            panel.Heading(marker.IsSelf ? Texts.You : marker.Name, KindText(marker));
            if (!marker.IsGps && !marker.IsSelf)
                panel.Line(Texts.Owner, RelationText(marker.Relation), Style.RelationColor(marker.Relation));
            if (marker.IsGrid && !marker.IsSelf)
                panel.Line(Texts.Blocks, marker.Blocks.ToString("N0"));
            if (!marker.IsSelf)
                panel.Line(Texts.Distance, Distance(Vector3D.Distance(marker.Position, World.PlayerPosition)));
            if (marker.Live && !marker.IsGps)
                panel.Line(Texts.Speed, ((int)Math.Round(marker.Velocity.Length())) + " m/s");
            var body = World.FindBody(marker.Body);
            if (body != null)
            {
                var altitude = Vector3D.Distance(marker.Position, body.Centre) - body.Radius;
                panel.Line(Texts.Altitude, Distance(Math.Max(0, altitude)) + "  (" + body.Name + ")");
            }
            if (!marker.IsGps)
                panel.Line(Texts.Seen, SeenText(marker), marker.Live ? Style.Live : Style.Memory);
            if (!marker.IsGps && !marker.IsSelf)
                panel.Line(Texts.Range, marker.Live ? Texts.InRange : Texts.OutOfRange, marker.Live ? Style.Live : Style.Memory);
            panel.Line(Texts.Position, ((Vector3I)marker.Position).ToString());
        }

        // The info of a planet or a moon: the same lines in every view.
        public void BodyInfo(Panel panel, Body body)
        {
            panel.Heading(body.Name, body.IsMoon ? string.Format(Texts.MoonOf, body.Parent.Name) : Texts.KindPlanet);
            var planet = body.Planet;
            var generator = planet != null ? planet.Generator : null;
            if (!string.IsNullOrEmpty(body.Type))
                panel.Line(Texts.Type, body.Type);
            panel.Line(Texts.Diameter, Distance(body.Radius * 2));
            if (generator != null)
            {
                panel.Line(Texts.Gravity, generator.SurfaceGravity.ToString("0.00") + " g");
                if (body.HasAtmosphere && generator.Atmosphere != null)
                {
                    panel.Line(Texts.Atmosphere, generator.Atmosphere.Density.ToString("0.0#") + " kg/m3, "
                        + Distance(body.AtmosphereRadius - body.Radius));
                    panel.Line(Texts.Oxygen, generator.Atmosphere.Breathable
                        ? Texts.Breathable + " (" + (int)Math.Round(generator.Atmosphere.OxygenDensity * 100) + " %)"
                        : Texts.NotBreathable);
                }
                else
                {
                    panel.Line(Texts.Atmosphere, Texts.None);
                }
                panel.Line(Texts.Temperature, TemperatureText(generator.DefaultSurfaceTemperature.ToString()));
                var ores = OresOf(body);
                panel.Line(Texts.Ores, ores.Length == 0 ? Texts.None : ores, Style.Text, true);
            }
            if (body.Moons.Count > 0)
            {
                var names = new List<string>();
                foreach (var moon in body.Moons)
                    names.Add(moon.Name);
                panel.Line(Texts.Moons, string.Join(", ", names.ToArray()), Style.Text, true);
            }
            panel.Line(Texts.Distance, Distance(Math.Max(0, Vector3D.Distance(World.PlayerPosition, body.Centre) - body.Radius)));
        }

        private static string TemperatureText(string level)
        {
            switch (level)
            {
                case "ExtremeFreeze":
                    return Texts.TempExtremeCold;
                case "Freeze":
                    return Texts.TempCold;
                case "Cozy":
                    return Texts.TempTemperate;
                case "Hot":
                    return Texts.TempHot;
                case "ExtremeHot":
                    return Texts.TempExtremeHeat;
                default:
                    return level;
            }
        }

        private static readonly Dictionary<long, string> OreCache = new Dictionary<long, string>();

        // The ores found in the ground of the planet, from its own definition.
        private static string OresOf(Body body)
        {
            string text;
            if (OreCache.TryGetValue(body.Id, out text))
                return text;
            var ores = new List<string>();
            try
            {
                var mappings = body.Planet.Generator.OreMappings;
                if (mappings != null)
                {
                    foreach (var mapping in mappings)
                    {
                        var material = MyDefinitionManager.Static.GetVoxelMaterialDefinition(mapping.Type);
                        var ore = material != null && !string.IsNullOrEmpty(material.MinedOre) ? material.MinedOre : null;
                        if (ore != null && !ores.Contains(ore))
                            ores.Add(ore);
                    }
                }
            }
            catch (Exception)
            {
            }
            ores.Sort(StringComparer.Ordinal);
            text = string.Join(", ", ores.ToArray());
            OreCache[body.Id] = text;
            return text;
        }
    }

    // Colours and sizes shared by every view: the map looks the same in A, B
    // and C.
    internal static class Style
    {
        public static readonly Color Accent = new Color(70, 190, 235);
        public static readonly Color AccentDim = new Color(40, 110, 140);
        public static readonly Color Background = new Color(8, 14, 22, 215);
        public static readonly Color BarBackground = new Color(4, 8, 14, 225);
        public static readonly Color Space = new Color(3, 5, 10, 255);
        public static readonly Color Text = new Color(215, 228, 238);
        public static readonly Color Dim = new Color(130, 152, 168);
        public static readonly Color Live = new Color(120, 230, 140);
        public static readonly Color Memory = new Color(150, 150, 160);
        public static readonly Color Selection = new Color(255, 214, 90);
        public static readonly Color Row = new Color(255, 255, 255, 18);
        public static readonly Color RowHover = new Color(70, 190, 235, 60);
        public static readonly Color RowSelected = new Color(255, 214, 90, 70);
        public static readonly Color Grid = new Color(70, 170, 220);
        public static readonly Color Orbit = new Color(210, 180, 90);

        public static Color RelationColor(ContactRelation relation)
        {
            switch (relation)
            {
                case ContactRelation.Own:
                    return new Color(90, 220, 255);
                case ContactRelation.Faction:
                    return new Color(120, 235, 120);
                case ContactRelation.Enemy:
                    return new Color(255, 90, 80);
                case ContactRelation.Neutral:
                    return new Color(225, 225, 235);
                default:
                    return new Color(215, 190, 120);
            }
        }

        // A memory is greyed: the eye tells it from a live marker without
        // reading.
        public static Color MarkerColor(Marker marker)
        {
            if (marker.IsSelf)
                return Color.White;
            var color = marker.IsGps ? marker.GpsColor : RelationColor(marker.Relation);
            if (marker.Live)
                return color;
            var grey = (color.R + color.G + color.B) / 3;
            return new Color((color.R + grey * 3) / 4 * 0.75f / 255f, (color.G + grey * 3) / 4 * 0.75f / 255f,
                (color.B + grey * 3) / 4 * 0.8f / 255f, 0.62f);
        }

        public static string Icon(Marker marker)
        {
            if (marker.IsGps)
                return GameTextures.Shape(Images.Shape.Diamond);
            switch (marker.Kind)
            {
                case ContactKind.Station:
                    return GameTextures.Shape(Images.Shape.Square);
                case ContactKind.LargeShip:
                    return GameTextures.Shape(Images.Shape.Triangle);
                case ContactKind.SmallShip:
                    return GameTextures.Shape(Images.Shape.Triangle);
                default:
                    return GameTextures.Shape(Images.Shape.Disc);
            }
        }

        public static float IconSize(Marker marker)
        {
            if (marker.IsSelf)
                return 16;
            switch (marker.Kind)
            {
                case ContactKind.Station:
                    return 13;
                case ContactKind.LargeShip:
                    return 14;
                case ContactKind.SmallShip:
                    return 10;
                default:
                    return 8;
            }
        }
    }

    // Draws markers the same way in every 3D view.
    internal static class MarkerPainter
    {
        public static void Paint(MapScreen map, Marker marker, Vector2 at, bool label, string extra, bool showAge = true)
        {
            var s = Gfx.Scale;
            var color = Style.MarkerColor(marker);
            var size = Style.IconSize(marker) * s;
            var selected = ReferenceEquals(map.Selected, marker);
            var hovered = ReferenceEquals(map.Hovered, marker) || ReferenceEquals(map.ListHovered, marker);

            if (marker.IsSelf)
            {
                var pulse = 0.5f + 0.5f * (float)Math.Sin(map.Time * 3);
                Gfx.Sprite(GameTextures.Shape(Images.Shape.Glow), at, (34 + 8 * pulse) * s, Gfx.Alpha(Style.Accent, 0.55f));
                Gfx.Sprite(GameTextures.Shape(Images.Shape.Ring), at, 22 * s, Style.Accent);
                Gfx.Sprite(GameTextures.Shape(Images.Shape.Disc), at, 9 * s, Color.White);
            }
            else
            {
                if (hovered || selected)
                    Gfx.Sprite(GameTextures.Shape(Images.Shape.Glow), at, size * 3.2f, Gfx.Alpha(selected ? Style.Selection : color, 0.45f));
                Gfx.Sprite(Style.Icon(marker), at, size, color);
                if (!marker.Live && !marker.IsGps)
                    Gfx.Sprite(GameTextures.Shape(Images.Shape.Ring), at, size * 1.7f, Gfx.Alpha(Style.Memory, 0.5f));
            }

            if (selected)
            {
                var pulse = 1 + 0.08f * (float)Math.Sin(map.Time * 6);
                Gfx.Brackets(at, Math.Max(size * 2.6f, 26 * s) * pulse, Style.Selection);
            }

            if (label || hovered || selected || marker.IsSelf)
            {
                var name = marker.IsSelf ? Texts.You : Gfx.Fit(marker.Name, 0.62f, 220 * s);
                var textColor = marker.Live ? Gfx.Alpha(Style.Text, 0.95f) : Gfx.Alpha(Style.Memory, 0.9f);
                Gfx.Text(name, at.X + size * 0.8f + 4 * s, at.Y - 10 * s, 0.62f, selected ? Style.Selection : textColor);
                var second = extra;
                if (!marker.Live && !marker.IsGps)
                    second = SeenShort(marker) + (string.IsNullOrEmpty(extra) ? "" : "  " + extra);
                if (!string.IsNullOrEmpty(second))
                    Gfx.Text(second, at.X + size * 0.8f + 4 * s, at.Y + 4 * s, 0.52f, Gfx.Alpha(Style.Dim, 0.9f));
            }
            else if (showAge && !marker.Live && !marker.IsGps)
            {
                // Memories always say when they were seen.
                Gfx.Text(SeenShort(marker), at.X + size * 0.8f + 3 * s, at.Y - 5 * s, 0.48f, Gfx.Alpha(Style.Memory, 0.85f));
            }
        }

        public static string SeenShort(Marker marker)
        {
            return string.Format(Texts.SeenAgo, TimeAgo.Format(DateTime.UtcNow - marker.LastSeenUtc));
        }
    }
}
