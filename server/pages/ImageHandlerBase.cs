using Maps.Rendering;
using System.Drawing;
using System.Web;

namespace Maps.Pages
{
    public abstract class ImageHandlerBase : DataHandlerBase
    {
        public override string DefaultContentType { get { return Util.MediaTypeName_Image_Png; } }

        public const double MinScale = 0.0078125; // Math.Pow(2, -7);
        public const double MaxScale = 512; // Math.Pow(2, 9);

        protected void ProduceResponse(HttpContext context, string title, Render.RenderContext ctx, Size tileSize,
            int rot = 0, float translateX = 0, float translateY = 0,
            bool transparent = false)
        {
            ImageGeneratorPage.SetCommonResponseHeaders(context);
            ImageGeneratorPage.ProduceResponse(context, this, title, ctx, tileSize, rot, translateX, translateY, transparent,
                (context.Items["RouteData"] as System.Web.Routing.RouteData).Values);
        }
    }
}
