using Json;
using System;
using System.Web;
using System.Web.Routing;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Maps.Pages;

namespace Maps
{
    // From http://web.archive.org/web/20080401025712/http://www.iridescence.no/Posts/Defining-Routes-using-Regular-Expressions-in-ASPNET-MVC.aspx
    internal class RegexRoute : System.Web.Routing.Route
    {
        private readonly Regex regex;

        public RegexRoute(string pattern, IRouteHandler handler, RouteValueDictionary defaults = null)
            : base(null, defaults, handler)
        {
            regex = new Regex(pattern, RegexOptions.Compiled);
        }

        public override RouteData GetRouteData(HttpContextBase context)
        {
            Match match = regex.Match(context.Request.Path);
            if (!match.Success)
                return null;

            RouteData data = new RouteData(this, this.RouteHandler);

            if (Defaults != null)
            {
                foreach (var def in Defaults)
                {
                    data.Values[def.Key] = def.Value;
                }
            }

            for (int i = 1; i < match.Groups.Count; i++)
            {
                Group group = match.Groups[i];
                if (!group.Success)
                    continue;

                string key = regex.GroupNameFromNumber(i);
                if (String.IsNullOrEmpty(key) || Char.IsNumber(key, 0))
                    continue;

                data.Values[key] = group.Value;
            }

            return data;
        }
    }

    internal class GenericRouteHandler : IRouteHandler
    {
        private readonly Type type;

        public GenericRouteHandler(Type type)
        {
            this.type = type;
        }

        IHttpHandler IRouteHandler.GetHttpHandler(RequestContext context)
        {
            IHttpHandler handler = Activator.CreateInstance(type) as IHttpHandler;

            // Pass in RouteData
            // suggested by http://weblog.west-wind.com/posts/2011/Mar/28/Custom-ASPNET-Routing-to-an-HttpHandler
            context.HttpContext.Items["RouteData"] = context.RouteData;

            // Can be accessed in ProcessRequest via:
            // RouteData routeData = HttpContext.Current.Items["RouteData"] as RouteData;

            return handler;
        }
    }

    public class GlobalAsax : System.Web.HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            RegisterRoutes(RouteTable.Routes);
        }

        private static void RegisterRoutes(RouteCollection routes)
        {
            var DEFAULT_JSON = new RouteValueDictionary { { "accept", JsonConstants.MediaType } };

            // HttpHandler routing ------------------------------------------------------

            //routes.Add(new RegexRoute(@"^/foo/bar", 
            //                          new GenericRouteHandler(typeof(HandlerClass)),
            //                          new RouteValueDictionary(new { alpha = "beta" })
            //);

            routes.Add(new RegexRoute(@"^/admin/admin$", new GenericRouteHandler(typeof(AdminHandler))));
            routes.Add(new RegexRoute(@"^/admin/flush$", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "flush" })));
            routes.Add(new RegexRoute(@"^/admin/reindex$", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "reindex" })));
            routes.Add(new RegexRoute(@"^/admin/codes$", new GenericRouteHandler(typeof(CodesHandler))));
            routes.Add(new RegexRoute(@"^/admin/dump$", new GenericRouteHandler(typeof(DumpHandler))));
            routes.Add(new RegexRoute(@"^/admin/errors$", new GenericRouteHandler(typeof(ErrorsHandler))));
            routes.Add(new RegexRoute(@"^/admin/overview$", new GenericRouteHandler(typeof(OverviewHandler))));

            // See: http://stackoverflow.com/questions/3001009/output-caching-in-http-handler-and-setvaliduntilexpires
            // to configure caching when moving pages to handlers:
            // context.Response.Cache.VaryByParam["*"] = true;
            // context.Response.Cache.VaryByHeaders["Accept"] = true;

            routes.Add(new RegexRoute(@"/api/search$", new GenericRouteHandler(typeof(SearchHandler)), DEFAULT_JSON));

            // ASPX Page routing --------------------------------------------------------

            // Helpers, to avoid having to mint names for each route
            int routeNum = 0;
            Func<string> routeName = () => "route " + (routeNum++).ToString();

            Action<string, string> mpr0 =
                (url, file) => routes.MapPageRoute(routeName(), url, file);
            Action<string, string, RouteValueDictionary> mpr1 =
                (url, file, defaults) => routes.MapPageRoute(routeName(), url, file, checkPhysicalUrlAccess: false, defaults: defaults);

            const string BASE_DIR = @"~/server/pages/";

            // Rendering
            mpr0("api/poster", BASE_DIR + "Poster.aspx");
            mpr0("api/tile", BASE_DIR + "Tile.aspx");
            mpr0("api/jumpmap", BASE_DIR + "JumpMap.aspx");

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

                mpr1(r("data/{sector}/{subsector}"), BASE_DIR + "SEC.aspx", new RouteValueDictionary { { "subsector", ss }, { "type", "SecondSurvey" }, { "metadata", "0" } });
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
