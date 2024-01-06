﻿#nullable enable
using Maps.Serialization;
using Maps.Utilities;
using System.IO;
using System.Web;

namespace Maps.API
{
    internal class MSECHandler : DataHandlerBase
    {
        protected override DataResponder GetResponder(HttpContext context) => new Responder(context);

        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType => ContentTypes.Text.Plain;

            public override void Process(ResourceManager resourceManager)
            {
                // NOTE: This (re)initializes a static data structure used for 
                // resolving names into sector locations, so needs to be run
                // before any other objects (e.g. Worlds) are loaded.
                SectorMap.Milieu map = SectorMap.ForMilieu(GetStringOption("milieu"));
                Sector sector;

                if (HasOption("sx") && HasOption("sy"))
                {
                    int sx = GetIntOption("sx", 0);
                    int sy = GetIntOption("sy", 0);

                    sector = map.FromLocation(sx, sy) ??
                        throw new HttpError(404, "Not Found", $"The sector at {sx},{sy} was not found.");
                }
                else if (HasOption("sector"))
                {
                    string sectorName = GetStringOption("sector")!;
                    sector = map.FromName(sectorName) ??
                        throw new HttpError(404, "Not Found", $"The specified sector '{sectorName}' was not found.");
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

                SendResult(data);
            }
        }
    }
}