using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;

namespace Maps.Pages
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    public class ErrorsHandler : AdminBase
    {
        public override string DefaultContentType { get { return MediaTypeNames.Text.Plain; } }

        private static readonly Regex candidate = new Regex(@"(\w\w\w\w\w\w\w-\w|Error) \b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

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
                              && (sector.Tags.Contains("OTU"))
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
                    foreach (string s in worlds.ErrorList.Where(s => candidate.IsMatch(s)))
                    {
                        context.Response.Output.WriteLine(s);
                    }
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
                    {
                        context.Response.Output.WriteLine("Route length {0}: {1}", distance, route.ToString());
                    }
                    /*
                     * This fails because of routes that use e.g. 3341-style coordinates
                     * It will also be extremely slow due to loading world lists w/o caching
                                        {
                                            var w = map.FromLocation(startSector).GetWorlds(resourceManager, cacheResults: false);
                                            if (w != null)
                                            {
                                                if (w[route.StartPoint.X, route.StartPoint.Y] == null)
                                                {
                                                    context.Response.Output.WriteLine("Route start empty hex: {0}", route.ToString());
                                                }
                                            }
                                        }
                                        {
                                            var w = map.FromLocation(endSector).GetWorlds(resourceManager, cacheResults: false);
                                            if (w != null)
                                            {
                                                if (w[route.EndPoint.X, route.EndPoint.Y] == null)
                                                {
                                                    context.Response.Output.WriteLine("Route end empty hex: {0}", route.ToString());
                                                }
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
