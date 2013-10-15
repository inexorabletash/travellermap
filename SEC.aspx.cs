using System;
using System.IO;
using System.Net.Mime;
using System.Text;

namespace Maps.Pages
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    public class SEC : DataPage
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Plain; } }

        private void Page_Load(object sender, System.EventArgs e)
        {
            if (!ServiceConfiguration.CheckEnabled("sec", Response))
            {
                return;
            }

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            ResourceManager resourceManager = new ResourceManager(Server, Cache);
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);
            Sector sector;

            if (HasOption("sx") && HasOption("sy"))
            {
                int sx = GetIntOption("sx", 0);
                int sy = GetIntOption("sy", 0);

                sector = map.FromLocation(sx, sy);

                if (sector == null)
                {
                    SendError(404, "Not Found", String.Format("The sector at {0},{1} was not found.", sx, sy));
                    return;
                }
            }
            else if (HasOption("sector"))
            {
                string sectorName = GetStringOption("sector");
                sector = map.FromName(sectorName);

                if (sector == null)
                {
                    SendError(404, "Not Found", String.Format("The specified sector '{0}' was not found.", sectorName));
                    return;
                }
            }
            else
            {
                SendError(404, "Not Found", "No sector specified.");
                return;
            }

            WorldFilter filter = null;
            if (HasOption("subsector"))
            {
                string ss = GetStringOption("subsector");
                filter = (World world) => (world.SS == ss);
            }

            bool sscoords = GetBoolOption("sscoords", defaultValue: false);
            bool includeMetadata = GetBoolOption("metadata", defaultValue: true);
            bool includeHeader = GetBoolOption("header", defaultValue: true);

            string mediaType = GetStringOption("type");
            Encoding encoding;;
            switch (mediaType) {
                case "SecondSurvey":
                case "TabDelimited":
                    encoding = Util.UTF8_NO_BOM;
                    break;
                default:
                     encoding = Encoding.GetEncoding(1252);
                    break;
            }

            string data;
            using (var writer = new StringWriter())
            {
                // Content
                //
                sector.Serialize(resourceManager, writer, mediaType, includeMetadata:includeMetadata, includeHeader:includeHeader, sscoords: sscoords, filter:filter);
                data = writer.ToString();
            }
            SendResult(data, encoding);
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
