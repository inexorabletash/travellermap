using Maps.Rendering;
using System;
using System.Drawing;
using System.Web;

namespace Maps.API
{
    internal class TileHandler : ImageHandlerBase
    {
        protected override string ServiceName { get { return "tile"; } }

        public const int MinDimension = 1;
        public const int MaxDimension = 2048;

        public const int NormalTileWidth = 256;
        public const int NormalTileHeight = 256;

        protected override DataResponder GetResponder(HttpContext context)
        {
            return new Responder(context);
        }
        private class Responder : ImageResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override void Process()
            {
                ResourceManager resourceManager = new ResourceManager(Context.Server);

                MapOptions options = MapOptions.SectorGrid | MapOptions.BordersMajor | MapOptions.NamesMajor | MapOptions.NamesMinor;
                Stylesheet.Style style = Stylesheet.Style.Poster;
                ParseOptions(ref options, ref style);

                double x = GetDoubleOption("x", 0);
                double y = GetDoubleOption("y", 0);
                double scale = Util.Clamp(GetDoubleOption("scale", 0), MinScale, MaxScale);

                int width = GetIntOption("w", NormalTileWidth);
                int height = GetIntOption("h", NormalTileHeight);
                if (width < 0 || height < 1)
                {
                    throw new HttpError(400, "Bad Request",
                          $"Requested dimensions ({width}x{height}) invalid.");
                }
                if (width * height > MaxDimension * MaxDimension)
                {
                    throw new HttpError(400, "Bad Request",
                         $"Requested dimensions ({width}x{height}) too large.");
                }


                Size tileSize = new Size(width, height);

                RectangleF tileRect = new RectangleF();
                tileRect.X = (float)(x * tileSize.Width / (scale * Astrometrics.ParsecScaleX));
                tileRect.Y = (float)(y * tileSize.Height / (scale * Astrometrics.ParsecScaleY));
                tileRect.Width = (float)(tileSize.Width / (scale * Astrometrics.ParsecScaleX));
                tileRect.Height = (float)(tileSize.Height / (scale * Astrometrics.ParsecScaleY));

                DateTime dt = DateTime.Now;
                bool silly = (Math.Abs((int)x % 2) == Math.Abs((int)y % 2)) && (dt.Month == 4 && dt.Day == 1);
                silly = GetBoolOption("silly", silly);

                Selector selector = new RectSelector(SectorMap.ForMilieu(resourceManager, GetStringOption("milieu")), resourceManager, tileRect);
                Stylesheet styles = new Stylesheet(scale, options, style);
                RenderContext ctx = new RenderContext(resourceManager, selector, tileRect, scale, options, styles, tileSize);
                ctx.Silly = silly;
                ctx.ClipOutsectorBorders = true;
                ProduceResponse(Context, "Tile", ctx, tileSize);
            }
        }
    }
}