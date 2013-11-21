using Maps.Rendering;
using System;
using System.Drawing;

namespace Maps.Pages
{
    /// <summary>
    /// Summary description for WebForm1.
    /// </summary>

    public class Poster : ImageGeneratorPage
    {
        private void Page_Load(object sender, System.EventArgs e)
        {
            if (!ServiceConfiguration.CheckEnabled("poster", Response))
            {
                return;
            }

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            ResourceManager resourceManager = new ResourceManager(Server, Cache);

            Selector selector;
            RectangleF tileRect = new RectangleF();
            MapOptions options = MapOptions.SectorGrid | MapOptions.SubsectorGrid | MapOptions.BordersMajor | MapOptions.BordersMinor | MapOptions.NamesMajor | MapOptions.NamesMinor | MapOptions.WorldsCapitals | MapOptions.WorldsHomeworlds;
            Stylesheet.Style style = Stylesheet.Style.Poster;
            ParseOptions(ref options, ref style);
            string title;

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

                title = String.Format("Poster ({0},{1}) - ({2},{3})", x1, y1, x2, y2);
            }
            else
            {
                // Sector - either POSTed or specified by name
                Sector sector = null;
                options = options & ~MapOptions.SectorGrid;

                if (Request.HttpMethod == "POST")
                {
                    try
                    {
                        sector = GetPostedSector();
                    }
                    catch (Exception ex)
                    {
                        SendError(400, "Invalid request", ex.Message);
                        return;
                    }

                    if (sector == null)
                    {
                        SendError(400, "Invalid request", "Either file or data must be supplied in the POST data.");
                        return;
                    }

                    title = "User Data";
                }
                else
                {
                    string sectorName = GetStringOption("sector");
                    if (sectorName == null)
                    {
                        SendError(404, "Not Found", "No sector specified.");
                        return;
                    }

                    SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

                    sector = map.FromName(sectorName);
                    if (sector == null)
                    {
                        SendError(404, "Not Found", String.Format("The specified sector '{0}' was not found.", sectorName));
                        return;
                    }

                    title = sector.Names[0].Text;
                }

                if (HasOption("subsector") && GetStringOption("subsector").Length > 0)
                {
                    options = options & ~MapOptions.SubsectorGrid;
                    char ss = GetStringOption("subsector").ToUpperInvariant()[0];
                    if (ss < 'A' || ss > 'P')
                    {
                        SendError(400, "Invalid subsector", String.Format("The subsector index '{0}' is not valid (must be A...P).", ss));
                        return;
                    }

                    int index = (int)(ss) - (int)('A');
                    selector = new SubsectorSelector(resourceManager, sector, index);

                    tileRect = sector.SubsectorBounds(index);

                    options &= ~(MapOptions.SectorGrid | MapOptions.SubsectorGrid);

                    title = String.Format("{0} - Subsector {1}", title, ss);
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

            }

            const double NormalScale = 64; // pixels/parsec - standard subsector-rendering scale
            double scale = Util.Clamp(GetDoubleOption("scale", NormalScale), MinScale, MaxScale);

            int rot = GetIntOption("rotation", 0) % 4;

            Size tileSize = new Size((int)Math.Floor(tileRect.Width * scale * Astrometrics.ParsecScaleX), (int)Math.Floor(tileRect.Height * scale * Astrometrics.ParsecScaleY));

            int bitmapWidth = tileSize.Width, bitmapHeight = tileSize.Height;
            float translateX = 0, translateY = 0, angle = rot * 90;
            switch (rot)
            {
                case 1: // 90 degrees clockwise
                    bitmapWidth = tileSize.Height; bitmapHeight = tileSize.Width;
                    translateX = bitmapWidth;
                    break;
                case 2: // 180 degrees
                    translateX = tileSize.Width; translateY = tileSize.Height;
                    break;
                case 3: // 270 degrees clockwise
                    bitmapWidth = tileSize.Height; bitmapHeight = tileSize.Width;
                    translateY = bitmapHeight;
                    break;
            }

            Render.RenderContext ctx = new Render.RenderContext();
            ctx.resourceManager = resourceManager;
            ctx.selector = selector;
            ctx.tileRect = tileRect;
            ctx.scale = scale;
            ctx.options = options;
            ctx.styles = new Stylesheet(scale, options, style);
            ctx.tileSize = tileSize;
            ProduceResponse(title, ctx, new Size(bitmapWidth, bitmapHeight), rot, translateX, translateY);
        }



        #region Web Form Designer generated code
        override protected void OnInit(EventArgs e)
        {
            //
            // CODEGEN: This call is required by the ASP.NET Web Form Designer.
            //
            InitializeComponent();
            base.OnInit(e);
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.Load += new System.EventHandler(this.Page_Load);
        }
        #endregion
    }
}
