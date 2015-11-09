using System.Collections.Generic;
using System.Web;
using System.Xml.Serialization;

namespace Maps.API
{
    internal class JumpWorldsHandler : DataHandlerBase
    {
        protected override string ServiceName { get { return "jumpworlds"; } }
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

                //
                // Jump
                //
                int jump = Util.Clamp(GetIntOption("jump", 6), 0, 12);

                //
                // Coordinates
                //
                SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, GetStringOption("milieu"));
                Location loc = new Location(map.FromName("Spinward Marches").Location, 1910);

                if (HasOption("sector") && HasOption("hex"))
                {
                    string sectorName = GetStringOption("sector");
                    int hex = GetIntOption("hex", 0);
                    Sector sector = map.FromName(sectorName);
                    if (sector == null)
                        throw new HttpError(404, "Not Found", string.Format("The specified sector '{0}' was not found.", sectorName));

                    loc = new Location(sector.Location, hex);
                }
                else if (HasLocation())
                {
                    loc = GetLocation();
                }

                Selector selector = new HexSelector(map, resourceManager, loc, jump);

                var data = new Results.JumpWorldsResult();
                data.Worlds.AddRange(selector.Worlds);
                SendResult(context, data);
            }
        }
    }
}

namespace Maps.API.Results
{
    [XmlRoot(ElementName = "JumpWorlds")]
    // public for XML serialization
    public class JumpWorldsResult
    {
        public JumpWorldsResult()
        {
            Worlds = new List<World>();
        }

        [XmlElement("World")]
        public List<World> Worlds { get; }
    }
}
