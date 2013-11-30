using Maps.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Web.UI.WebControls;
using System.Xml.Serialization;

namespace Maps.Pages
{
    /// <summary>
    /// Summary description for MobilePage.
    /// </summary>
    public class MobilePage : DataPage
    {
        protected override string DefaultContentType { get { throw new NotImplementedException(); } }
        protected override string ServiceName { get { return "mobile"; } }

        protected System.Web.UI.HtmlControls.HtmlForm Form1;
        protected System.Web.UI.WebControls.ImageMap MapImage;
        protected System.Web.UI.WebControls.Button ButtonScrollCoreward;
        protected System.Web.UI.WebControls.Button ButtonScrollSpinward;
        protected System.Web.UI.WebControls.Button ButtonScrollTrailing;
        protected System.Web.UI.WebControls.Button ButtonScrollRimward;
        protected System.Web.UI.WebControls.Button ButtonZoomIn;
        protected System.Web.UI.WebControls.Button ButtonZoomOut;
        protected System.Web.UI.WebControls.TextBox TextBoxSearch;
        protected System.Web.UI.WebControls.Button ButtonSearch;
        protected System.Web.UI.WebControls.Button ButtonJump;
        protected System.Web.UI.WebControls.DataList ResultsDataList;
        protected System.Web.UI.WebControls.Label LabelNoResults;
        protected System.Web.UI.WebControls.DropDownList DropDownTileSize;

        private const int SmallTileDimension = 128;
        private const int LargeTileDimension = 192;
        private const int MaxTileDimension = 512;

        private string MakeURL()
        {
            double x = (double)ViewState["x"];
            double y = (double)ViewState["y"];
            int w = (int)ViewState["w"];
            int h = (int)ViewState["h"];
            double scale = (double)ViewState["scale"];

            double tx = (x * (scale * Astrometrics.ParsecScaleX) - (w / 2)) / w;
            double ty = (y * (scale * Astrometrics.ParsecScaleY) - (h / 2)) / h;

            return "/api/tile" +
                "?x=" + tx +
                "&y=" + ty +
                "&w=" + w +
                "&h=" + h +
                "&scale=" + scale +
                "&options=" + (int)(MapOptions)ViewState["options"] +
                "&style=" + ViewState["style"];
        }

        private void Scroll(double dx, double dy)
        {
            double x = (double)ViewState["x"];
            double y = (double)ViewState["y"];
            int w = (int)ViewState["w"];
            int h = (int)ViewState["h"];
            double scale = (double)ViewState["scale"];

            ViewState["x"] = x + (dx / scale) * w / Astrometrics.ParsecScaleX;
            ViewState["y"] = y + (dy / scale) * h / Astrometrics.ParsecScaleY;

            Refresh();
        }

        private void ZoomIn()
        {
            SetScale((double)ViewState["scale"] * 2);
        }

        private void ZoomOut()
        {
            SetScale((double)ViewState["scale"] / 2);
        }

        private void SetScale(double scale)
        {
            if (scale < ImageGeneratorPage.MinScale)
                scale = ImageGeneratorPage.MinScale;
            if (scale > ImageGeneratorPage.MaxScale)
                scale = ImageGeneratorPage.MaxScale;

            ViewState["scale"] = scale;
            Refresh();
        }

        private void SetTileSize(int value)
        {
            ViewState["w"] = value;
            ViewState["h"] = value;

            Refresh();
        }

        private void Refresh()
        {
            MapImage.ImageUrl = MakeURL();

            MapImage.BackColor = Color.Black;

            int w = (int)ViewState["w"];
            int h = (int)ViewState["h"];

            MapImage.Width = w;
            MapImage.Height = h;

            int w1 = w / 3;
            int w2 = w * 2 / 3;
            int h1 = h / 3;
            int h2 = h * 2 / 3;

            foreach (HotSpot hotSpot in MapImage.HotSpots)
            {
                RectangleHotSpot rect = hotSpot as RectangleHotSpot;
                if (rect == null)
                    return;
                switch (rect.PostBackValue)
                {
                    case "0": rect.Left = 0; rect.Right = w1; rect.Top = 0; rect.Bottom = h1; break;
                    case "1": rect.Left = w1; rect.Right = w2; rect.Top = 0; rect.Bottom = h1; break;
                    case "2": rect.Left = w2; rect.Right = w; rect.Top = 0; rect.Bottom = h1; break;
                    case "3": rect.Left = 0; rect.Right = w1; rect.Top = h1; rect.Bottom = h2; break;
                    case "4": rect.Left = w1; rect.Right = w2; rect.Top = h1; rect.Bottom = h2; break;
                    case "5": rect.Left = w2; rect.Right = w; rect.Top = h1; rect.Bottom = h2; break;
                    case "6": rect.Left = 0; rect.Right = w1; rect.Top = h2; rect.Bottom = h; break;
                    case "7": rect.Left = w1; rect.Right = w2; rect.Top = h2; rect.Bottom = h; break;
                    case "8": rect.Left = w2; rect.Right = w; rect.Top = h2; rect.Bottom = h; break;
                }
            }
        }

        private void Page_Load(object sender, System.EventArgs e)
        {
            // Put user code to initialize the page here

            // Hitting Enter in the text field should do "Search" - this works cross-browser
            this.TextBoxSearch.Attributes.Add(
                "onKeyPress",
                "if((event.keyCode||event.which)==13){document.getElementById('" + ButtonSearch.ClientID + "').click();return false;}");

            // Make Search the default button
            ClientScript.RegisterHiddenField("__EVENTTARGET", ButtonSearch.ID);

            if (!this.IsPostBack)
            {
                // ViewState should be pay-for-play - it's enabled on first search
                Form1.EnableViewState = false;

                ViewState["x"] = -0.5;
                ViewState["y"] = -0.5;
                ViewState["scale"] = 1.0;
                ViewState["options"] = MapOptions.SectorGrid | MapOptions.SubsectorGrid | MapOptions.SectorsAll | MapOptions.BordersMajor | MapOptions.BordersMinor | MapOptions.NamesMajor | MapOptions.WorldsCapitals | MapOptions.WorldsHomeworlds;
                ViewState["style"] = "poster";

                if (Request.Cookies["MobileMapSize"] != null)
                {
                    string val = Request.Cookies["MobileMapSize"].Value;

                    // back compat
                    if (val == "Large") val = "192";
                    if (val == "Small") val = "128";
                    this.DropDownTileSize.SelectedValue = val;
                }
                else
                {
                    this.DropDownTileSize.SelectedValue = LargeTileDimension.ToString(CultureInfo.InvariantCulture);
                }
            }

            int dim;
            Int32.TryParse(DropDownTileSize.SelectedValue, out dim);
            dim = Util.Clamp(dim, SmallTileDimension, MaxTileDimension);

            ViewState["w"] = dim;
            ViewState["h"] = dim;

            Refresh();
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
            this.ButtonScrollCoreward.Click += new System.EventHandler(this.BtnCoreward_Click);
            this.ButtonScrollSpinward.Click += new System.EventHandler(this.BtnSpinward_Click);
            this.ButtonScrollTrailing.Click += new System.EventHandler(this.BtnTrailing_Click);
            this.ButtonScrollRimward.Click += new System.EventHandler(this.BtnRimward_Click);
            this.ButtonZoomIn.Click += new System.EventHandler(this.BtnZoomIn_Click);
            this.ButtonZoomOut.Click += new System.EventHandler(this.BtnZoomOut_Click);
            this.ButtonSearch.Click += new System.EventHandler(this.BtnSearch_Click);
            this.ButtonJump.Click += new System.EventHandler(this.BtnJump_Click);
            this.ResultsDataList.ItemCommand += new System.Web.UI.WebControls.DataListCommandEventHandler(this.ResultsDataList_ItemCommand);
            this.DropDownTileSize.SelectedIndexChanged += new EventHandler(DropDownTileSize_SelectedIndexChanged);
            this.Load += new System.EventHandler(this.Page_Load);
        }
        #endregion

        private const double SCROLL_OFFSET = 0.4;

        private void BtnCoreward_Click(object sender, System.EventArgs e)
        {
            Scroll(0, -SCROLL_OFFSET);
        }

        private void BtnSpinward_Click(object sender, System.EventArgs e)
        {
            Scroll(-SCROLL_OFFSET, 0);
        }

        private void BtnTrailing_Click(object sender, System.EventArgs e)
        {
            Scroll(SCROLL_OFFSET, 0);
        }

        private void BtnRimward_Click(object sender, System.EventArgs e)
        {
            Scroll(0, SCROLL_OFFSET);
        }

        private void BtnZoomIn_Click(object sender, System.EventArgs e)
        {
            ZoomIn();
        }

        private void BtnZoomOut_Click(object sender, System.EventArgs e)
        {
            ZoomOut();
        }

        private void BtnSearch_Click(object sender, System.EventArgs e)
        {
            // ViewState should be pay-for-play - it's enabled on first search
            Form1.EnableViewState = true;

            string query = TextBoxSearch.Text;
            if (String.IsNullOrEmpty(query))
                return;

            ResourceManager resourceManager = new ResourceManager(Server, Cache);
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            IEnumerable<ItemLocation> results = SearchEngine.PerformSearch(query, resourceManager, SearchEngine.SearchResultsType.Default, 20);

            DataTable dt = new DataTable();
            dt.Locale = CultureInfo.InvariantCulture;
            dt.Columns.Add(new DataColumn("Name", typeof(string)));
            dt.Columns.Add(new DataColumn("Details", typeof(string)));
            dt.Columns.Add(new DataColumn("Data", typeof(string)));

            if (results != null)
            {
                foreach (object o in results)
                {
                    StringWriter sw = new StringWriter(CultureInfo.InvariantCulture);
                    // NOTE: Serialize explicitly using base class, so deserialization handles heterogenous types
                    XmlSerializer xs = new XmlSerializer(typeof(ItemLocation));
                    xs.Serialize(sw, o);
                    string xml = sw.ToString();

                    DataRow dr = dt.NewRow();
                    if (o is WorldLocation)
                    {
                        Sector sector;
                        World world;
                        ((WorldLocation)o).Resolve(map, resourceManager, out sector, out world);

                        dr[0] = world.Name;
                        dr[1] = String.Format(CultureInfo.InvariantCulture, "{0} {1}", sector.Names[0].Text, world.Hex.ToString("0000", CultureInfo.InvariantCulture));
                        dr[2] = xml;
                    }
                    else if (o is SubsectorLocation)
                    {
                        Sector sector;
                        Subsector subsector;
                        ((SubsectorLocation)o).Resolve(map, out sector, out subsector);

                        dr[0] = subsector.Name;
                        dr[1] = String.Format(CultureInfo.InvariantCulture, "Subsector {1} of {0}", sector.Names[0].Text, subsector.Index);
                        dr[2] = xml;
                    }
                    else if (o is SectorLocation)
                    {
                        Sector sector = ((SectorLocation)o).Resolve(map);

                        dr[0] = sector.Names[0].Text;
                        dr[1] = "Sector";
                        dr[2] = xml;

                    }
                    dt.Rows.Add(dr);
                }

                LabelNoResults.Visible = false;
            }
            else
            {
                LabelNoResults.Visible = true;
                LabelNoResults.Text = "No matches found";
            }

            this.ResultsDataList.DataSource = new DataView(dt);
            this.ResultsDataList.DataBind();
        }

        private void BtnJump_Click(object sender, System.EventArgs e)
        {
            string query = TextBoxSearch.Text;
            if (String.IsNullOrEmpty(query))
                return;

            ResourceManager resourceManager = new ResourceManager(Server, Cache);
            IEnumerable<ItemLocation> results = SearchEngine.PerformSearch(query, resourceManager, SearchEngine.SearchResultsType.Worlds, 20);

            // Clear previous search results, if any
            ResultsDataList.DataSource = null;
            ResultsDataList.DataBind();

            WorldLocation jump = null;
            double distanceSquared = Double.PositiveInfinity;

            if (results != null)
            {
                foreach (object o in results)
                {
                    WorldLocation worldLocation = o as WorldLocation;
                    if (worldLocation != null)
                    {
                        Point coords = Astrometrics.LocationToCoordinates(worldLocation.Sector, worldLocation.World);
                        double x = coords.X - 0.5;
                        double y = coords.Y - (((coords.X % 2) == 0) ? 0.5 : 0);

                        double dx = x - (double)ViewState["x"];
                        double dy = y - (double)ViewState["y"];

                        double d = dx * dx + dy * dy;

                        if (d < distanceSquared)
                        {
                            jump = worldLocation;
                            distanceSquared = d;
                        }
                    }
                }
            }


            if (jump != null)
            {
                Point coords = Astrometrics.LocationToCoordinates(jump.Sector, jump.World);
                double x = coords.X - 0.5;
                double y = coords.Y - (((coords.X % 2) == 0) ? 0.5 : 0);

                ViewState["x"] = x;
                ViewState["y"] = y;
                ViewState["scale"] = (double)64;

                Refresh();
            }
            else
            {
                LabelNoResults.Visible = true;
                LabelNoResults.Text = "No matches found";
            }
        }

        private void ResultsDataList_ItemCommand(object source, System.Web.UI.WebControls.DataListCommandEventArgs e)
        {
            string xml = e.CommandArgument as String;
            XmlSerializer xs = new XmlSerializer(typeof(ItemLocation));

            object o = xs.Deserialize(new StringReader(xml));

            if (o is WorldLocation)
            {
                WorldLocation worldLocation = o as WorldLocation;
                Point coords = Astrometrics.LocationToCoordinates(worldLocation.Sector, worldLocation.World);

                double x = coords.X - 0.5;
                double y = coords.Y - (((coords.X % 2) == 0) ? 0.5 : 0);

                ViewState["x"] = x;
                ViewState["y"] = y;
                ViewState["scale"] = (double)64;
            }
            else if (o is SubsectorLocation)
            {
                SubsectorLocation subsectorLocation = o as SubsectorLocation;

                int nIndex = (subsectorLocation.Index - 'A');
                int hx = (int)((Math.Floor(nIndex % 4.0) + 0.5) * (Astrometrics.SectorWidth / 4));
                int hy = (int)((Math.Floor(nIndex / 4.0) + 0.5) * (Astrometrics.SectorHeight / 4));

                Point coords = Astrometrics.LocationToCoordinates(subsectorLocation.SectorLocation, new Point(hx, hy));

                ViewState["x"] = (double)coords.X;
                ViewState["y"] = (double)coords.Y;
                ViewState["scale"] = (double)32;
            }
            else if (o is SectorLocation)
            {
                SectorLocation sectorLocation = o as SectorLocation;

                Point coords = Astrometrics.LocationToCoordinates(sectorLocation.SectorCoords, new Point(Astrometrics.SectorWidth / 2, Astrometrics.SectorHeight / 2));

                ViewState["x"] = (double)coords.X;
                ViewState["y"] = (double)coords.Y;
                ViewState["scale"] = (double)6;
            }

            Refresh();
        }

        protected void MapImage_Click(object sender, ImageMapEventArgs e)
        {
            switch (e.PostBackValue)
            {
                case "0": Scroll(-0.5, -0.5); break;
                case "1": Scroll(0, -0.5); break;
                case "2": Scroll(0.5, -0.5); break;
                case "3": Scroll(-0.5, 0); break;
                case "4": ZoomIn(); break;
                case "5": Scroll(0.5, 0); break;
                case "6": Scroll(-0.5, 0.5); break;
                case "7": Scroll(0, 0.5); break;
                case "8": Scroll(0.5, 0.5); break;
            }
        }

        protected void DropDownTileSize_SelectedIndexChanged(object sender, EventArgs e)
        {
            int dim;
            Int32.TryParse(DropDownTileSize.SelectedValue, out dim);
            dim = Util.Clamp(dim, SmallTileDimension, MaxTileDimension);

            SetTileSize(dim);

            Response.Cookies["MobileMapSize"].Value = dim.ToString(CultureInfo.InvariantCulture);
            Response.Cookies["MobileMapSize"].Expires = DateTime.MaxValue;
        }

    }
}
