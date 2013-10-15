using System;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;

namespace Maps.Pages
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    public class Errors : BasePage
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Plain; } }

        private static readonly Regex candidate = new Regex(@"(\w\w\w\w\w\w\w-\w|Error) \b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        private void Page_Load(object sender, System.EventArgs e)
        {
            if (!AdminAuthorized())
                return;

            Response.ContentType = MediaTypeNames.Text.Plain;

            ResourceManager resourceManager = new ResourceManager(Server, Cache);

            string sectorName = GetStringOption("sector");
            string type = GetStringOption("type");

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap.Flush();
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            var sectorQuery = from sector in map.Sectors
                              where (sectorName == null || sector.Names[0].Text.StartsWith(sectorName, ignoreCase: true, culture: CultureInfo.InvariantCulture))
                              && (sector.DataFile != null)
                              && (type == null || sector.DataFile.Type == type)
                              && (sector.Tags.Contains("OTU"))
                              orderby sector.Names[0].Text
                              select sector;

            foreach (var sector in sectorQuery)
            {
                Response.Output.WriteLine(sector.Names[0].Text);
#if DEBUG
				WorldCollection worlds = sector.GetWorlds( resourceManager, cacheResults: false );

                if( worlds != null )
                {
                    Response.Output.WriteLine("{0} world(s)", worlds.Count());
                    foreach (string s in worlds.ErrorList.Where(s => candidate.IsMatch(s)))
                    {
                        Response.Output.WriteLine(s);
                    }
                }
                else
                {
                    Response.Output.WriteLine("{0} world(s)", 0);
                }

                foreach (var route in sector.Routes)
                {
                    System.Drawing.Point startSector = sector.Location, endSector = sector.Location;
                    startSector.Offset(route.StartOffset);
                    endSector.Offset(route.EndOffset);

                    Location startLocation = new Location(startSector, route.Start);
                    Location endLocation = new Location(endSector, route.End);
                    int distance = Astrometrics.HexDistance(Astrometrics.LocationToCoordinates(startLocation),
                        Astrometrics.LocationToCoordinates(endLocation));
                    if (distance > 4)
                    {
                        Response.Output.WriteLine("Route length {0}: {1}", distance, route.ToString());
                    }
/*
 * This fails because of routes that use e.g. 3341-style coordinates
 * It will also be extremely slow due to loading world lists w/o caching
                    {
                        var w = map.FromLocation(startSector).GetWorlds(resourceManager, cacheResults: false);
                        if (w != null)
                        {
                            if (w[route.StartPoint.X, route.StartPoint.Y] == null)
                            {
                                Response.Output.WriteLine("Route start empty hex: {0}", route.ToString());
                            }
                        }
                    }
                    {
                        var w = map.FromLocation(endSector).GetWorlds(resourceManager, cacheResults: false);
                        if (w != null)
                        {
                            if (w[route.EndPoint.X, route.EndPoint.Y] == null)
                            {
                                Response.Output.WriteLine("Route end empty hex: {0}", route.ToString());
                            }
                        }
                    }
                    */
                }
#endif
                Response.Output.WriteLine();
            }
            return;
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
