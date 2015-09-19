using Maps.API.Results;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;
using System.Xml.Serialization;

namespace Maps.API
{
    /// <summary>
    /// Fetch data about the universe.
    /// </summary>
    internal class UniverseHandler : DataHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }
        protected override string ServiceName { get { return "universe"; } }

        public override void Process(HttpContext context)
        {
            ResourceManager resourceManager = new ResourceManager(context.Server);

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            // Filter parameters
            string era = GetStringOption(context, "era");
            bool requireData = GetBoolOption(context, "requireData", defaultValue: false);
            string[] tags = GetStringsOption(context, "tag");

            UniverseResult data = new UniverseResult();
            foreach (Sector sector in map.Sectors)
            {
                if (requireData && sector.DataFile == null)
                    continue;

                if (era != null && (sector.DataFile == null || sector.DataFile.Era != era))
                    continue;

                if (tags != null && !(tags.Any(tag => sector.Tags.Contains(tag))))
                    continue;

                data.Sectors.Add(new UniverseResult.SectorResult(sector));
            }

            SendResult(context, data);
        }
    }
}

namespace Maps.API.Results
{
    [XmlRoot(ElementName = "Universe")]
    // public for XML serialization
    public class UniverseResult
    {
        public UniverseResult() { }

        [XmlElement("Sector")]
        public List<SectorResult> Sectors { get { return m_sectors; } }
        private List<SectorResult> m_sectors = new List<SectorResult>();

        [XmlRoot("Sector")]
        public class SectorResult
        {
            private SectorResult() { }

            public SectorResult(Sector sector) { m_sector = sector; }
            private Sector m_sector;

            public int X { get { return m_sector.X; } }
            public int Y { get { return m_sector.Y; } }

            [XmlAttribute]
            public string Abbreviation { get { return m_sector.Abbreviation; } }

            [XmlAttribute]
            public string Tags { get { return m_sector.TagString; } }

            [XmlElement("Name")]
            public List<Name> Names { get { return m_sector.Names; } }
        }
    }
}