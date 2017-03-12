using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Maps.API
{
    internal class RouteHandler : DataHandlerBase
    {
        protected override string ServiceName => "route";
        protected override DataResponder GetResponder(HttpContext context)
        {
            return new Responder(context);
        }
        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType => System.Net.Mime.MediaTypeNames.Text.Xml;
            private class TravellerPathFinder : PathFinder.Map<World>
            {
                ResourceManager manager;
                SectorMap.Milieu map;

                public int Jump { get; set; }
                public bool RequireWildernessRefuelling { get; set; }
                public bool AvoidRedZones { get; set; }
                public bool ImperialWorldsOnly { get; set; }

                public TravellerPathFinder(ResourceManager manager, SectorMap.Milieu map, int jump)
                {
                    this.manager = manager;
                    this.map = map;
                    Jump = jump;
                }

                private World start, end;

                public List<World> FindPath(World start, World end)
                {
                    this.start = start;
                    this.end = end;
                    return PathFinder.FindPath<World>(this, start, end);
                }

                IEnumerable<World> PathFinder.Map<World>.Adjacent(World world)
                {
                    if (world == null) throw new ArgumentNullException(nameof(world));
                    foreach (World w in new HexSelector(map, manager, Astrometrics.CoordinatesToLocation(world.Coordinates), Jump).Worlds)
                    {
                        // Exclude destination from filters.
                        if (w != end)
                        {
                            if (RequireWildernessRefuelling && (w.GasGiants == 0 && !w.WaterPresent)) continue;
                            if (AvoidRedZones && w.IsRed) continue;
                            if (ImperialWorldsOnly && !SecondSurvey.IsDefaultAllegiance(w.Allegiance)) continue;
                        }

                        yield return w;
                    }
                }

                int PathFinder.Map<World>.Distance(World a, World b)
                {
                    if (a == null) throw new ArgumentNullException(nameof(a));
                    if (b == null) throw new ArgumentNullException(nameof(b));
                    return Astrometrics.HexDistance(a.Coordinates, b.Coordinates);
                }
            }

            private World ResolveLocation(HttpContext context, string field, ResourceManager manager, SectorMap.Milieu map)
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
                    WorldLocation loc = SearchEngine.FindNearestWorldMatch(query, GetStringOption("milieu"), x, y) ??
                        throw new HttpError(404, "Not Found", $"Location not found: {query}");

                    loc.Resolve(map, manager, out Sector loc_sector, out World loc_world);
                    return loc_world;
                }

                string name = match.Groups["sector"].Value;
                Sector sector = map.FromName(name) ??
                    throw new HttpError(404, "Not Found", $"Sector not found: {name}");

                string hexString = match.Groups["hex"].Value;
                Hex hex = new Hex(hexString);
                if (!hex.IsValid)
                    throw new HttpError(400, "Not Found", $"Invalid hex: {hexString}");

                World world = sector.GetWorlds(manager)[hex.ToInt()] ??
                    throw new HttpError(404, "Not Found", $"No such world: {sector.Names[0].Text} {hexString}");

                return world;
            }

            public override void Process(ResourceManager resourceManager)
            {
                SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, GetStringOption("milieu"));

                World startWorld = ResolveLocation(Context, "start", resourceManager, map);
                if (startWorld == null)
                    return;

                World endWorld = ResolveLocation(Context, "end", resourceManager, map);
                if (endWorld == null)
                    return;

                int jump = Util.Clamp(GetIntOption("jump", 2), 0, 12);

                var finder = new TravellerPathFinder(resourceManager, map, jump)
                {
                    RequireWildernessRefuelling = GetBoolOption("wild", false),
                    ImperialWorldsOnly = GetBoolOption("im", false),
                    AvoidRedZones = GetBoolOption("nored", false)
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
}
