using Maps.Rendering;
using System;
using System.Drawing;
using System.Web;

namespace Maps.API
{
    internal class PosterHandler : ImageHandlerBase
    {
        protected override string ServiceName { get { return "poster"; } }

        protected override DataResponder GetResponder(HttpContext context)
        {
            return new Responder(context);
        }
        private class Responder : ImageResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override void Process()
            {
                // NOTE: This (re)initializes a static data structure used for
                // resolving names into sector locations, so needs to be run
                // before any other objects (e.g. Worlds) are loaded.
                ResourceManager resourceManager = new ResourceManager(context.Server);

                Selector selector;
                RectangleF tileRect = new RectangleF();
                MapOptions options = MapOptions.SectorGrid | MapOptions.SubsectorGrid | MapOptions.BordersMajor | MapOptions.BordersMinor | MapOptions.NamesMajor | MapOptions.NamesMinor | MapOptions.WorldsCapitals | MapOptions.WorldsHomeworlds;
                Stylesheet.Style style = Stylesheet.Style.Poster;
                ParseOptions(ref options, ref style);
                string title;
                bool clipOutsectorBorders;

                if (HasOption("x1") && HasOption("x2") &&
                    HasOption("y1") && HasOption("y2"))
                {
                    // Arbitrary rectangle

                    int x1 = GetIntOption("x1", 0);
                    int x2 = GetIntOption("x2", 0);
                    int y1 = GetIntOption("y1", 0);
                    int y2 = GetIntOption("y2", 0);

                    tileRect.X = Math.Min(x1, x2);
                    tileRect.Y = Math.Min(y1, y2);
                    tileRect.Width = Math.Max(x1, x2) - tileRect.X;
                    tileRect.Height = Math.Max(y1, y2) - tileRect.Y;

                    SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);
                    selector = new RectSelector(map, resourceManager, tileRect);
                    selector.Slop = false;

                    tileRect.Offset(-1, -1);
                    tileRect.Width += 1;
                    tileRect.Height += 1;

                    title = string.Format("Poster ({0},{1}) - ({2},{3})", x1, y1, x2, y2);
                    clipOutsectorBorders = true;
                }
                else if (HasOption("domain"))
                {
                    string domain = GetStringOption("domain");
                    int x, y, w = 2, h = 2;
                    switch (domain.ToLowerInvariant())
                    {
                        case "deneb": x = -4; y = -1; title = "Domain of Deneb"; break;
                        case "vland": x = -2; y = -1; title = "Domain of Vland"; break;
                        case "ilelish": x = -2; y = 1; title = "Domain of Ilelish"; break;
                        case "antares": x = 0; y = -2; title = "Domain of Antares"; break;
                        case "sylea": x = 0; y = 0; title = "Domain of Sylea"; break;
                        case "sol": x = 0; y = 2; title = "Domain of Sol"; break;
                        case "gateway": x = 2; y = 0; title = "Domain of Gateway"; break;

                        // And these aren't domains, but...
                        case "foreven": x = -6; y = -1; title = "Land Grant / Foreven"; break;
                        case "imperium": x = -4; y = -1; w = 7; h = 5; title = "Third Imperium"; break;
                        case "solomani": x = -2; y = 2; w = 5; h = 3; title = "Solomani Confederacy"; break;
                        case "zhodani": x = -8; y = -3; w = 5; h = 3; title = "Zhodani Consulate"; break;
                        case "hive":
                        case "hiver": x = 2; y = 1; w = 6; h = 4; title = "Hiver Federation"; break;
                        case "aslan": x = -8; y = 1; w = 7; h = 4; title = "Aslan Hierate"; break;
                        case "vargr": x = -4; y = -4; w = 8; h = 3; title = "Vargr Extents"; break;
                        // TODO: K'kree
                        // TODO: Zhodani provinces

                        case "jg": x = 160; y = 0; w = 2; h = 2; title = "Judges Guild"; break;

                        default:
                            throw new HttpError(404, "Not Found", string.Format("Unknown domain: {0}", domain));
                    }

                    int x1 = x * Astrometrics.SectorWidth - Astrometrics.ReferenceHex.X + 1;
                    int y1 = y * Astrometrics.SectorHeight - Astrometrics.ReferenceHex.Y + 1;
                    int x2 = x1 + w * Astrometrics.SectorWidth - 1;
                    int y2 = y1 + h * Astrometrics.SectorHeight - 1;

                    tileRect.X = Math.Min(x1, x2);
                    tileRect.Y = Math.Min(y1, y2);
                    tileRect.Width = Math.Max(x1, x2) - tileRect.X;
                    tileRect.Height = Math.Max(y1, y2) - tileRect.Y;

                    SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);
                    selector = new RectSelector(map, resourceManager, tileRect);
                    selector.Slop = false;

                    tileRect.Offset(-1, -1);
                    tileRect.Width += 1;
                    tileRect.Height += 1;

                    // Account for jagged hexes
                    tileRect.Height += 0.5f;
                    tileRect.Inflate(0.25f, 0.10f);
                    if (style == Stylesheet.Style.Candy)
                        tileRect.Width += 0.75f;

                    clipOutsectorBorders = true;
                }
                else
                {
                    // Sector - either POSTed or specified by name
                    Sector sector = null;
                    options = options & ~MapOptions.SectorGrid;

                    if (context.Request.HttpMethod == "POST")
                    {
                        bool lint = GetBoolOption("lint", defaultValue: false);
                        ErrorLogger errors = new ErrorLogger();
                        sector = GetPostedSector(context.Request, errors);
                        if (lint && !errors.Empty)
                            throw new HttpError(400, "Bad Request", errors.ToString());

                        if (sector == null)
                            throw new HttpError(400, "Bad Request", "Either file or data must be supplied in the POST data.");

                        title = "User Data";

                        // TODO: Suppress all OTU rendering.
                        options = options & ~MapOptions.WorldsHomeworlds & ~MapOptions.WorldsCapitals;
                    }
                    else
                    {
                        string sectorName = GetStringOption("sector");
                        if (sectorName == null)
                            throw new HttpError(400, "Bad Request", "No sector specified.");

                        SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

                        sector = map.FromName(sectorName);
                        if (sector == null)
                            throw new HttpError(404, "Not Found", string.Format("The specified sector '{0}' was not found.", sectorName));

                        title = sector.Names[0].Text;
                    }

                    if (sector != null && HasOption("subsector") && GetStringOption("subsector").Length > 0)
                    {
                        options = options & ~MapOptions.SubsectorGrid;
                        string subsector = GetStringOption("subsector");
                        int index = sector.SubsectorIndexFor(subsector);
                        if (index == -1)
                            throw new HttpError(404, "Not Found", string.Format("The specified subsector '{0}' was not found.", subsector));

                        selector = new SubsectorSelector(resourceManager, sector, index);

                        tileRect = sector.SubsectorBounds(index);

                        options &= ~(MapOptions.SectorGrid | MapOptions.SubsectorGrid);

                        title = string.Format("{0} - Subsector {1}", title, 'A' + index);
                    }
                    else if (sector != null && HasOption("quadrant") && GetStringOption("quadrant").Length > 0)
                    {
                        string quadrant = GetStringOption("quadrant");
                        int index;
                        switch (quadrant.ToLowerInvariant())
                        {
                            case "alpha": index = 0; quadrant = "Alpha"; break;
                            case "beta": index = 1; quadrant = "Beta"; break;
                            case "gamma": index = 2; quadrant = "Gamma"; break;
                            case "delta": index = 3; quadrant = "Delta"; break;
                            default:
                                throw new HttpError(400, "Bad Request", string.Format("The specified quadrant '{0}' is invalid.", quadrant));
                        }

                        selector = new QuadrantSelector(resourceManager, sector, index);
                        tileRect = sector.QuadrantBounds(index);

                        options &= ~(MapOptions.SectorGrid | MapOptions.SubsectorGrid | MapOptions.SectorsMask);

                        title = string.Format("{0} - {1} Quadrant", title, quadrant);
                    }
                    else
                    {
                        selector = new SectorSelector(resourceManager, sector);
                        tileRect = sector.Bounds;

                        options &= ~(MapOptions.SectorGrid);
                    }

                    // Account for jagged hexes
                    tileRect.Height += 0.5f;
                    tileRect.Inflate(0.25f, 0.10f);
                    if (style == Stylesheet.Style.Candy)
                        tileRect.Width += 0.75f;
                    clipOutsectorBorders = false;
                }

                const double NormalScale = 64; // pixels/parsec - standard subsector-rendering scale
                double scale = Util.Clamp(GetDoubleOption("scale", NormalScale), MinScale, MaxScale);

                int rot = GetIntOption("rotation", 0) % 4;
                bool thumb = GetBoolOption("thumb", false);

                Stylesheet stylesheet = new Stylesheet(scale, options, style);

                Size tileSize = new Size((int)Math.Floor(tileRect.Width * scale * Astrometrics.ParsecScaleX), (int)Math.Floor(tileRect.Height * scale * Astrometrics.ParsecScaleY));

                if (thumb)
                {
                    tileSize.Width = (int)Math.Floor(16 * tileSize.Width / scale);
                    tileSize.Height = (int)Math.Floor(16 * tileSize.Height / scale);
                    scale = 16;
                }

                int bitmapWidth = tileSize.Width, bitmapHeight = tileSize.Height;
                float translateX = 0, translateY = 0;
                switch (rot)
                {
                    case 1: // 90 degrees clockwise
                        Util.Swap(ref bitmapWidth, ref bitmapHeight);
                        translateX = bitmapWidth;
                        break;
                    case 2: // 180 degrees
                        translateX = bitmapWidth; translateY = bitmapHeight;
                        break;
                    case 3: // 270 degrees clockwise
                        Util.Swap(ref bitmapWidth, ref bitmapHeight);
                        translateY = bitmapHeight;
                        break;
                }

                RenderContext ctx = new RenderContext(resourceManager, selector, tileRect, scale, options, stylesheet, tileSize);
                ctx.ClipOutsectorBorders = clipOutsectorBorders;
                ProduceResponse(context, title, ctx, new Size(bitmapWidth, bitmapHeight), rot, translateX, translateY);
            }
        }
    }
}