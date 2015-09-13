using Maps.Rendering;
using System;
using System.Drawing;

namespace Maps.API
{
    internal class TileHandler : ImageHandlerBase
    {
        protected override string ServiceName { get { return "tile"; } }

        public const int MinDimension = 1;
        public const int MaxDimension = 2048;

        public const int NormalTileWidth = 256;
        public const int NormalTileHeight = 256;

        public override void Process(System.Web.HttpContext context)
        {
            ResourceManager resourceManager = new ResourceManager(context.Server, context.Cache);

            MapOptions options = MapOptions.SectorGrid | MapOptions.BordersMajor | MapOptions.NamesMajor | MapOptions.NamesMinor;
            Stylesheet.Style style = Stylesheet.Style.Poster;
            ParseOptions(context, ref options, ref style);

            double x = GetDoubleOption(context, "x", 0);
            double y = GetDoubleOption(context, "y", 0);
            double scale = Util.Clamp(GetDoubleOption(context, "scale", 0), MinScale, MaxScale);
            int width = Util.Clamp(GetIntOption(context, "w", NormalTileWidth), MinDimension, MaxDimension);
            int height = Util.Clamp(GetIntOption(context, "h", NormalTileHeight), MinDimension, MaxDimension);

            Size tileSize = new Size(width, height);

            RectangleF tileRect = new RectangleF();
            tileRect.X = (float)(x * tileSize.Width / (scale * Astrometrics.ParsecScaleX));
            tileRect.Y = (float)(y * tileSize.Height / (scale * Astrometrics.ParsecScaleY));
            tileRect.Width = (float)(tileSize.Width / (scale * Astrometrics.ParsecScaleX));
            tileRect.Height = (float)(tileSize.Height / (scale * Astrometrics.ParsecScaleY));

            DateTime dt = DateTime.Now;
            bool silly = (Math.Abs((int)x % 2) == Math.Abs((int)y % 2)) && (dt.Month == 4 && dt.Day == 1);
            silly = GetBoolOption(context, "silly", silly);

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
            ctx.silly = silly;
            ctx.clipOutsectorBorders = true;
            ProduceResponse(context, "Tile", ctx, tileSize);
        }
    }
}
