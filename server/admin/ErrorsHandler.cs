using Maps.Utilities;
using System.Globalization;
using System.Linq;

namespace Maps.Admin
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    internal class ErrorsHandler : AdminHandlerBase
    {
        protected override void Process(System.Web.HttpContext context, ResourceManager resourceManager)
        {
            context.Response.ContentType = ContentTypes.Text.Plain;
            context.Response.BufferOutput = false;

            string sectorName = GetStringOption(context, "sector");
            string type = GetStringOption(context, "type");
            string milieu = GetStringOption(context, "milieu");
            string tag = GetStringOption(context, "tag");
            bool hide_tl = GetBoolOption(context, "hide-tl");
            bool hide_gov = GetBoolOption(context, "hide-gov");
            ErrorLogger.Severity severity = GetBoolOption(context, "warnings", true) ? 0 : ErrorLogger.Severity.Error;

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap.Flush();
            SectorMap map = SectorMap.GetInstance(resourceManager);
            
            var sectorQuery = from sector in map.Sectors
                              where (sectorName == null || sector.Names[0].Text.StartsWith(sectorName, ignoreCase: true, culture: CultureInfo.InvariantCulture))
                              && (sector.DataFile != null)
                              && (type == null || sector.DataFile.Type == type)
                              && (milieu == null || sector.CanonicalMilieu == milieu)
                              && (tag == null || sector.Tags.Contains(tag))
                              && (sector.Tags.Contains("OTU") || sector.Tags.Contains("Apocryphal") || sector.Tags.Contains("Faraway"))
                              orderby sector.Names[0].Text
                              select sector;

            foreach (var sector in sectorQuery)
            {
                context.Response.Output.WriteLine($"{sector.Names[0].Text} - {sector.Milieu}");
#if DEBUG
                int error_count = 0;
                int warning_count = 0;

                WorldCollection worlds = sector.GetWorlds(resourceManager, cacheResults: false);

                if (worlds != null)
                {
                    double pop = worlds.Select(w => w.Population).Sum();
                    if (pop > 0)
                        context.Response.Output.WriteLine($"{worlds.Count()} world(s) - population: {pop / 1e9:#,###.##} billion");
                    else
                        context.Response.Output.WriteLine($"{worlds.Count()} world(s) - population: N/A");
                    worlds.ErrorList.Report(context.Response.Output, severity, (ErrorLogger.Record record) =>
                    {
                        if (hide_gov && (record.message.StartsWith("UWP: Gov") || record.message.StartsWith("Gov"))) return false;
                        if (hide_tl && record.message.StartsWith("UWP: TL")) return false;
                        return true;
                    });
                    error_count += worlds.ErrorList.CountOf(ErrorLogger.Severity.Error);
                    warning_count += worlds.ErrorList.CountOf(ErrorLogger.Severity.Warning);
                }
                else
                {
                    context.Response.Output.WriteLine("0 world(s)");
                }

                foreach (IAllegiance item in sector.Borders.AsEnumerable<IAllegiance>()
                    .Concat(sector.Routes.AsEnumerable<IAllegiance>())
                    .Concat(sector.Labels.AsEnumerable<IAllegiance>()))
                {
                    if (string.IsNullOrWhiteSpace(item.Allegiance))
                        continue;
                    if (sector.GetAllegianceFromCode(item.Allegiance) == null)
                        context.Response.Output.WriteLine($"Undefined allegiance code: {item.Allegiance} (on {item.GetType().Name})");
                }

                foreach (var route in sector.Routes)
                {
                    System.Drawing.Point startSector = sector.Location, endSector = sector.Location;
                    startSector.Offset(route.StartOffset);
                    endSector.Offset(route.EndOffset);

                    int distance = Astrometrics.HexDistance(
                        Astrometrics.LocationToCoordinates(new Location(startSector, route.Start)),
                        Astrometrics.LocationToCoordinates(new Location(endSector, route.End)));
                    if (distance == 0)
                    {
                        context.Response.Output.WriteLine($"Error: Route length {distance}: {route.ToString()}");
                        ++error_count;
                    }
                    else if (distance > 4)
                    {
                        if (severity <= ErrorLogger.Severity.Warning)
                            context.Response.Output.WriteLine($"Warning: Route length {distance}: {route.ToString()}");
                        ++warning_count;
                    }
                    /*
                     * This fails because of routes that use e.g. 3341-style coordinates
                     * It will also be extremely slow due to loading world lists w/o caching
                                        {
                                            var w = map.FromLocation(startSector).GetWorlds(resourceManager, cacheResults: false);
                                            if (w != null)
                                            {
                                                if (w[route.StartPoint.X, route.StartPoint.Y] == null)
                                                    context.Response.Output.WriteLine($"Route start empty hex: {route.ToString()}");
                                            }
                                        }
                                        {
                                            var w = map.FromLocation(endSector).GetWorlds(resourceManager, cacheResults: false);
                                            if (w != null)
                                            {
                                                if (w[route.EndPoint.X, route.EndPoint.Y] == null)
                                                    context.Response.Output.WriteLine($"Route end empty hex: {route.ToString()}");
                                            }
                                        }
                                        */
                }
                context.Response.Output.WriteLine($"{error_count} errors, {warning_count} warnings.");
#endif
                context.Response.Output.WriteLine();
            }
            return;
        }
    }
}
