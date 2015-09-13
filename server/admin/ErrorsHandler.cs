using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;

namespace Maps.Admin
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    internal class ErrorsHandler : AdminHandlerBase
    {
        public override string DefaultContentType { get { return MediaTypeNames.Text.Plain; } }

        protected override void Process(System.Web.HttpContext context)
        {
            context.Response.ContentType = MediaTypeNames.Text.Plain;
            context.Response.BufferOutput = false;

            ResourceManager resourceManager = new ResourceManager(context.Server, context.Cache);

            string sectorName = GetStringOption(context, "sector");
            string type = GetStringOption(context, "type");

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap.Flush();
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            var sectorQuery = from sector in map.Sectors
                              where (sectorName == null || sector.Names[0].Text.StartsWith(sectorName, ignoreCase: true, culture: CultureInfo.InvariantCulture))
                              && (sector.DataFile != null)
                              && (type == null || sector.DataFile.Type == type)
                              && (sector.Tags.Contains("OTU") || sector.Tags.Contains("Apocryphal") || sector.Tags.Contains("Faraway"))
                              orderby sector.Names[0].Text
                              select sector;

            foreach (var sector in sectorQuery)
            {
                context.Response.Output.WriteLine(sector.Names[0].Text);
#if DEBUG
                WorldCollection worlds = sector.GetWorlds(resourceManager, cacheResults: false);

                if (worlds != null)
                {
                    context.Response.Output.WriteLine("{0} world(s)", worlds.Count());
                    worlds.ErrorList.Report(context.Response.Output);
                }
                else
                {
                    context.Response.Output.WriteLine("{0} world(s)", 0);
                }

                foreach (var route in sector.Routes)
                {
                    System.Drawing.Point startSector = sector.Location, endSector = sector.Location;
                    startSector.Offset(route.StartOffset);
                    endSector.Offset(route.EndOffset);

                    Location startLocation = new Location(startSector, route.Start);
                    Location endLocation = new Location(endSector, route.End);
                    int distance = Astrometrics.HexDistance(Astrometrics.LocationToCoordinates(startLocation),
                        Astrometrics.LocationToCoordinates(endLocation));
                    if (distance > 4)
                        context.Response.Output.WriteLine("Route length {0}: {1}", distance, route.ToString());
                    /*
                     * This fails because of routes that use e.g. 3341-style coordinates
                     * It will also be extremely slow due to loading world lists w/o caching
                                        {
                                            var w = map.FromLocation(startSector).GetWorlds(resourceManager, cacheResults: false);
                                            if (w != null)
                                            {
                                                if (w[route.StartPoint.X, route.StartPoint.Y] == null)
                                                    context.Response.Output.WriteLine("Route start empty hex: {0}", route.ToString());
                                            }
                                        }
                                        {
                                            var w = map.FromLocation(endSector).GetWorlds(resourceManager, cacheResults: false);
                                            if (w != null)
                                            {
                                                if (w[route.EndPoint.X, route.EndPoint.Y] == null)
                                                    context.Response.Output.WriteLine("Route end empty hex: {0}", route.ToString());
                                            }
                                        }
                                        */
                }
#endif
                context.Response.Output.WriteLine();
            }
            return;
        }
    }
}
