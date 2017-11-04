using Maps.Utilities;
using System.Collections.Generic;
using System.Web;
using System.Xml.Serialization;

namespace Maps.API
{
    internal class JumpWorldsHandler : DataHandlerBase
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

                //
                // Jump
                //
                int jump = GetIntOption("jump", 6).Clamp(0, 12);

                //
                // Coordinates
                //
                Location loc = Location.Empty;

                if (HasOption("sector") && HasOption("hex"))
                {
                    string sectorName = GetStringOption("sector");
                    int hex = GetIntOption("hex", 0);
                    Sector sector = map.FromName(sectorName) ??
                        throw new HttpError(404, "Not Found", $"The specified sector '{sectorName}' was not found.");

                    loc = new Location(sector.Location, hex);
                }
                else if (HasLocation())
                {
                    loc = GetLocation();
                }

                Selector selector = new HexSelector(map, resourceManager, loc, jump);

                var data = new Results.JumpWorldsResult();
                data.Worlds.AddRange(selector.Worlds);
                SendResult(data);
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
        [XmlElement("World")]
        public List<World> Worlds { get; } = new List<World>();
    }
}
