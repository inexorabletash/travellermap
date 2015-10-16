using System.Drawing;
using System.Web;
using System.Xml.Serialization;

namespace Maps.API
{
    internal class CoordinatesHandler : DataHandlerBase
    {
        protected override string ServiceName { get { return "coordinates"; } }

        protected override DataResponder GetResponder(HttpContext context)
        {
            return new Responder(context);
        }

        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }

            public override void Process()
            {
                // NOTE: This (re)initializes a static data structure used for 
                // resolving names into sector locations, so needs to be run
                // before any other objects (e.g. Worlds) are loaded.
                ResourceManager resourceManager = new ResourceManager(Context.Server);
                SectorMap map = SectorMap.GetInstance(resourceManager);

                Location loc = new Location(map.FromName("Spinward Marches").Location, 1910);

                // Accept either sector [& hex] or sx,sy [& hx,hy] or x,y
                if (HasOption("sector"))
                {
                    string sectorName = GetStringOption("sector");
                    Sector sector = map.FromName(sectorName);
                    if (sector == null)
                        throw new HttpError(404, "Not Found", string.Format("The specified sector '{0}' was not found.", sectorName));

                    int hex = GetIntOption("hex", 0);
                    loc = new Location(sector.Location, hex);
                }
                else if (HasLocation())
                {
                    loc = GetLocation();
                }
                else
                {
                    throw new HttpError(400, "Bad Request", "Must specify either sector name (and optional hex) or sx, sy (and optional hx, hy), or x, y (world-space coordinates).");
                }

                Point coords = Astrometrics.LocationToCoordinates(loc);

                var result = new Results.CoordinatesResult();
                result.sx = loc.Sector.X;
                result.sy = loc.Sector.Y;
                result.hx = loc.Hex.X;
                result.hy = loc.Hex.Y;
                result.x = coords.X;
                result.y = coords.Y;
                SendResult(context, result);
            }
        }
    }
}

namespace Maps.API.Results
{
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