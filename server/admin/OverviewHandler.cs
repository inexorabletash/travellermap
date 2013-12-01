using Maps.Rendering;
using System;
using System.Drawing;

namespace Maps.Pages
{
    public class OverviewHandler : AdminHandler
    {
        public const int MinDimension = 64;
        public const int MaxDimension = 2048;

        public const int NormalTileWidth = 256;
        public const int NormalTileHeight = 256;

        protected override void Process(System.Web.HttpContext context)
        {
            ResourceManager resourceManager = new ResourceManager(context.Server, context.Cache);

            MapOptions options = MapOptions.SectorGrid | MapOptions.FilledBorders;
            Stylesheet.Style style = Stylesheet.Style.Poster;
            ParseOptions(context, ref options, ref style);

            double x = -0.5;
            double y = -0.5;
            double scale = 2;
            Size tileSize = new Size(1000, 1000);

            RectangleF tileRect = new RectangleF();
            tileRect.X = (float)(x * tileSize.Width / (scale * Astrometrics.ParsecScaleX));
            tileRect.Y = (float)(y * tileSize.Height / (scale * Astrometrics.ParsecScaleY));
            tileRect.Width = (float)(tileSize.Width / (scale * Astrometrics.ParsecScaleX));
            tileRect.Height = (float)(tileSize.Height / (scale * Astrometrics.ParsecScaleY));

            DateTime dt = DateTime.Now;

            Render.RenderContext ctx = new Render.RenderContext();
            ctx.resourceManager = resourceManager;
            ctx.selector = new RectSelector(
                SectorMap.FromName(SectorMap.DefaultSetting, resourceManager),
                resourceManager,
                tileRect);
            ctx.tileRect = tileRect;
            ctx.scale = scale;
            ctx.options = options;
            ctx.styles = new Stylesheet(scale, options, style);
            ctx.tileSize = tileSize;
            ctx.silly = false;
            ctx.clipOutsectorBorders = true;

            ctx.styles.microRoutes.visible = true;
            ctx.styles.macroRoutes.visible = false;
            ctx.styles.macroBorders.visible = false;
            ctx.styles.microBorders.visible = true;
            ctx.styles.capitals.visible = false;
            ctx.styles.worlds.visible = true;
            ctx.styles.worldDetails = WorldDetails.Dotmap;
            ctx.styles.showAllSectorNames = false;
            ctx.styles.showSomeSectorNames = false;
            ctx.styles.macroNames.visible = false;
            ctx.styles.pseudoRandomStars.visible = false;
            ctx.styles.fillMicroBorders = true;

            ImageGeneratorPage.ProduceResponse(context, this, "Overview", ctx, tileSize);
        }
    }
}
