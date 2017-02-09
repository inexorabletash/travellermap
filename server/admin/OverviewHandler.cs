using Maps.Rendering;
using System.Drawing;
using System.Web;

namespace Maps.Admin
{
    internal class OverviewHandler : AdminHandler
    {
        IHttpHandler impl = new OverviewImpl();

        protected override void Process(System.Web.HttpContext context)
        {
            impl.ProcessRequest(context);
        }

        class OverviewImpl : Maps.API.ImageHandlerBase
        {
            protected override string ServiceName { get { return "overview"; } }

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

                    MapOptions options = MapOptions.SectorGrid | MapOptions.FilledBorders;
                    Stylesheet.Style style = Stylesheet.Style.Poster;
                    ParseOptions(ref options, ref style);

                    float x = -0.5f;
                    float y = -0.5f;
                    float scale = 2;
                    Size tileSize = new Size(1000, 1000);

                    RectangleF tileRect = new RectangleF();
                    tileRect.X = x * tileSize.Width / (scale * Astrometrics.ParsecScaleX);
                    tileRect.Y = y * tileSize.Height / (scale * Astrometrics.ParsecScaleY);
                    tileRect.Width = tileSize.Width / (scale * Astrometrics.ParsecScaleX);
                    tileRect.Height = tileSize.Height / (scale * Astrometrics.ParsecScaleY);

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

                    RenderContext ctx = new RenderContext(resourceManager, selector, tileRect, scale, options, styles, tileSize);
                    ctx.ClipOutsectorBorders = true;

                    ProduceResponse(Context, "Overview", ctx, tileSize);
                }
            }
        }
    }
}