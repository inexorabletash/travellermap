using System.Drawing;
using System.Web;
using System.Xml.Serialization;

namespace Maps.API
{
    internal class CoordinatesHandler : DataHandlerBase
    {
        protected override string ServiceName => "coordinates";
        protected override DataResponder GetResponder(HttpContext context)
        {
            return new Responder(context);
        }

        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType => System.Net.Mime.MediaTypeNames.Text.Xml;
            public override void Process()
            {
                // NOTE: This (re)initializes a static data structure used for 
                // resolving names into sector locations, so needs to be run
                // before any other objects (e.g. Worlds) are loaded.
                ResourceManager resourceManager = new ResourceManager(Context.Server);
                SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, GetStringOption("milieu"));

                Location loc = Location.Empty;

                // Accept either sector [& hex] or sx,sy [& hx,hy] or x,y
                if (HasOption("sector"))
                {
                    string sectorName = GetStringOption("sector");
                    Sector sector = map.FromName(sectorName) ??
                        throw new HttpError(404, "Not Found", $"The specified sector '{sectorName}' was not found.");

                    if (HasOption("subsector"))
                    {
                        string subsector = GetStringOption("subsector");
                        int index = sector.SubsectorIndexFor(subsector);
                        if (index == -1)
                            throw new HttpError(404, "Not Found", $"The specified subsector '{subsector}' was not found.");
                        loc = new Location(sector.Location, new Hex(
                            (byte)(index % 4 * Astrometrics.SubsectorWidth + Astrometrics.SubsectorWidth / 2),
                            (byte)(index / 4 * Astrometrics.SubsectorHeight + Astrometrics.SubsectorHeight / 2)));
                    }
                    else
                    {
                        int hex = GetIntOption("hex", 0);
                        loc = new Location(sector.Location, hex);
                    }
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

                SendResult(Context, new Results.CoordinatesResult()
                {
                    sx = loc.Sector.X,
                    sy = loc.Sector.Y,
                    hx = loc.Hex.X,
                    hy = loc.Hex.Y,
                    x = coords.X,
                    y = coords.Y
                });
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
#pragma warning disable IDE1006 // Naming Styles
        public int sx { get; set; }
        public int sy { get; set; }
        public int hx { get; set; }
        public int hy { get; set; }

        // World-space X/Y
        public int x { get; set; }
        public int y { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }
}