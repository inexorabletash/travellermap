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
        protected override string ServiceName { get { return "universe"; } }

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
                ResourceManager resourceManager = new ResourceManager(context.Server);

                // NOTE: This (re)initializes a static data structure used for 
                // resolving names into sector locations, so needs to be run
                // before any other objects (e.g. Worlds) are loaded.
                SectorMap map = SectorMap.GetInstance(resourceManager);

                // Filter parameters
                string milieu = GetStringOption("milieu") ?? GetStringOption("era");
                bool requireData = GetBoolOption("requireData", defaultValue: false);
                string[] tags = GetStringsOption("tag");

                UniverseResult data = new UniverseResult();
                foreach (Sector sector in map.Sectors)
                {
                    if (requireData && sector.DataFile == null)
                        continue;

                    if (milieu != null && sector.DataFile?.Era != milieu)
                        continue;

                    if (tags != null && !tags.Any(tag => sector.Tags.Contains(tag)))
                        continue;

                    data.Sectors.Add(new UniverseResult.SectorResult(sector));
                }

                SendResult(context, data);
            }
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
        public List<SectorResult> Sectors { get { return sectors; } }
        private List<SectorResult> sectors = new List<SectorResult>();

        [XmlRoot("Sector")]
        public class SectorResult
        {
            private SectorResult() { }

            public SectorResult(Sector sector) { this.sector = sector; }
            private Sector sector;

            public int X { get { return sector.X; } }
            public int Y { get { return sector.Y; } }

            [XmlAttribute]
            public string Abbreviation { get { return sector.Abbreviation; } }

            [XmlAttribute]
            public string Tags { get { return sector.TagString; } }

            [XmlElement("Name")]
            public List<Name> Names { get { return sector.Names; } }
        }
    }
}