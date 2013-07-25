using System;
using System.Collections.Generic;
using System.Net.Mime;

namespace Maps.Pages
{
    /// <summary>
    /// Fetch data about the universe.
    /// </summary>
    public class Dump : BasePage
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Plain; } }

        private void Page_Load(object sender, System.EventArgs e)
        {
            if (!AdminAuthorized())
                return;

            ResourceManager resourceManager = new ResourceManager(Server, Cache);

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            Response.ContentType = MediaTypeNames.Text.Plain;

            foreach (Sector sector in map.Sectors)
            {
                WorldCollection worlds = sector.GetWorlds(resourceManager);
                if (worlds == null)
                    continue;
                foreach (World world in worlds)
                {
                    List<string> list = new List<string> {
                        sector.Names[0].Text,
                        sector.X.ToString(),
                        sector.Y.ToString(),
                        world.X.ToString(),
                        world.Y.ToString(),
                        world.Name,
                        world.UWP,
                        world.Bases,
                        world.Remarks,
                        world.PBG,
                        world.Allegiance,
                        world.Stellar
                    };
                    WriteCSV(list);
                }
            }
        }

        private void WriteCSV(List<string> values)
        {
            bool first = true;
            foreach (string value in values)
            {
                if (!first)
                {
                    Response.Output.Write(',');
                }
                else
                {
                    first = false;
                }

                if (value.IndexOf(',') == -1 && value.IndexOf('"') == -1)
                {
                    // plain
                    Response.Output.Write(value);
                }
                else
                {
                    // quoted
                    Response.Output.Write('"');
                    Response.Output.Write(value.Replace("\"", "\"\""));
                    Response.Output.Write('"');
                }
            }
            Response.Output.Write("\n");
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
