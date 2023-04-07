#nullable enable
using Maps.Search;
using Maps.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Maps.API
{
    internal class RouteHandler : DataHandlerBase
    {
        protected override DataResponder GetResponder(HttpContext context) => new Responder(context);

        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType => ContentTypes.Text.Xml;
            private class TravellerPathFinder : PathFinder.IMap<World>
            {
                ResourceManager manager;
                SectorMap.Milieu map;

                public int Jump { get; set; }
                public bool RequireWildernessRefuelling { get; set; }
                public bool AvoidRedZones { get; set; }
                public bool ImperialWorldsOnly { get; set; }
                public bool AllowAnomalies { get; set; }

                public TravellerPathFinder(ResourceManager manager, SectorMap.Milieu map, int jump)
                {
                    this.manager = manager;
                    this.map = map;
                    Jump = jump;
                }

                private World? end;

                public List<World>? FindPath(World start, World end)
                {
                    this.end = end;
                    return PathFinder.FindPath<World>(this, start, end);
                }

                IEnumerable<World> PathFinder.IMap<World>.Neighbors(World world)
                {
                    if (world == null) throw new ArgumentNullException(nameof(world));
                    foreach (World w in new HexSelector(map, manager, Astrometrics.CoordinatesToLocation(world.Coordinates), Jump).Worlds)
                    {
                        // Exclude destination from filters.
                        if (w != end)
                        {
                            if (!AllowAnomalies && w.IsAnomaly) continue;
                            if (RequireWildernessRefuelling && (w.GasGiants == 0 && !w.WaterPresent)) continue;
                            if (AvoidRedZones && w.IsRed) continue;
                            if (ImperialWorldsOnly && !SecondSurvey.IsDefaultAllegiance(w.Allegiance)) continue;
                        }

                        yield return w;
                    }
                }

                int PathFinder.IMap<World>.CostEstimate(World a, World b)
                {
                    if (a == null) throw new ArgumentNullException(nameof(a));
                    if (b == null) throw new ArgumentNullException(nameof(b));
                    return (int)Math.Ceiling(Astrometrics.HexDistance(a.Coordinates, b.Coordinates) / (float)Jump);
                }
            }

            private World? ResolveLocation(HttpContext context, string field, ResourceManager manager, SectorMap.Milieu map)
            {
                string query = context.Request.QueryString[field];
                if (string.IsNullOrWhiteSpace(query))
                    throw new HttpError(400, "Bad Request", $"Missing {field} location");

                query = query.Trim();

                Match match = Regex.Match(query, @"^(?<sector>.+?)\s+(?<hex>\d\d\d\d)$");
                if (!match.Success)
                {
                    int x = GetIntOption("x", 0);
                    int y = GetIntOption("y", 0);
                    WorldResult loc = SearchEngine.FindNearestWorldMatch(query, GetStringOption("milieu", SectorMap.DEFAULT_MILIEU)!, x, y) ??
                        throw new HttpError(404, "Not Found", $"Location not found: {query}");
                    loc.Resolve(map, manager, out _, out World? loc_world);
                    return loc_world;
                }

                string name = match.Groups["sector"].Value;
                Sector sector = map.FromName(name) ??
                    throw new HttpError(404, "Not Found", $"Sector not found: {name}");

                string hexString = match.Groups["hex"].Value;
                Hex hex = new Hex(hexString);
                if (!hex.IsValid)
                    throw new HttpError(400, "Not Found", $"Invalid hex: {hexString}");

                World world = sector.GetWorlds(manager)?[hex.ToInt()] ??
                    throw new HttpError(404, "Not Found", $"No such world: {sector.Names[0].Text} {hexString}");

                return world;
            }

            public override void Process(ResourceManager resourceManager)
            {
                SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, GetStringOption("milieu"));

                World? startWorld = ResolveLocation(Context, "start", resourceManager, map);
                if (startWorld == null)
                    return;

                World? endWorld = ResolveLocation(Context, "end", resourceManager, map);
                if (endWorld == null)
                    return;

                int jump = GetIntOption("jump", 2).Clamp(0, 12);

                var finder = new TravellerPathFinder(resourceManager, map, jump)
                {
                    RequireWildernessRefuelling = GetBoolOption("wild", false),
                    ImperialWorldsOnly = GetBoolOption("im", false),
                    AvoidRedZones = GetBoolOption("nored", false),
                    AllowAnomalies = GetBoolOption("aok", false)
                };
                List<World> route = finder.FindPath(startWorld, endWorld) ??
                    throw new HttpError(404, "Not Found", "No route found");

                SendResult(route.Select(w => new Results.RouteStop(w)).ToList());
            }
        }
    }
}

namespace Maps.API.Results
{
#nullable disable
    public class RouteStop
    {
        public RouteStop() { }
        public RouteStop(World w)
        {
            if (w == null) throw new ArgumentNullException(nameof(w));

            Sector = w.SectorName;
            SectorX = w.Sector.X;
            SectorY = w.Sector.Y;

            Subsector = w.SubsectorName;

            Name = w.Name;
            Hex = w.Hex;
            HexX = w.X;
            HexY = w.Y;

            UWP = w.UWP;
            PBG = w.PBG;
            Zone = w.Zone;
            AllegianceName = w.AllegianceName;

        }

        public string Sector { get; set; }
        public int SectorX { get; set; }
        public int SectorY { get; set; }

        public string Subsector { get; set; }

        public string Name { get; set; }
        public string Hex { get; set; }
        public int HexX { get; set; }
        public int HexY { get; set; }

        public string UWP { get; set; }
        public string PBG { get; set; }
        public string Zone { get; set; }
        public string AllegianceName { get; set; }
    }
#nullable restore
}
