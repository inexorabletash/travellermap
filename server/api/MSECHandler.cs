using Maps.Serialization;
using System;
using System.IO;
using System.Web;

namespace Maps.API
{
    internal class MSECHandler : DataHandlerBase
    {
        protected override string ServiceName { get { return "msec"; } }

        protected override DataResponder GetResponder(HttpContext context)
        {
            return new Responder(context);
        }
        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Plain; } }
            public override void Process()
            {
                // NOTE: This (re)initializes a static data structure used for 
                // resolving names into sector locations, so needs to be run
                // before any other objects (e.g. Worlds) are loaded.
                ResourceManager resourceManager = new ResourceManager(context.Server);
                SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);
                Sector sector;

                if (HasOption("sx") && HasOption("sy"))
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

                string data;
                using (var writer = new StringWriter())
                {
                    new MSECSerializer().Serialize(writer, sector);
                    data = writer.ToString();
                }

                SendResult(context, data);
            }
        }
    }
}