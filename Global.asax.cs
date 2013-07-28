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

            // Rendering
            mpr0("api/poster", "~/Poster.aspx");
            mpr0("api/tile", "~/Tile.aspx");
            mpr0("api/jumpmap", "~/JumpMap.aspx");
            mpr0("api/overview", "~/Overview.aspx");

            // Search Queries
            mpr1("api/search", "~/Search.aspx", DEFAULT_JSON);

            // Location Queries
            mpr1("api/coordinates", "~/Coordinates.aspx", DEFAULT_JSON);
            mpr1("api/credits", "~/Credits.aspx", DEFAULT_JSON);
            mpr1("api/jumpworlds", "~/JumpWorlds.aspx", DEFAULT_JSON);

            // Data Retrieval - API-centric
            mpr1("api/sec", "~/SEC.aspx", new RouteValueDictionary { { "type", "SecondSurvey" } });
            mpr1("api/sec/{sector}", "~/SEC.aspx", new RouteValueDictionary { { "type", "SecondSurvey" } });
            mpr0("api/msec", "~/MSEC.aspx");
            mpr0("api/msec/{sector}", "~/MSEC.aspx");
            mpr1("api/metadata", "~/SectorMetaData.aspx", DEFAULT_JSON);
            mpr1("api/metadata/{sector}", "~/SectorMetaData.aspx", DEFAULT_JSON);
            mpr1("api/universe", "~/Universe.aspx", DEFAULT_JSON);

            // Admin
            mpr0("admin/admin", "~/admin/Admin.aspx");
            mpr0("admin/codes", "~/admin/Codes.aspx");
            mpr0("admin/dump", "~/admin/Dump.aspx");
            mpr0("admin/errors", "~/admin/Errors.aspx");
            mpr0("admin/overview", "~/admin/Overview.aspx");

            // RESTful
            mpr1("data", "~/Universe.aspx", DEFAULT_JSON);

            mpr1("data/{sector}", "~/SEC.aspx", new RouteValueDictionary { { "type", "SecondSurvey" } });
            mpr0("data/{sector}/sec", "~/SEC.aspx");
            mpr1("data/{sector}/tab", "~/SEC.aspx", new RouteValueDictionary { { "type", "TabDelimited" } });
            mpr0("data/{sector}/metadata", "~/SectorMetaData.aspx"); // TODO: JSON?
            mpr0("data/{sector}/msec", "~/MSEC.aspx");
            mpr0("data/{sector}/image", "~/Poster.aspx");
            mpr1("data/{sector}/coordinates", "~/Coordinates.aspx", DEFAULT_JSON);
            mpr1("data/{sector}/credits", "~/Credits.aspx", DEFAULT_JSON);

            // data/{sector}/{subsector}/foo conflicts with data/{sector}/{hex}/foo
            // so register data/{sector}/A ... data/{sector}/P instead
            // TODO: Support subsectors by name - will require manual delegation
            for (char s = 'A'; s <= 'P'; ++s)
            {
                string ss = s.ToString();
                Func<string, string> r = (pattern) => pattern.Replace("{subsector}", ss);
 
                mpr1(r("data/{sector}/{subsector}"), "~/SEC.aspx", new RouteValueDictionary { { "subsector", ss }, { "type", "SecondSurvey" }, {"metadata", "0"} });
                mpr1(r("data/{sector}/{subsector}/sec"), "~/SEC.aspx", new RouteValueDictionary { { "subsector", ss }, { "metadata", "0" } });
                mpr1(r("data/{sector}/{subsector}/tab"), "~/SEC.aspx", new RouteValueDictionary { { "subsector", ss }, { "type", "TabDelimited" }, { "metadata", "0" } });
                mpr1(r("data/{sector}/{subsector}/image"), "~/Poster.aspx", new RouteValueDictionary { { "subsector", ss } });
            }

            mpr1("data/{sector}/{hex}", "~/JumpWorlds.aspx", new RouteValueDictionary { { "accept", JsonConstants.MediaType }, { "jump", "0" } });
            mpr1("data/{sector}/{hex}/image", "~/JumpMap.aspx", new RouteValueDictionary { { "jump", "0" } });
            mpr1("data/{sector}/{hex}/coordinates", "~/Coordinates.aspx", DEFAULT_JSON);
            mpr1("data/{sector}/{hex}/credits", "~/Credits.aspx", DEFAULT_JSON);
            mpr1("data/{sector}/{hex}/jump/{jump}", "~/JumpWorlds.aspx", DEFAULT_JSON);
            mpr0("data/{sector}/{hex}/jump/{jump}/image", "~/JumpMap.aspx");
        }
    }
}
