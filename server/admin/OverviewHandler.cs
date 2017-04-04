using Maps.API;
using Maps.Graphics;
using Maps.Rendering;
using System.Drawing;
using System.Web;

namespace Maps.Admin
{
    internal class OverviewHandler : AdminHandler
    {
        IHttpHandler impl = new OverviewImpl();

        // TODO: Passed resourceManager is discarded. Avoid creating it.
        protected override void Process(HttpContext context, ResourceManager resourceManager)
        {
            impl.ProcessRequest(context);
        }

        class OverviewImpl : ImageHandlerBase
        {
            protected override string ServiceName => "overview";
            protected override DataResponder GetResponder(HttpContext context)
            {
                return new Responder(context);
            }
            private class Responder : ImageResponder
            {
                public Responder(HttpContext context) : base(context) { }
                public override void Process(ResourceManager resourceManager)
                {
                    MapOptions options = MapOptions.SectorGrid | MapOptions.FilledBorders;
                    Style style = Style.Poster;
                    ParseOptions(ref options, ref style);

                    float x = -0.5f;
                    float y = -0.5f;
                    float scale = 2;
                    Size tileSize = new Size(1000, 1000);

                    RectangleF tileRect = new RectangleF()
                    {
                        X = x * tileSize.Width / (scale * Astrometrics.ParsecScaleX),
                        Y = y * tileSize.Height / (scale * Astrometrics.ParsecScaleY),
                        Width = tileSize.Width / (scale * Astrometrics.ParsecScaleX),
                        Height = tileSize.Height / (scale * Astrometrics.ParsecScaleY)
                    };
                    Selector selector = new RectSelector(
                        SectorMap.ForMilieu(resourceManager, GetStringOption("milieu")),
                        resourceManager,
                        tileRect);
                    Stylesheet styles = new Stylesheet(scale, options, style);
                    styles.microRoutes.visible = true;
                    styles.macroRoutes.visible = false;
                    styles.macroBorders.visible = false;
                    styles.microBorders.visible = true;
                    styles.capitals.visible = false;
                    styles.worlds.visible = true;
                    styles.worldDetails = WorldDetails.Dotmap;
                    styles.showAllSectorNames = false;
                    styles.showSomeSectorNames = false;
                    styles.macroNames.visible = false;
                    styles.pseudoRandomStars.visible = false;
                    styles.fillMicroBorders = true;

                    RenderContext ctx = new RenderContext(resourceManager, selector, tileRect, scale, options, styles, tileSize)
                    {
                        ClipOutsectorBorders = true
                    };
                    ProduceResponse(Context, "Overview", ctx, tileSize, AbstractMatrix.Identity);
                }
            }
        }
    }
}