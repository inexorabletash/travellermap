using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Xml.Serialization;

namespace Maps.Pages
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    public class Credits : DataPage
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }

        private void Page_Load(object sender, System.EventArgs e)
        {
            if (!ServiceConfiguration.CheckEnabled("credits", Response))
            {
                return;
            }

            ResourceManager resourceManager = new ResourceManager(Server, Cache);

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);
            Location loc = new Location(map.FromName("Spinward Marches").Location, 1910);

            if (HasOption("sector"))
            {
                string sectorName = GetStringOption("sector");
                Sector sec = map.FromName(sectorName);
                if (sec == null)
                {
                    SendError(404, "Not Found", "Sector not found.");
                    return;
                }

                int hex = GetIntOption("hex", Astrometrics.SectorCentralHex);
                loc = new Location(sec.Location, hex);
            }
            else if (HasOption("sx") && HasOption("sy"))
            {
                int sx = GetIntOption("sx", 0);
                int sy = GetIntOption("sy", 0);
                int hx = GetIntOption("hx", 0);
                int hy = GetIntOption("hy", 0);
                loc = new Location(map.FromLocation(sx, sy).Location, hx * 100 + hy);
            }
            else if (HasOption("x") && HasOption("y"))
            {
                loc = Astrometrics.CoordinatesToLocation(GetIntOption("x", 0), GetIntOption("y", 0));
            }

            if (loc.HexLocation.IsEmpty)
            {
                loc.HexLocation = new Point(Astrometrics.SectorWidth / 2, Astrometrics.SectorHeight / 2);
            }

            Sector sector = map.FromLocation(loc.SectorLocation.X, loc.SectorLocation.Y);

            Result data = new Result();

            if (sector != null)
            {
                // TODO: Multiple names
                foreach (var name in sector.Names.Take(1))
                {
                    data.SectorName = name.Text;
                }

                // Raw HTML credits
                data.Credits = sector.Credits == null ? null : sector.Credits.Trim();

                // Product info
                if (sector.Products.Count > 0)
                {
                    data.ProductPublisher = sector.Products[0].Publisher;
                    data.ProductTitle = sector.Products[0].Title;
                    data.ProductAuthor = sector.Products[0].Author;
                    data.ProductRef = sector.Products[0].Ref;
                }

                //
                // Sector Credits
                //
                if (sector.DataFile != null)
                {
                    data.SectorAuthor = sector.DataFile.Author;
                    data.SectorSource = sector.DataFile.Source;
                    data.SectorPublisher = sector.DataFile.Publisher;
                    data.SectorCopyright = sector.DataFile.Copyright;
                    data.SectorRef = sector.DataFile.Ref;
                    data.SectorEra = sector.DataFile.Era;
                }

                //
                // Subsector Credits
                //
                int ssx = (loc.HexLocation.X - 1) / Astrometrics.SubsectorWidth;
                int ssy = (loc.HexLocation.Y - 1) / Astrometrics.SubsectorHeight;
                int ssi = ssx + ssy * 4;
                Subsector ss = sector[ssi];
                if (ss != null)
                {
                    data.SubsectorIndex = ss.Index;
                    data.SubsectorName = ss.Name;
                }

                //
                // Routes Credits
                //


                //
                // World Data
                // 
                WorldCollection worlds = sector.GetWorlds(resourceManager);
                if (worlds != null)
                {
                    World world = worlds[loc.HexLocation.X, loc.HexLocation.Y];
                    if (world != null)
                    {
                        data.WorldName = world.Name;
                        data.WorldHex = world.Hex.ToString("0000", CultureInfo.InvariantCulture);
                        data.WorldUwp = world.UWP;
                    }
                }
            }

            SendResult(data);
        }

        [XmlRoot(ElementName = "Data")]
        // public for XML serialization
        public class Result
        {
            public string Credits { get; set; }

            public string SectorName { get; set; }
            public string SectorAuthor { get; set; }
            public string SectorSource { get; set; }
            public string SectorPublisher { get; set; }
            public string SectorCopyright { get; set; }
            public string SectorRef { get; set; }
            public string SectorEra { get; set; }

            public string RouteCredits { get; set; }

            public string SubsectorName { get; set; }
            public string SubsectorIndex { get; set; }
            public string SubsectorCredits { get; set; }

            public string WorldName { get; set; }
            public string WorldHex { get; set; }
            public string WorldUwp { get; set; }
            public string WorldCredits { get; set; }
            public string LandGrabTitle { get; set; }
            public string LandGrabURL { get; set; }

            public string ProductPublisher { get; set; }
            public string ProductTitle { get; set; }
            public string ProductAuthor { get; set; }
            public string ProductRef { get; set; }
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
