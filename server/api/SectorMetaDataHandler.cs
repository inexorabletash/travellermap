using Maps.Serialization;
using System.Collections.Generic;
using System.Web;
using System.Xml.Serialization;

namespace Maps.API
{
    /// <summary>
    /// Fetch metadata about sector.
    /// </summary>
    internal class SectorMetaDataHandler : DataHandlerBase
    {
        protected override string ServiceName { get { return "sectormetadata"; } }

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
                ResourceManager resourceManager = new ResourceManager(context.Server);
                SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, GetStringOption("milieu"));
                Sector sector;

                if (context.Request.HttpMethod == "POST")
                {
                    var type = SectorMetadataFileParser.SniffType(context.Request.InputStream);
                    var parser = SectorMetadataFileParser.ForType(type);
                    using (var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                    {
                        sector = parser.Parse(reader);
                    }
                }
                else if (HasOption("sx") && HasOption("sy"))
                {
                    int sx = GetIntOption("sx", 0);
                    int sy = GetIntOption("sy", 0);

                    sector = map.FromLocation(sx, sy);

                    if (sector == null)
                        throw new HttpError(404, "Not Found", string.Format("The sector at {0},{1} was not found.", sx, sy));
                }
                else if (HasOption("sector"))
                {
                    string sectorName = GetStringOption("sector");
                    sector = map.FromName(sectorName);

                    if (sector == null)
                        throw new HttpError(404, "Not Found", string.Format("The specified sector '{0}' was not found.", sectorName));
                }
                else
                {
                    throw new HttpError(400, "Bad Request", "No sector specified.");
                }

                SendResult(context, new Results.SectorMetadata(sector, sector.GetWorlds(resourceManager, cacheResults: true)));
            }
        }
    }
}

namespace Maps.API.Results
{
    [XmlRoot("Sector")]
    public class SectorMetadata
    {
        private SectorMetadata() { }

        internal SectorMetadata(Sector sector, WorldCollection worlds)
        {
            this.sector = sector;
            this.worlds = worlds;
            dataFile = new DataFileMetadata(sector);
        }
        private Sector sector;
        private WorldCollection worlds;
        private DataFileMetadata dataFile;

        [XmlAttribute]
        [System.ComponentModel.DefaultValue(false)]
        public bool Selected { get { return sector.Selected; } }

        [XmlAttribute]
        public string Tags { get { return sector.TagString; } }

        [XmlAttribute]
        public string Abbreviation { get { return sector.Abbreviation; } }

        [XmlElement("Name")]
        public List<Name> Names { get { return sector.Names; } }

        public string Credits { get { return sector.Credits; } set { } }
        public DataFileMetadata DataFile { get { return dataFile; } }

        public int X { get { return sector.X; } }
        public int Y { get { return sector.Y; } }

        [XmlElement("Product")]
        public MetadataCollection<Product> Products { get { return sector.Products; } }

        public MetadataCollection<Subsector> Subsectors { get { return sector.Subsectors; } }

        public List<Allegiance> Allegiances
        {
            get
            {
                if (worlds == null)
                    return null;

                // Ensure the allegiance list documents the codes as used by the worlds
                var list = new List<Allegiance>();
                foreach (var code in worlds.AllegianceCodes())
                {
                    var alleg = sector.GetAllegianceFromCode(code);
                    if (alleg == null)
                        continue;
                    list.Add(new Allegiance(code, alleg.Name));
                }
                return list;
            }
        }

        public string Stylesheet { get { return sector.StylesheetText; } set { } }

        public MetadataCollection<Label> Labels { get { return sector.Labels; } }
        public bool ShouldSerializeLabels() { return sector.Labels.Count > 0; }

        public MetadataCollection<Border> Borders { get { return sector.Borders; } }
        public bool ShouldSerializeBorders() { return sector.Borders.Count > 0; }

        public MetadataCollection<Route> Routes { get { return sector.Routes; } }
        public bool ShouldSerializeRoutes() { return sector.Routes.Count > 0; }

        [XmlRoot("DataFile")]
        public class DataFileMetadata
        {
            private DataFileMetadata() { }

            public DataFileMetadata(Sector sector)
            {
                this.sector = sector;
            }
            private Sector sector;

            [XmlAttribute]
            public string Title { get { return sector.DataFile?.Title ?? sector.Title; } }

            [XmlAttribute]
            public string Author { get { return sector.DataFile?.Author ?? sector.Author; } }

            [XmlAttribute]
            public string Source { get { return sector.DataFile?.Source ?? sector.Source; } }

            [XmlAttribute]
            public string Publisher { get { return sector.DataFile?.Publisher ?? sector.Publisher; } }

            [XmlAttribute]
            public string Copyright { get { return sector.DataFile?.Copyright ?? sector.Copyright; } }

            [XmlAttribute]
            public string Era { get { return sector.DataFile?.Era ?? sector.Era; } }

            [XmlAttribute]
            public string Ref { get { return sector.DataFile?.Ref ?? sector.Ref; } }
        }
    }

}
