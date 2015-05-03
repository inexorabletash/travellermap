using System;
using System.Web;
using Maps.Serialization;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace Maps.API
{
    /// <summary>
    /// Fetch metadata about sector.
    /// </summary>
    public class SectorMetaDataHandler : DataHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }
        protected override string ServiceName { get { return "sectormetadata"; } }

        public override void Process(HttpContext context)
        {
            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            ResourceManager resourceManager = new ResourceManager(context.Server, context.Cache);
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);
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
            else if (HasOption(context, "sx") && HasOption(context, "sy"))
            {
                int sx = GetIntOption(context, "sx", 0);
                int sy = GetIntOption(context, "sy", 0);

                sector = map.FromLocation(sx, sy);

                if (sector == null)
                {
                    SendError(context.Response, 404, "Not Found", String.Format("The sector at {0},{1} was not found.", sx, sy));
                    return;
                }
            }
            else if (HasOption(context, "sector"))
            {
                string sectorName = GetStringOption(context, "sector");
                sector = map.FromName(sectorName);

                if (sector == null)
                {
                    SendError(context.Response, 404, "Not Found", String.Format("The specified sector '{0}' was not found.", sectorName));
                    return;
                }
            }
            else
            {
                SendError(context.Response, 400, "Bad Request", "No sector specified.");
                return;
            }

            WorldCollection worlds = sector.GetWorlds(resourceManager, cacheResults: true);
            SendResult(context, new SectorMetadata(sector, worlds));
        }

        [XmlRoot("Sector")]
        public class SectorMetadata
        {
            private SectorMetadata() { }

            public SectorMetadata(Sector sector, WorldCollection worlds)
            {
                this.m_sector = sector;
                this.m_worlds = worlds;
                this.m_dataFile = new DataFileMetadata(sector);
            }
            private Sector m_sector;
            private WorldCollection m_worlds;
            private DataFileMetadata m_dataFile;

            [XmlAttribute]
            [System.ComponentModel.DefaultValue(false)]
            public bool Selected { get { return m_sector.Selected; } }

            [XmlAttribute]
            public string Tags { get { return m_sector.TagString; } }

            [XmlAttribute]
            public string Abbreviation { get { return m_sector.Abbreviation; } }

            [XmlElement("Name")]
            public List<Name> Names { get { return m_sector.Names; } }

            public string Credits { get { return m_sector.Credits; } set { } }
            public DataFileMetadata DataFile { get { return m_dataFile; } }

            public int X { get { return m_sector.X; } }
            public int Y { get { return m_sector.Y; } }

            [XmlElement("Product")]
            public MetadataCollection<Product> Products { get { return m_sector.Products; } }

            public MetadataCollection<Subsector> Subsectors { get { return m_sector.Subsectors; } }

            public List<Allegiance> Allegiances
            {
                get
                {
                    if (m_worlds == null)
                        return null;

                    // Ensure the allegiance list documents the codes as used by the worlds
                    var list = new List<Allegiance>();
                    foreach (var code in m_worlds.AllegianceCodes())
                    {
                        var alleg = m_sector.GetAllegianceFromCode(code);
                        if (alleg == null)
                            continue;
                        list.Add(new Allegiance(code, alleg.Name));
                    }
                    return list;
                }
            }

            public string Stylesheet { get { return m_sector.StylesheetText; } set { } }

            public MetadataCollection<Label> Labels { get { return m_sector.Labels; } }
            public bool ShouldSerializeLabels() { return m_sector.Labels.Count > 0; }

            public MetadataCollection<Border> Borders { get { return m_sector.Borders; } }
            public bool ShouldSerializeBorders() { return m_sector.Borders.Count > 0;  }

            public MetadataCollection<Route> Routes { get { return m_sector.Routes; } }
            public bool ShouldSerializeRoutes() { return m_sector.Routes.Count > 0;  }
        }

        [XmlRoot("DataFile")]
        public class DataFileMetadata
        {
            private DataFileMetadata() { }

            public DataFileMetadata(Sector sector)
            {
                this.m_sector = sector;
            }
            private Sector m_sector;

            [XmlAttribute]
            public string Title { get { return (m_sector.DataFile != null ? m_sector.DataFile.Title : null) ?? m_sector.Title; } }

            [XmlAttribute]
            public string Author { get { return (m_sector.DataFile != null ? m_sector.DataFile.Author : null) ?? m_sector.Author; } }

            [XmlAttribute]
            public string Source { get { return (m_sector.DataFile != null ? m_sector.DataFile.Source : null) ?? m_sector.Source; } }

            [XmlAttribute]
            public string Publisher { get { return (m_sector.DataFile != null ? m_sector.DataFile.Publisher : null) ?? m_sector.Publisher; } }

            [XmlAttribute]
            public string Copyright { get { return (m_sector.DataFile != null ? m_sector.DataFile.Copyright : null) ?? m_sector.Copyright; } }

            [XmlAttribute]
            public string Era { get { return (m_sector.DataFile != null ? m_sector.DataFile.Era : null) ?? m_sector.Era; } }

            [XmlAttribute]
            public string Ref { get { return (m_sector.DataFile != null ? m_sector.DataFile.Ref : null) ?? m_sector.Ref; } }
        }
    }
}
