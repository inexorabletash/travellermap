#nullable enable
using Maps.Serialization;
using Maps.Utilities;
using System;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Web;

namespace Maps.API
{
    internal class SECHandler : DataHandlerBase
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
                SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, GetStringOption("milieu"));
                Sector sector;

                SectorSerializeOptions options = new SectorSerializeOptions()
                {
                    sscoords = GetBoolOption("sscoords", defaultValue: false),
                    includeMetadata = GetBoolOption("metadata", defaultValue: true),
                    includeHeader = GetBoolOption("header", defaultValue: true),
                    includeRoutes = GetBoolOption("routes", defaultValue: false)
                };
                if (Context.Request.HttpMethod == "POST")
                {
                    bool lint = GetBoolOption("lint", defaultValue: false);
                    ErrorLogger? errors = null;
                    if (lint)
                    {
                        bool hide_uwp = GetBoolOption("hide-uwp", defaultValue: false);
                        bool hide_tl = GetBoolOption("hide-tl", defaultValue: false);
                        bool filter(ErrorLogger.Record record)
                        {
                            if (hide_uwp && record.message.StartsWith("UWP")) return false;
                            if (hide_tl && record.message.StartsWith("UWP: TL")) return false;
                            return true;
                        }
                        errors = new ErrorLogger(filter);
                    }

                    try
                    {
                        sector = new Sector(Context.Request.InputStream, new ContentType(Context.Request.ContentType).MediaType, errors);
                    }
                    catch (ParseException ex)
                    {
                        if (!lint)
                            throw;
                        throw new HttpError(400, "Bad Request", $"Bad data file: {ex.Message}");
                    }
                    if (lint && errors != null && !errors.Empty)
                        throw new HttpError(400, "Bad Request", errors.ToString());
                    options.includeMetadata = false;
                }
                else if (HasOption("sx") && HasOption("sy"))
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

                if (HasOption("subsector"))
                {
                    string subsector = GetStringOption("subsector")!;
                    int index = sector.SubsectorIndexFor(subsector);
                    if (index == -1)
                        throw new HttpError(404, "Not Found", $"The specified subsector '{subsector}' was not found.");
                    options.filter = (World world) => (world.Subsector == index);
                }
                else if (HasOption("quadrant"))
                {
                    string quadrant = GetStringOption("quadrant")!;
                    int index = Sector.QuadrantIndexFor(quadrant);
                    if (index == -1)
                        throw new HttpError(400, "Bad Request", $"The specified quadrant '{quadrant}' is invalid.");
                    options.filter = (World world) => (world.Quadrant == index);
                }

                string? mediaType = GetStringOption("type");
                Encoding encoding = mediaType switch
                {
                    "SecondSurvey" => Util.UTF8_NO_BOM,
                    "TabDelimited" => Util.UTF8_NO_BOM,
                    _ => Encoding.GetEncoding(1252),
                };

                string data;
                using (var writer = new StringWriter())
                {
                    // Content
                    //
                    sector.Serialize(resourceManager, writer, mediaType, options);
                    data = writer.ToString();
                }
                SendResult(data, encoding);
            }
        }
    }
}