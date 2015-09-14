using Maps.Serialization;
using System;
using System.IO;
using System.Web;

namespace Maps.API
{
    internal class MSECHandler : DataHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Plain; } }
        protected override string ServiceName { get { return "msec"; } }

        public override void Process(HttpContext context)
        {
            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            ResourceManager resourceManager = new ResourceManager(context.Server);
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);
            Sector sector;

            if (HasOption(context, "sx") && HasOption(context, "sy"))
            {
                int sx = GetIntOption(context, "sx", 0);
                int sy = GetIntOption(context, "sy", 0);

                sector = map.FromLocation(sx, sy);

                if (sector == null)
                {
                    SendError(context.Response, 404, "Not Found", string.Format("The sector at {0},{1} was not found.", sx, sy));
                    return;
                }
            }
            else if (HasOption(context, "sector"))
            {
                string sectorName = GetStringOption(context, "sector");
                sector = map.FromName(sectorName);

                if (sector == null)
                {
                    SendError(context.Response, 404, "Not Found", string.Format("The specified sector '{0}' was not found.", sectorName));
                    return;
                }
            }
            else
            {
                SendError(context.Response, 400, "Bad Request", "No sector specified.");
                return;
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
