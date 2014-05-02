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
                SendError(context.Response, 404, "Not Found", "No sector specified.");
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
            }
            private Sector m_sector;
            private WorldCollection m_worlds;

            [XmlAttribute]
            [System.ComponentModel.DefaultValue(false)]
            public bool Selected { get { return m_sector.Selected; } }

            [XmlAttribute]
            public string Tags { get { return m_sector.TagString; } }

            [XmlAttribute]
            public string Abbreviation { get { return m_sector.Abbreviation; } }

            [XmlElement("Name")]
            public List<Name> Names { get { return m_sector.Names; } }

            public string Credits { get { return m_sector.Credits; } }

            public int X { get { return m_sector.X; } }
            public int Y { get { return m_sector.Y; } }

            [XmlElement("Product")]
            public MetadataCollection<Product> Products { get { return m_sector.Products; } }

            public MetadataCollection<Subsector> Subsectors { get { return m_sector.Subsectors; } }

            public List<Allegiance> Allegiances
            {
                get
                {
                    return m_worlds.AllegianceCodes().Select(code => m_sector.GetAllegiance(code)).Where(code => code != null).ToList();
                }
            }

            public MetadataCollection<Label> Labels { get { return m_sector.Labels; } }
            public MetadataCollection<Border> Borders { get { return m_sector.Borders; } }
            public MetadataCollection<Route> Routes { get { return m_sector.Routes; } }
        }
    }
}
