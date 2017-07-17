using Maps.Serialization;
using Maps.Utilities;
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
                Sector sector;

                if (Context.Request.HttpMethod == "POST")
                {
                    var type = SectorMetadataFileParser.SniffType(Context.Request.InputStream);
                    var parser = SectorMetadataFileParser.ForType(type);
                    using (var reader = new System.IO.StreamReader(Context.Request.InputStream, Context.Request.ContentEncoding))
                    {
                        sector = parser.Parse(reader);
                    }
                    sector.MetadataFile = null;
                    sector.DataFile = null;
                }
                else if (HasOption("sx") && HasOption("sy"))
                {
                    int sx = GetIntOption("sx", 0);
                    int sy = GetIntOption("sy", 0);

                    sector = map.FromLocation(sx, sy) ??
                        throw new HttpError(404, "Not Found", $"The sector at {sx},{sy} was not found.");
                }
                else if (HasOption("sector"))
                {
                    string sectorName = GetStringOption("sector");
                    sector = map.FromName(sectorName) ??
                        throw new HttpError(404, "Not Found", $"The specified sector '{sectorName}' was not found.");
                }
                else
                {
                    throw new HttpError(400, "Bad Request", "No sector specified.");
                }

                SendResult(new Results.SectorMetadata(sector, sector.GetWorlds(resourceManager, cacheResults: true)));
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
        public bool Selected { get => sector.Selected; set { } }
        [XmlAttribute]
        public string Tags { get => sector.TagString; set { } }
        [XmlAttribute]
        public string Abbreviation { get => sector.Abbreviation; set { } }
        [XmlElement("Name")]
        public List<Name> Names => sector.Names;
        public string Credits { get => sector.Credits; set { } }

        public int X { get => sector.X; set { } }
        public int Y { get => sector.Y; set { } }

        [XmlElement("Product")]
        public MetadataCollection<Product> Products => sector.Products;
        public MetadataCollection<Subsector> Subsectors => sector.Subsectors;
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

        public string Stylesheet { get => sector.StylesheetText; set { } }

        public MetadataCollection<Label> Labels => sector.Labels;
        public bool ShouldSerializeLabels() => sector.Labels.Count > 0;

        public MetadataCollection<Border> Borders => sector.Borders;
        public bool ShouldSerializeBorders() => sector.Borders.Count > 0;

        public MetadataCollection<Region> Regions => sector.Regions;
        public bool ShouldSerializeRegions() => sector.Regions.Count > 0;

        public MetadataCollection<Route> Routes => sector.Routes;
        public bool ShouldSerializeRoutes() => sector.Routes.Count > 0;

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
            public string Title { get => sector.DataFile?.Title ?? sector.Title; set { } }
            [XmlAttribute]
            public string Author { get => sector.DataFile?.Author ?? sector.Author; set { } }
            [XmlAttribute]
            public string Source { get => sector.DataFile?.Source ?? sector.Source; set { } }
            [XmlAttribute]
            public string Publisher { get => sector.DataFile?.Publisher ?? sector.Publisher; set { } }
            [XmlAttribute]
            public string Copyright { get => sector.DataFile?.Copyright ?? sector.Copyright; set { } }
            [XmlAttribute]
            public string Milieu { get => sector.CanonicalMilieu; set { } }
            [XmlAttribute]
            public string Ref { get => sector.DataFile?.Ref ?? sector.Ref; set { } }
        }
    }

}
