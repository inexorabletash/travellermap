using Maps.API.Results;
using Maps.Utilities;
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
        protected override string ServiceName => "universe";
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

                    if (sector.Tags.Contains("meta") && !(tags?.Contains("meta") ?? false))
                        continue;

                    if (milieu != null && sector.CanonicalMilieu != milieu)
                        continue;

                    if (tags != null && !tags.Any(tag => sector.Tags.Contains(tag)))
                        continue;

                    data.Sectors.Add(new UniverseResult.SectorResult(sector));
                }
                SendResult(data);
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
        public List<SectorResult> Sectors { get; } = new List<SectorResult>();

        [XmlRoot("Sector")]
        public class SectorResult
        {
            private SectorResult() { }

            public SectorResult(Sector sector) { this.sector = sector; }
            private Sector sector;

            public int X { get => sector.X; set { } }
            public int Y { get => sector.Y; set { } }
            public string Milieu { get => sector.CanonicalMilieu; set { } }

            [XmlAttribute]
            public string Abbreviation { get => sector.Abbreviation; set { } }

            [XmlAttribute]
            public string Tags { get => sector.TagString; set { } }

            [XmlElement("Name")]
            public List<Name> Names { get => sector.Names; set { } }
        }
    }
}
