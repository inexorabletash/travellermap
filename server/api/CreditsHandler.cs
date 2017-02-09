using System.Linq;
using System.Web;
using System.Xml.Serialization;

namespace Maps.API
{
    internal class CreditsHandler : DataHandlerBase
    {
        protected override string ServiceName { get { return "credits"; } }

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
                ResourceManager resourceManager = new ResourceManager(Context.Server);

                // NOTE: This (re)initializes a static data structure used for 
                // resolving names into sector locations, so needs to be run
                // before any other objects (e.g. Worlds) are loaded.
                SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, GetStringOption("milieu"));
                Location loc = Location.Empty;

                if (HasOption("sector"))
                {
                    string sectorName = GetStringOption("sector");
                    Sector sec = map.FromName(sectorName);
                    if (sec == null)
                        throw new HttpError(404, "Not Found", $"The specified sector '{sectorName}' was not found.");

                    int hex = GetIntOption("hex", Astrometrics.SectorCentralHex);
                    loc = new Location(sec.Location, hex);
                }
                else if (HasLocation())
                {
                    loc = GetLocation();
                }

                if (loc.Hex.IsEmpty)
                    loc.Hex = Astrometrics.SectorCenter;

                Sector sector = map.FromLocation(loc.Sector.X, loc.Sector.Y);

                var data = new Results.CreditsResult();

                if (sector != null)
                {
                    // TODO: Multiple names
                    foreach (var name in sector.Names.Take(1))
                        data.SectorName = name.Text;

                    // Raw HTML credits
                    data.Credits = sector.Credits?.Trim();

                    // Product info
                    if (sector.Products.Count > 0)
                    {
                        data.ProductPublisher = sector.Products[0].Publisher;
                        data.ProductTitle = sector.Products[0].Title;
                        data.ProductAuthor = sector.Products[0].Author;
                        data.ProductRef = sector.Products[0].Ref;
                    }

                    // Tags
                    data.SectorTags = sector.TagString;

                    //
                    // Sector Credits
                    //
                    data.SectorAuthor = sector.Author;
                    data.SectorSource = sector.Source;
                    data.SectorPublisher = sector.Publisher;
                    data.SectorCopyright = sector.Copyright;
                    data.SectorRef = sector.Ref;
                    data.SectorMilieu = sector.CanonicalMilieu;

                    if (sector.DataFile != null)
                    {
                        data.SectorAuthor = sector.DataFile.Author ?? data.SectorAuthor;
                        data.SectorSource = sector.DataFile.Source ?? data.SectorSource;
                        data.SectorPublisher = sector.DataFile.Publisher ?? data.SectorPublisher;
                        data.SectorCopyright = sector.DataFile.Copyright ?? data.SectorCopyright;
                        data.SectorRef = sector.DataFile.Ref ?? data.SectorRef;
                        data.SectorMilieu = sector.CanonicalMilieu;
                    }

                    //
                    // Subsector Credits
                    //
                    int ssx = (loc.Hex.X - 1) / Astrometrics.SubsectorWidth;
                    int ssy = (loc.Hex.Y - 1) / Astrometrics.SubsectorHeight;
                    int ssi = ssx + ssy * 4;
                    Subsector ss = sector.Subsector(ssi);
                    if (ss != null)
                    {
                        data.SubsectorIndex = ss.Index;
                        data.SubsectorName = ss.Name;
                    }

                    //
                    // Routes Credits
                    //


                    //
                    // World Data
                    // 
                    WorldCollection worlds = sector.GetWorlds(resourceManager);
                    if (worlds != null)
                    {
                        World world = worlds[loc.Hex];
                        if (world != null)
                        {
                            data.WorldName = world.Name;
                            data.WorldHex = world.Hex;
                            data.WorldUwp = world.UWP;
                            data.WorldRemarks = world.Remarks;
                            data.WorldIx = world.Importance;
                            data.WorldEx = world.Economic;
                            data.WorldCx = world.Cultural;
                            data.WorldPbg = world.PBG;
                            data.WorldAllegiance = sector.GetAllegianceFromCode(world.Allegiance).T5Code;
                        }
                    }
                }

                SendResult(Context, data);
            }
        }
    }
}

namespace Maps.API.Results
{
    [XmlRoot(ElementName = "Data")]
    // public for XML serialization
    public class CreditsResult
    {
        public string Credits { get; set; }

        public string SectorName { get; set; }
        public string SectorAuthor { get; set; }
        public string SectorSource { get; set; }
        public string SectorPublisher { get; set; }
        public string SectorCopyright { get; set; }
        public string SectorRef { get; set; }
        public string SectorMilieu { get; set; }
        public string SectorTags { get; set; }

        public string RouteCredits { get; set; }

        public string SubsectorName { get; set; }
        public string SubsectorIndex { get; set; }
        public string SubsectorCredits { get; set; }

        public string WorldName { get; set; }
        public string WorldHex { get; set; }
        public string WorldUwp { get; set; }
        public string WorldRemarks { get; set; }
        public string WorldIx { get; set; }
        public string WorldEx { get; set; }
        public string WorldCx { get; set; }
        public string WorldPbg { get; set; }
        public string WorldAllegiance { get; set; }

        public string WorldCredits { get; set; }

        public string ProductPublisher { get; set; }
        public string ProductTitle { get; set; }
        public string ProductAuthor { get; set; }
        public string ProductRef { get; set; }
    }
}
