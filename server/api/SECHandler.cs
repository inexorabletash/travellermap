using System;
using System.IO;
using System.Net.Mime;
using System.Text;

namespace Maps.Pages
{
    public class SECHandler : DataHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Plain; } }
        protected override string ServiceName { get { return "sec"; } }

        public override void Process(System.Web.HttpContext context)
        {
            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            ResourceManager resourceManager = new ResourceManager(context.Server, context.Cache);
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);
            Sector sector;

            if (HasOption(context, "sx") && HasOption(context, "sy"))
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

            WorldFilter filter = null;
            if (HasOption(context, "subsector"))
            {
                string ss = GetStringOption(context, "subsector").ToUpperInvariant();
                filter = (World world) => (world.SS == ss);
            }

            bool sscoords = GetBoolOption(context, "sscoords", defaultValue: false);
            bool includeMetadata = GetBoolOption(context, "metadata", defaultValue: true);
            bool includeHeader = GetBoolOption(context, "header", defaultValue: true);

            string mediaType = GetStringOption(context, "type");
            Encoding encoding; ;
            switch (mediaType)
            {
                case "SecondSurvey":
                case "TabDelimited":
                    encoding = Util.UTF8_NO_BOM;
                    break;
                default:
                    encoding = Encoding.GetEncoding(1252);
                    break;
            }

            string data;
            using (var writer = new StringWriter())
            {
                // Content
                //
                sector.Serialize(resourceManager, writer, mediaType, includeMetadata: includeMetadata, includeHeader: includeHeader, sscoords: sscoords, filter: filter);
                data = writer.ToString();
            }
            SendResult(context, data, encoding);
        }
    }
}
