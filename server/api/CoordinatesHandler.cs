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
            ResourceManager resourceManager = new ResourceManager(context.Server, context.Cache);
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
            else if (HasOption(context, "sx") && HasOption(context, "sy"))
            {
                int sx = GetIntOption(context, "sx", 0);
                int sy = GetIntOption(context, "sy", 0);
                byte hx = (byte)GetIntOption(context, "hx", 0);
                byte hy = (byte)GetIntOption(context, "hy", 0);
                loc = new Location(map.FromLocation(sx, sy).Location, new Hex(hx, hy));
            }
            else if (HasOption(context, "x") && HasOption(context, "y"))
            {
                int x = GetIntOption(context, "x", 0);
                int y = GetIntOption(context, "y", 0);
                loc = Astrometrics.CoordinatesToLocation(x, y);
            }
            else
            {
                SendError(context.Response, 400, "Bad Request", "Must specify either sector name (and optional hex) or sx, sy (and optional hx, hy), or x, y (world-space coordinates).");
                return;
            }

            Point coords = Astrometrics.LocationToCoordinates(loc);

            CoordinatesResult result = new CoordinatesResult();
            result.sx = loc.SectorLocation.X;
            result.sy = loc.SectorLocation.Y;
            result.hx = loc.HexLocation.X;
            result.hy = loc.HexLocation.Y;
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