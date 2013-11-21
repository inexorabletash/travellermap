using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace Maps.Pages
{
    /// <summary>
    /// Summary description for WebForm1.
    /// </summary>

    public class JumpWorlds : DataPage
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }

        private void Page_Load(object sender, System.EventArgs e)
        {
            if (!ServiceConfiguration.CheckEnabled("jumpworlds", Response))
            {
                return;
            }

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            ResourceManager resourceManager = new ResourceManager(Server, Cache);

            //
            // Jump
            //
            int jump = Util.Clamp(GetIntOption("jump", 6), 0, 12);

            //
            // Coordinates
            //
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);
            Location loc = new Location(map.FromName("Spinward Marches").Location, 1910);

            if (HasOption("sector") && HasOption("hex"))
            {
                string sectorName = GetStringOption("sector");
                int hex = GetIntOption("hex", 0);
                loc = new Location(map.FromName(sectorName).Location, hex);
            }
            else if (HasOption("sx") && HasOption("sy") && HasOption("hx") && HasOption("hy"))
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

            Selector selector = new HexSelector(map, resourceManager, loc, jump);

            Result data = new Result();
            data.Worlds.AddRange(selector.Worlds);
            SendResult(data);
        }


        [XmlRoot(ElementName = "JumpWorlds")]
        // public for XML serialization
        public class Result
        {
            public Result()
            {
                Worlds = new List<World>();
            }

            [XmlElement("World")]
            public List<World> Worlds { get; set; }
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
