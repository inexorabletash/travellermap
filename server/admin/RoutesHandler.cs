#nullable enable
using Maps.Utilities;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using static Maps.API.Results.SearchResults;

namespace Maps.Admin
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    internal class RoutesHandler : AdminHandler
    {
        private class RouteTally
        {
            public int count = 0;
            public int distance = 0;
        }
        private struct RouteKey
        {
            public string allegiance;
            public string type;
        }
        protected override void Process(System.Web.HttpContext context, ResourceManager resourceManager)
        {
            context.Response.ContentType = ContentTypes.Text.Plain;
            context.Response.StatusCode = 200;

            string? type = GetStringOption(context, "type");
            string? allegiance= GetStringOption(context, "regex");
            string? milieu = GetStringOption(context, "milieu");

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap.Flush();
            SectorMap map = SectorMap.GetInstance(resourceManager);

            var sectorQuery = from sector in map.Sectors where
                              (!sector.Tags.Contains("ZCR")) && (!sector.Tags.Contains("meta"))
                              && (milieu == null || sector.CanonicalMilieu == milieu)
                              select sector;


            Dictionary<RouteKey, RouteTally> results = new Dictionary<RouteKey, RouteTally>();

            foreach (var sector in sectorQuery)
            {
                // TODO: Dedupe outsector routes across sectors
                foreach (var route in sector.Routes)
                {
                    string route_type = route.Type ?? "unknown";
                    string route_allegiance = route.Allegiance ?? "unknown";
                    if (type != null && type != route_type) continue;
                    if (allegiance != null && allegiance != route_allegiance) continue;

                    RouteKey key = new RouteKey { type = route_type, allegiance = route_allegiance };
                    RouteTally tally;
                    if (!results.TryGetValue(key, out tally))
                    {
                        tally = new RouteTally();
                        results.Add(key, tally);
                    }

                    sector.RouteToStartEnd(route, out Location start, out Location end);
                    int distance = Astrometrics.HexDistance(
                        Astrometrics.LocationToCoordinates(start), 
                        Astrometrics.LocationToCoordinates(end));

                    tally.count += 1;
                    tally.distance += distance;
                }
            }

            context.Response.Output.WriteLine("Allegiance\tType\tCount\tDistance");
            foreach (var item in results.OrderBy(i => i.Key.allegiance).ThenBy(i => i.Key.type))
            {
                context.Response.Output.WriteLine($"{item.Key.allegiance}\t{item.Key.type}\t{item.Value.count}\t{item.Value.distance}");
            }
        }
    }
}
