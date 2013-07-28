using System;
using System.Data.SqlClient;
using System.IO;
using System.Text;

namespace Maps.Pages
{
    public class Admin : BasePage
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Html; } }

        private void Page_Load(object sender, System.EventArgs e)
        {
            if (!AdminAuthorized())
                return;

            Server.ScriptTimeout = 3600; // An hour should be plenty
            Response.ContentType = System.Net.Mime.MediaTypeNames.Text.Html;
            Response.BufferOutput = false;

            Response.Write("<!DOCTYPE html>");
            Response.Write("<title>Admin Page</title>");
            Response.Write("<style>");
            using (var reader = new StreamReader(Server.MapPath("~/site.css"), Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    Response.Write(reader.ReadLine());
                }
            }
            Response.Write("</style>");
            Response.Write("<h1>Traveller Map - Administration</h1>");
            Response.Flush();

            string action = GetStringOption("action");
            switch (action)
            {
                case "reindex": Reindex(); return;
                case "flush": Flush(); return;
            }
            Write("Unknown action: <pre>" + action + "</pre>");
            Write("<b>&Omega;</b>");
        }

        private void Write(string line)
        {
            Response.Write("<div>");
            Response.Write(line);
            Response.Write("</div>");
            Response.Flush();
        }

        private void Flush()
        {
            SectorMap.Flush();
            Write("Sector map flushed.");
            Write("<b>&Omega;</b>");
        }

        private void Reindex()
        {

            Write("Initializing resource manager...");
            ResourceManager resourceManager = new ResourceManager(Server, Cache);

            SearchEngine.PopulateDatabase(resourceManager, Write);

            Write("&nbsp;");
            Write("Summary:");
            using (var connection = DBUtil.MakeConnection())
            {
                foreach (string table in new string[] { "sectors", "subsectors", "worlds" })
                {
                    string sql = String.Format("SELECT COUNT(*) FROM {0}", table);
                    using (var command = new SqlCommand(sql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Write(String.Format("{0}: {1}", table, reader.GetInt32(0)));
                            }
                        }
                    }
                }
            }

            Write("<b>&Omega;</b>");

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
