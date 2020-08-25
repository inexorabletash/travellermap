#nullable enable
using Maps.Graphics;
using Maps.Rendering;
using Maps.Utilities;
using System.Drawing;
using System.Web;

namespace Maps.API
{
    internal class TileHandler : ImageHandlerBase
    {
        public const int MinDimension = 1;
        public const int MaxDimension = 2048;

        public const int NormalTileWidth = 256;
        public const int NormalTileHeight = 256;

        protected override DataResponder GetResponder(HttpContext context) => new Responder(context);

        private class Responder : ImageResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override void Process(ResourceManager resourceManager)
            {
                MapOptions options = MapOptions.SectorGrid | MapOptions.BordersMajor | MapOptions.NamesMajor | MapOptions.NamesMinor;
                Style style = Style.Poster;
                ParseOptions(ref options, ref style);

                double x = GetDoubleOption("x", 0);
                double y = GetDoubleOption("y", 0);
                double scale = GetDoubleOption("scale", 0).Clamp(MinScale, MaxScale);

                int width = GetIntOption("w", NormalTileWidth);
                int height = GetIntOption("h", NormalTileHeight);
                if (width < 1 || height < 1)
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

                RectangleF tileRect = new RectangleF()
                {
                    X = (float)(x * tileSize.Width / (scale * Astrometrics.ParsecScaleX)),
                    Y = (float)(y * tileSize.Height / (scale * Astrometrics.ParsecScaleY)),
                    Width = (float)(tileSize.Width / (scale * Astrometrics.ParsecScaleX)),
                    Height = (float)(tileSize.Height / (scale * Astrometrics.ParsecScaleY))
                };

                Selector selector = new RectSelector(SectorMap.ForMilieu(resourceManager, GetStringOption("milieu")), resourceManager, tileRect);
                Stylesheet styles = new Stylesheet(scale, options, style);
                RenderContext ctx = new RenderContext(resourceManager, selector, tileRect, scale, options, styles, tileSize)
                {
                    ClipOutsectorBorders = true
                };
                ProduceResponse(Context, "Tile", ctx, tileSize, AbstractMatrix.Identity);
            }
        }
    }
}