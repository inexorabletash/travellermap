using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Xml.Serialization;

namespace Maps.API
{
    internal class CreditsHandler : DataHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }
        protected override string ServiceName { get { return "credits"; } }

        public override void Process(System.Web.HttpContext context)
        {
            ResourceManager resourceManager = new ResourceManager(context.Server);

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);
            Location loc = new Location(map.FromName("Spinward Marches").Location, 1910);

            if (HasOption(context, "sector"))
            {
                string sectorName = GetStringOption(context, "sector");
                Sector sec = map.FromName(sectorName);
                if (sec == null)
                {
                    SendError(context.Response, 404, "Not Found", string.Format("The specified sector '{0}' was not found.", sectorName));
                    return;
                }

                int hex = GetIntOption(context, "hex", Astrometrics.SectorCentralHex);
                loc = new Location(sec.Location, hex);
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
                loc = Astrometrics.CoordinatesToLocation(GetIntOption(context, "x", 0), GetIntOption(context, "y", 0));
            }

            if (loc.HexLocation.IsEmpty)
                loc.HexLocation = Astrometrics.SectorCenter;

            Sector sector = map.FromLocation(loc.SectorLocation.X, loc.SectorLocation.Y);

            CreditsResult data = new CreditsResult();

            if (sector != null)
            {
                // TODO: Multiple names
                foreach (var name in sector.Names.Take(1))
                    data.SectorName = name.Text;
                
                // Raw HTML credits
                data.Credits = sector.Credits == null ? null : sector.Credits.Trim();

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
                data.SectorEra = sector.Era;

                if (sector.DataFile != null)
                {
                    data.SectorAuthor = sector.DataFile.Author ?? data.SectorAuthor;
                    data.SectorSource = sector.DataFile.Source ?? data.SectorSource;
                    data.SectorPublisher = sector.DataFile.Publisher ?? data.SectorPublisher;
                    data.SectorCopyright = sector.DataFile.Copyright ?? data.SectorCopyright;
                    data.SectorRef = sector.DataFile.Ref ?? data.SectorRef;
                    data.SectorEra = sector.DataFile.Era ?? data.SectorEra;
                }

                //
                // Subsector Credits
                //
                int ssx = (loc.HexLocation.X - 1) / Astrometrics.SubsectorWidth;
                int ssy = (loc.HexLocation.Y - 1) / Astrometrics.SubsectorHeight;
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
                    World world = worlds[loc.HexLocation];
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

            SendResult(context, data);
        }
    }

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
        public string SectorEra { get; set; }
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
