using System;
using System.Drawing;
using System.Xml.Serialization;

namespace Maps.API
{
    internal class CoordinatesHandler : DataHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }
        protected override string ServiceName { get { return "coordinates"; } }

        public override void Process(System.Web.HttpContext context)
        {
            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            ResourceManager resourceManager = new ResourceManager(context.Server);
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            Location loc = new Location(map.FromName("Spinward Marches").Location, 1910);

            // Accept either sector [& hex] or sx,sy [& hx,hy] or x,y
            if (HasOption(context, "sector"))
            {
                string sectorName = GetStringOption(context, "sector");
                Sector sector = map.FromName(sectorName);
                if (sector == null)
                {
                    SendError(context.Response, 404, "Not Found", string.Format("The specified sector '{0}' was not found.", sectorName));
                    return;
                }

                int hex = GetIntOption(context, "hex", 0);
                loc = new Location(sector.Location, hex);
            }
            else if (HasLocation(context))
            {
                loc = GetLocation(context);
            }
            else
            {
                SendError(context.Response, 400, "Bad Request", "Must specify either sector name (and optional hex) or sx, sy (and optional hx, hy), or x, y (world-space coordinates).");
                return;
            }

            Point coords = Astrometrics.LocationToCoordinates(loc);

            CoordinatesResult result = new CoordinatesResult();
            result.sx = loc.Sector.X;
            result.sy = loc.Sector.Y;
            result.hx = loc.Hex.X;
            result.hy = loc.Hex.Y;
            result.x = coords.X;
            result.y = coords.Y;
            SendResult(context, result);
        }
    }

    [XmlRoot(ElementName = "Coordinates")]
    // public for XML serialization
    public class CoordinatesResult
    {
        // Sector/Hex
        public int sx { get; set; }
        public int sy { get; set; }
        public int hx { get; set; }
        public int hy { get; set; }

        // World-space X/Y
        public int x { get; set; }
        public int y { get; set; }
    }
}