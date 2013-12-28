using Maps.Rendering;
using System;
using System.Drawing;

namespace Maps.API
{
    public class PosterHandler : ImageHandlerBase
    {
        protected override string ServiceName { get { return "poster"; } }

        public override void Process(System.Web.HttpContext context)
        {
            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            ResourceManager resourceManager = new ResourceManager(context.Server, context.Cache);

            Selector selector;
            RectangleF tileRect = new RectangleF();
            MapOptions options = MapOptions.SectorGrid | MapOptions.SubsectorGrid | MapOptions.BordersMajor | MapOptions.BordersMinor | MapOptions.NamesMajor | MapOptions.NamesMinor | MapOptions.WorldsCapitals | MapOptions.WorldsHomeworlds;
            Stylesheet.Style style = Stylesheet.Style.Poster;
            ParseOptions(context, ref options, ref style);
            string title;
            bool clipOutsectorBorders;

            if (HasOption(context, "x1") && HasOption(context, "x2") &&
                HasOption(context, "y1") && HasOption(context, "y2"))
            {
                // Arbitrary rectangle

                int x1 = GetIntOption(context, "x1", 0);
                int x2 = GetIntOption(context, "x2", 0);
                int y1 = GetIntOption(context, "y1", 0);
                int y2 = GetIntOption(context, "y2", 0);

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

                title = String.Format("Poster ({0},{1}) - ({2},{3})", x1, y1, x2, y2);
                clipOutsectorBorders = true;
            }
            else
            {
                // Sector - either POSTed or specified by name
                Sector sector = null;
                options = options & ~MapOptions.SectorGrid;

                if (context.Request.HttpMethod == "POST")
                {
                    try
                    {
                        sector = GetPostedSector(context.Request);
                    }
                    catch (Exception ex)
                    {
                        SendError(context.Response, 400, "Invalid request", ex.Message);
                        return;
                    }

                    if (sector == null)
                    {
                        SendError(context.Response, 400, "Invalid request", "Either file or data must be supplied in the POST data.");
                        return;
                    }

                    title = "User Data";
                }
                else
                {
                    string sectorName = GetStringOption(context, "sector");
                    if (sectorName == null)
                    {
                        SendError(context.Response, 404, "Not Found", "No sector specified.");
                        return;
                    }

                    SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

                    sector = map.FromName(sectorName);
                    if (sector == null)
                    {
                        SendError(context.Response, 404, "Not Found", String.Format("The specified sector '{0}' was not found.", sectorName));
                        return;
                    }

                    title = sector.Names[0].Text;
                }

                if (sector != null && HasOption(context, "subsector") && GetStringOption(context, "subsector").Length > 0)
                {
                    options = options & ~MapOptions.SubsectorGrid;
                    string subsector = GetStringOption(context, "subsector");
                    int index = sector.SubsectorIndexFor(subsector);
                    if (index == -1)
                    {
                        SendError(context.Response, 404, "Not Found", String.Format("The specified subsector '{0}' was not found.", subsector));
                        return;
                    }

                    selector = new SubsectorSelector(resourceManager, sector, index);

                    tileRect = sector.SubsectorBounds(index);

                    options &= ~(MapOptions.SectorGrid | MapOptions.SubsectorGrid);

                    title = String.Format("{0} - Subsector {1}", title, 'A' + index);
                }
                else
                {
                    selector = new SectorSelector(resourceManager, sector);
                    tileRect = sector.Bounds;

                    options &= ~(MapOptions.SectorGrid);
                }

                // Account for jagged hexes
                tileRect.X -= 0.25f;
                tileRect.Width += 0.5f;
                tileRect.Height += 0.5f;

                if (style == Stylesheet.Style.Candy)
                {
                    tileRect.Width += 0.75f;
                }
                clipOutsectorBorders = false;
            }

            const double NormalScale = 64; // pixels/parsec - standard subsector-rendering scale
            double scale = Util.Clamp(GetDoubleOption(context, "scale", NormalScale), MinScale, MaxScale);

            int rot = GetIntOption(context, "rotation", 0) % 4;
            bool thumb = GetBoolOption(context, "thumb", false);

            Stylesheet stylesheet = new Stylesheet(scale, options, style);

            Size tileSize = new Size((int)Math.Floor(tileRect.Width * scale * Astrometrics.ParsecScaleX), (int)Math.Floor(tileRect.Height * scale * Astrometrics.ParsecScaleY));

            if (thumb)
            {
                tileSize.Width = (int)Math.Floor(16 * tileSize.Width / scale);
                tileSize.Height = (int)Math.Floor(16 * tileSize.Height / scale);
                scale = 16;
            }

            int bitmapWidth = tileSize.Width, bitmapHeight = tileSize.Height;
            float translateX = 0, translateY = 0, angle = rot * 90;
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

            Render.RenderContext ctx = new Render.RenderContext();
            ctx.resourceManager = resourceManager;
            ctx.selector = selector;
            ctx.tileRect = tileRect;
            ctx.scale = scale;
            ctx.options = options;
            ctx.styles = stylesheet;
            ctx.tileSize = tileSize;
            ctx.clipOutsectorBorders = clipOutsectorBorders;
            ProduceResponse(context, title, ctx, new Size(bitmapWidth, bitmapHeight), rot, translateX, translateY);
        }
    }
}
