using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace Maps.Pages
{
    /// <summary>
    /// Fetch data about the universe.
    /// </summary>
    public class Universe : DataPage
    {
        protected override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }
        protected override string ServiceName { get { return "universe"; } }

        private void Page_Load(object sender, System.EventArgs e)
        {
            if (!ServiceConfiguration.CheckEnabled(ServiceName, Response))
            {
                return;
            }

            ResourceManager resourceManager = new ResourceManager(Server, Cache);

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            // Filter parameters
            string era = GetStringOption("era");
            bool requireData = GetBoolOption("requireData", defaultValue: false);

            Result data = new Result();
            foreach (Sector sector in map.Sectors)
            {
                if (requireData && sector.DataFile == null)
                    continue;

                if (era != null && (sector.DataFile == null || sector.DataFile.Era != era))
                    continue;

                SectorBase sb = new SectorBase(sector);
                data.Sectors.Add(sb);
            }

            SendResult(data);
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

        [XmlRoot(ElementName = "Universe")]
        // public for XML serialization
        public class Result
        {
            public Result()
            {
                Sectors = new List<SectorBase>();
            }

            [XmlElement("Sector")]
            public List<SectorBase> Sectors { get; set; }
        }
    }
}
