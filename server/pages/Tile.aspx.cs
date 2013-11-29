using Maps.Rendering;
using System;
using System.Drawing;

namespace Maps.Pages
{
    public class Tile : ImageGeneratorPage
    {
        protected override string ServiceName { get { return "tile"; } }

        public const int MinDimension = 64;
        public const int MaxDimension = 2048;

        public const int NormalTileWidth = 256;
        public const int NormalTileHeight = 256;


        private void Page_Load(object sender, System.EventArgs e)
        {
            ResourceManager resourceManager = new ResourceManager(Server, Cache);

            MapOptions options = MapOptions.SectorGrid | MapOptions.BordersMajor | MapOptions.NamesMajor | MapOptions.NamesMinor;
            Stylesheet.Style style = Stylesheet.Style.Poster;
            ParseOptions(ref options, ref style);

            double x = GetDoubleOption("x", 0);
            double y = GetDoubleOption("y", 0);
            double scale = Util.Clamp(GetDoubleOption("scale", 0), MinScale, MaxScale);
            int width = Util.Clamp(GetIntOption("w", NormalTileWidth), MinDimension, MaxDimension);
            int height = Util.Clamp(GetIntOption("h", NormalTileHeight), MinDimension, MaxDimension);

            Size tileSize = new Size(width, height);

            RectangleF tileRect = new RectangleF();
            tileRect.X = (float)(x * tileSize.Width / (scale * Astrometrics.ParsecScaleX));
            tileRect.Y = (float)(y * tileSize.Height / (scale * Astrometrics.ParsecScaleY));
            tileRect.Width = (float)(tileSize.Width / (scale * Astrometrics.ParsecScaleX));
            tileRect.Height = (float)(tileSize.Height / (scale * Astrometrics.ParsecScaleY));

            DateTime dt = DateTime.Now;
            bool silly = (Math.Abs((int)x % 2) == Math.Abs((int)y % 2)) && (dt.Month == 4 && dt.Day == 1);
            silly = GetBoolOption("silly", silly);

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
            ctx.tiling = true;
            ProduceResponse("Tile", ctx, tileSize);
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
