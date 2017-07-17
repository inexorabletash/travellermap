using Json;
using Maps.Utilities;
using System.Drawing;
using System.Web;
using System.Xml.Serialization;

namespace Maps.API
{
    internal class CoordinatesHandler : DataHandlerBase
    {
        protected override DataResponder GetResponder(HttpContext context) => new Responder(context);

        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType => ContentTypes.Text.Xml;
            public override void Process(ResourceManager resourceManager)
            {
                // NOTE: This (re)initializes a static data structure used for 
                // resolving names into sector locations, so needs to be run
                // before any other objects (e.g. Worlds) are loaded.
                SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, GetStringOption("milieu"));

                Location loc;

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

                SendResult(new Results.CoordinatesResult()
                {
                    SectorX = loc.Sector.X,
                    SectorY = loc.Sector.Y,
                    HexX = loc.Hex.X,
                    HexY = loc.Hex.Y,
                    X = coords.X,
                    Y = coords.Y
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
        [XmlElement("sx"), JsonName("sx")]
        public int SectorX { get; set; }
        [XmlElement("sy"), JsonName("sy")]
        public int SectorY { get; set; }
        [XmlElement("hx"), JsonName("hx")]
        public int HexX { get; set; }
        [XmlElement("hy"), JsonName("hy")]
        public int HexY { get; set; }

        // World-space X/Y
        [XmlElement("x"), JsonName("x")]
        public int X { get; set; }
        [XmlElement("y"), JsonName("y")]
        public int Y { get; set; }
    }
}
