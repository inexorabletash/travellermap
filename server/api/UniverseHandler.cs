using System.Collections.Generic;
using System.Web;
using System.Xml;
using System.Xml.Serialization;

namespace Maps.API
{
    /// <summary>
    /// Fetch data about the universe.
    /// </summary>
    public class UniverseHandler : DataHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }
        protected override string ServiceName { get { return "universe"; } }

        public override void Process(HttpContext context)
        {
            ResourceManager resourceManager = new ResourceManager(context.Server, context.Cache);

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            // Filter parameters
            string era = GetStringOption(context, "era");
            bool requireData = GetBoolOption(context, "requireData", defaultValue: false);

            Result data = new Result();
            foreach (Sector sector in map.Sectors)
            {
                if (requireData && sector.DataFile == null)
                    continue;

                if (era != null && (sector.DataFile == null || sector.DataFile.Era != era))
                    continue;

                SectorBase sb = new SectorBase(sector);
                data.Sectors.Add(sb);
            }

            SendResult(context, data);
        }

        [XmlRoot(ElementName = "Universe")]
        // public for XML serialization
        public class Result
        {
            public Result()
            {
                Sectors = new List<SectorBase>();
            }

            [XmlElement("Sector")]
            public List<SectorBase> Sectors { get; set; }
        }
    }
}
