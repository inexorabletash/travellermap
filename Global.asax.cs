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

        public RegexRoute(string pattern, Type t, RouteValueDictionary defaults = null, bool caseInsensitive = false)
            : base(null, defaults, new GenericRouteHandler(t))
        {
            if (!pattern.StartsWith("^") || !pattern.EndsWith("$"))
                throw new ApplicationException("RegexRoute pattern should be pinned with ^..$: " + pattern);

            RegexOptions options = RegexOptions.Compiled | RegexOptions.ExplicitCapture;
            if (caseInsensitive) options = options | RegexOptions.IgnoreCase;

            regex = new Regex(pattern, options);
        }

        public RegexRoute(string pattern, IRouteHandler handler, RouteValueDictionary defaults = null, bool caseInsensitive = false)
            : base(null, defaults, handler)
        {
            if (!pattern.StartsWith("^") || !pattern.EndsWith("$"))
                throw new ApplicationException("RegexRoute pattern should be pinned with ^..$: " + pattern);

            RegexOptions options = RegexOptions.Compiled | RegexOptions.ExplicitCapture;
            if (caseInsensitive) options = options | RegexOptions.IgnoreCase;

            regex = new Regex(pattern, options);
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

            foreach (var name in regex.GetGroupNames())
            {
                data.Values[name] = match.Groups[name];
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

            routes.Add(new RegexRoute(@"^/admin/admin$", typeof(AdminHandler)));
            routes.Add(new RegexRoute(@"^/admin/flush$", typeof(AdminHandler),
                new RouteValueDictionary(new { action = "flush" })));
            routes.Add(new RegexRoute(@"^/admin/reindex$", typeof(AdminHandler),
                new RouteValueDictionary(new { action = "reindex" })));
            routes.Add(new RegexRoute(@"^/admin/codes$", typeof(CodesHandler)));
            routes.Add(new RegexRoute(@"^/admin/dump$", typeof(DumpHandler)));
            routes.Add(new RegexRoute(@"^/admin/errors$", typeof(ErrorsHandler)));
            routes.Add(new RegexRoute(@"^/admin/overview$", typeof(OverviewHandler)));

            // See: http://stackoverflow.com/questions/3001009/output-caching-in-http-handler-and-setvaliduntilexpires
            // to configure caching when moving pages to handlers:
            // context.Response.Cache.VaryByParam["*"] = true;
            // context.Response.Cache.VaryByHeaders["Accept"] = true;

            // Search
            routes.Add(new RegexRoute(@"^/api/search$", typeof(SearchHandler), DEFAULT_JSON));

            // Rendering
            // TODO: Migrate from pages - but add ASPX routes (case-insensitively)

            // Location Queries
            routes.Add(new RegexRoute(@"^/api/coordinates$", typeof(CoordinatesHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/credits$", typeof(CreditsHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/jumpworlds$", typeof(JumpWorldsHandler), DEFAULT_JSON));

            routes.Add(new RegexRoute(@"^/Coordinates.aspx$", typeof(CoordinatesHandler), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/Credits.aspx$", typeof(CreditsHandler), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/JumpWorlds.aspx$", typeof(JumpWorldsHandler), caseInsensitive: true));

            // Data Retrieval - API-centric
            // TODO: Migrate from pages - but add ASPX routes (case-insensitively)
            routes.Add(new RegexRoute(@"^/api/universe$", typeof(UniverseHandler), DEFAULT_JSON));

            routes.Add(new RegexRoute(@"^/api/sec$", typeof(SECHandler), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"^/api/sec/(?<sector>[^/]+)$", typeof(SECHandler), new RouteValueDictionary { { "type", "SecondSurvey" } }));

            routes.Add(new RegexRoute(@"^/SEC.aspx$", typeof(SECHandler), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/Universe.aspx$", typeof(UniverseHandler), caseInsensitive: true));


            // Data Retrieval - RESTful
            // TODO: Migrate from pages
            routes.Add(new RegexRoute(@"^/data$", typeof(UniverseHandler), DEFAULT_JSON));

            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)$", typeof(SECHandler), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/sec$", typeof(SECHandler)));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/tab$", typeof(SECHandler), new RouteValueDictionary { { "type", "TabDelimited" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/coordinates$", typeof(CoordinatesHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/credits$", typeof(CreditsHandler), DEFAULT_JSON));

            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])$", typeof(SECHandler), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])/sec$", typeof(SECHandler), new RouteValueDictionary { { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])/tab$", typeof(SECHandler), new RouteValueDictionary { { "type", "TabDelimited" }, { "metadata", "0" } }));

            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>\d\d\d\d)$", typeof(JumpWorldsHandler), new RouteValueDictionary { { "accept", JsonConstants.MediaType }, { "jump", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>\d\d\d\d)/coordinates$", typeof(CoordinatesHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>\d\d\d\d)/credits$", typeof(CreditsHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>\d\d\d\d)/jump/(?<jump>\d+)$", typeof(JumpWorldsHandler), DEFAULT_JSON));

            // Legacy Aliases

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

            // Data Retrieval - API-centric
            mpr0("api/msec", BASE_DIR + "MSEC.aspx");
            mpr0("api/msec/{sector}", BASE_DIR + "MSEC.aspx");
            mpr1("api/metadata", BASE_DIR + "SectorMetaData.aspx", DEFAULT_JSON);
            mpr1("api/metadata/{sector}", BASE_DIR + "SectorMetaData.aspx", DEFAULT_JSON);

            // RESTful
            mpr0("data/{sector}/metadata", BASE_DIR + "SectorMetaData.aspx"); // TODO: JSON?
            mpr0("data/{sector}/msec", BASE_DIR + "MSEC.aspx");
            mpr0("data/{sector}/image", BASE_DIR + "Poster.aspx");

            // data/{sector}/{subsector}/foo conflicts with data/{sector}/{hex}/foo
            // so register data/{sector}/A ... data/{sector}/P instead
            // TODO: Support subsectors by name - will require manual delegation
            for (char s = 'A'; s <= 'P'; ++s)
            {
                string ss = s.ToString();
                Func<string, string> r = (pattern) => pattern.Replace("{subsector}", ss);

                mpr1(r("data/{sector}/{subsector}/image"), BASE_DIR + "Poster.aspx", new RouteValueDictionary { { "subsector", ss } });
            }

            mpr1("data/{sector}/{hex}/image", BASE_DIR + "JumpMap.aspx", new RouteValueDictionary { { "jump", "0" } });
            mpr0("data/{sector}/{hex}/jump/{jump}/image", BASE_DIR + "JumpMap.aspx");
        }
    }
}
