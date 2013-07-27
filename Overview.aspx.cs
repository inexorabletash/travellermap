using Maps.Rendering;
using System;
using System.Drawing;

namespace Maps.Pages
{
    public class Overview : ImageGeneratorPage
    {
        public const int MinDimension = 64;
        public const int MaxDimension = 2048;

        public const int NormalTileWidth = 256;
        public const int NormalTileHeight = 256;


        private void Page_Load(object sender, System.EventArgs e)
        {
            if (!AdminAuthorized())
                return;

            ResourceManager resourceManager = new ResourceManager(Server, Cache);

            MapOptions options = MapOptions.SectorGrid | MapOptions.FilledBorders;
            Stylesheet.Style style = Stylesheet.Style.Poster;
            ParseOptions(ref options, ref style);

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
            ctx.tiling = true;

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

            ProduceResponse("Overview", ctx, tileSize);
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
