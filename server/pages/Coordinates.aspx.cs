using System;
using System.Drawing;
using System.Xml.Serialization;

namespace Maps.Pages
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    /// 
    public class Coordinates : DataPage
    {
        protected override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }
        protected override string ServiceName { get { return "coordinates"; } }

        private void Page_Load(object sender, System.EventArgs e)
        {
            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            ResourceManager resourceManager = new ResourceManager(Server, Cache);
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            Location loc = new Location(map.FromName("Spinward Marches").Location, 1910);

            // Accept either sector [& hex] or sx,sy [& hx,hy]
            if (HasOption("sector"))
            {
                string sectorName = GetStringOption("sector");
                Sector sector = map.FromName(sectorName);
                if (sector == null)
                {
                    SendError(404, "Not Found", "Sector not found.");
                    return;
                }

                int hex = GetIntOption("hex", 0);
                loc = new Location(sector.Location, hex);
            }
            else if (HasOption("sx") && HasOption("sy"))
            {
                int sx = GetIntOption("sx", 0);
                int sy = GetIntOption("sy", 0);
                int hx = GetIntOption("hx", 0);
                int hy = GetIntOption("hy", 0);
                loc = new Location(map.FromLocation(sx, sy).Location, hx * 100 + hy);
            }
            else
            {
                SendError(404, "Not Found", "Must specify either sector name (and optional hex) or sx, sy (and optional hx, hy).");
                return;
            }

            Point coords = Astrometrics.LocationToCoordinates(loc);

            Result result = new Result();
            result.sx = loc.SectorLocation.X;
            result.sy = loc.SectorLocation.Y;
            result.hx = loc.HexLocation.X;
            result.hy = loc.HexLocation.Y;
            result.x = coords.X;
            result.y = coords.Y;
            SendResult(result);
        }

        [XmlRoot(ElementName = "Coordinates")]
        // public for XML serialization
        public class Result
        {
            // Sector/Hex
            public int sx { get; set; }
            public int sy { get; set; }
            public int hx { get; set; }
            public int hy { get; set; }

            // World-space X/Y
            public int x { get; set; }
            public int y { get; set; }
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
