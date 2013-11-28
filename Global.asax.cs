using Json;
using System;
using System.Web.Routing;

namespace Maps
{
    public class GlobalAsax : System.Web.HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            RegisterRoutes(RouteTable.Routes);
        }

        private static void RegisterRoutes(RouteCollection routes)
        {
            var DEFAULT_JSON = new RouteValueDictionary { { "accept", JsonConstants.MediaType } };

            // Helpers, to avoid having to mint names for each route
            int routeNum = 0;
            Func<string> routeName = () => "route " + (routeNum++).ToString();

            Action<string, string> mpr0 = 
                (url, file) => routes.MapPageRoute(routeName(), url, file);
            Action<string, string, RouteValueDictionary> mpr1 =
                (url, file, defaults) => routes.MapPageRoute(routeName(), url, file, checkPhysicalUrlAccess: false, defaults: defaults);

            const string BASE_DIR = @"~/server/pages/";
            const string ADMIN_DIR = @"~/server/admin/";

            // Rendering
            mpr0("api/poster", BASE_DIR + "Poster.aspx");
            mpr0("api/tile", BASE_DIR + "Tile.aspx");
            mpr0("api/jumpmap", BASE_DIR + "JumpMap.aspx");

            // Search Queries
            mpr1("api/search", BASE_DIR + "Search.aspx", DEFAULT_JSON);

            // Location Queries
            mpr1("api/coordinates", BASE_DIR + "Coordinates.aspx", DEFAULT_JSON);
            mpr1("api/credits", BASE_DIR + "Credits.aspx", DEFAULT_JSON);
            mpr1("api/jumpworlds", BASE_DIR + "JumpWorlds.aspx", DEFAULT_JSON);

            // Data Retrieval - API-centric
            mpr1("api/sec", BASE_DIR + "SEC.aspx", new RouteValueDictionary { { "type", "SecondSurvey" } });
            mpr1("api/sec/{sector}", BASE_DIR + "SEC.aspx", new RouteValueDictionary { { "type", "SecondSurvey" } });
            mpr0("api/msec", BASE_DIR + "MSEC.aspx");
            mpr0("api/msec/{sector}", BASE_DIR + "MSEC.aspx");
            mpr1("api/metadata", BASE_DIR + "SectorMetaData.aspx", DEFAULT_JSON);
            mpr1("api/metadata/{sector}", BASE_DIR + "SectorMetaData.aspx", DEFAULT_JSON);
            mpr1("api/universe", BASE_DIR + "Universe.aspx", DEFAULT_JSON);

            // Admin
            mpr0("admin/overview", ADMIN_DIR + "Overview.aspx");

            // RESTful
            mpr1("data", BASE_DIR + "Universe.aspx", DEFAULT_JSON);

            mpr1("data/{sector}", BASE_DIR + "SEC.aspx", new RouteValueDictionary { { "type", "SecondSurvey" } });
            mpr0("data/{sector}/sec", BASE_DIR + "SEC.aspx");
            mpr1("data/{sector}/tab", BASE_DIR + "SEC.aspx", new RouteValueDictionary { { "type", "TabDelimited" } });
            mpr0("data/{sector}/metadata", BASE_DIR + "SectorMetaData.aspx"); // TODO: JSON?
            mpr0("data/{sector}/msec", BASE_DIR + "MSEC.aspx");
            mpr0("data/{sector}/image", BASE_DIR + "Poster.aspx");
            mpr1("data/{sector}/coordinates", BASE_DIR + "Coordinates.aspx", DEFAULT_JSON);
            mpr1("data/{sector}/credits", BASE_DIR + "Credits.aspx", DEFAULT_JSON);

            // data/{sector}/{subsector}/foo conflicts with data/{sector}/{hex}/foo
            // so register data/{sector}/A ... data/{sector}/P instead
            // TODO: Support subsectors by name - will require manual delegation
            for (char s = 'A'; s <= 'P'; ++s)
            {
                string ss = s.ToString();
                Func<string, string> r = (pattern) => pattern.Replace("{subsector}", ss);
 
                mpr1(r("data/{sector}/{subsector}"), BASE_DIR + "SEC.aspx", new RouteValueDictionary { { "subsector", ss }, { "type", "SecondSurvey" }, {"metadata", "0"} });
                mpr1(r("data/{sector}/{subsector}/sec"), BASE_DIR + "SEC.aspx", new RouteValueDictionary { { "subsector", ss }, { "metadata", "0" } });
                mpr1(r("data/{sector}/{subsector}/tab"), BASE_DIR + "SEC.aspx", new RouteValueDictionary { { "subsector", ss }, { "type", "TabDelimited" }, { "metadata", "0" } });
                mpr1(r("data/{sector}/{subsector}/image"), BASE_DIR + "Poster.aspx", new RouteValueDictionary { { "subsector", ss } });
            }

            mpr1("data/{sector}/{hex}", BASE_DIR + "JumpWorlds.aspx", new RouteValueDictionary { { "accept", JsonConstants.MediaType }, { "jump", "0" } });
            mpr1("data/{sector}/{hex}/image", BASE_DIR + "JumpMap.aspx", new RouteValueDictionary { { "jump", "0" } });
            mpr1("data/{sector}/{hex}/coordinates", BASE_DIR + "Coordinates.aspx", DEFAULT_JSON);
            mpr1("data/{sector}/{hex}/credits", BASE_DIR + "Credits.aspx", DEFAULT_JSON);
            mpr1("data/{sector}/{hex}/jump/{jump}", BASE_DIR + "JumpWorlds.aspx", DEFAULT_JSON);
            mpr0("data/{sector}/{hex}/jump/{jump}/image", BASE_DIR + "JumpMap.aspx");
        }
    }
}
