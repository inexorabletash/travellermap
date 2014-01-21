#define LEGACY_ASPX

using Json;
using Maps.Admin;
using Maps.API;
using System;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Routing;
using System.Collections.Generic;

namespace Maps
{
    // From http://web.archive.org/web/20080401025712/http://www.iridescence.no/Posts/Defining-Routes-using-Regular-Expressions-in-ASPNET-MVC.aspx
    internal class RegexRoute : System.Web.Routing.Route
    {
        private readonly Regex regex;

        public RegexRoute(string pattern, IRouteHandler handler, RouteValueDictionary defaults = null, bool caseInsensitive = false)
            : base(null, defaults, handler)
        {
            if (!pattern.StartsWith("^") || !pattern.EndsWith("$"))
                throw new ApplicationException("RegexRoute pattern should be pinned with ^..$: " + pattern);

            RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
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

    internal class RedirectRouteHandler : IRouteHandler
    {
        private Regex replacer = new Regex(@"{(.*?)}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly string pattern;
        private readonly int statusCode;

        public RedirectRouteHandler(string target, int statusCode = 301)
        {
            this.pattern = target;
            this.statusCode = statusCode;
        }

        IHttpHandler IRouteHandler.GetHttpHandler(RequestContext requestContext)
        {
            RouteValueDictionary dict = requestContext.RouteData.Values;
            var url = replacer.Replace(pattern, new MatchEvaluator(m => dict[m.Groups[1].Value].ToString()));
            return new RedirectHandler(url, statusCode);
        }

        private class RedirectHandler : IHttpHandler
        {
            private readonly string url;
            private readonly int statusCode;
            public RedirectHandler(string url, int statusCode)
            {
                this.url = url;
                this.statusCode = statusCode;
            }

            bool IHttpHandler.IsReusable
            {
                get { return false; }
            }

            void IHttpHandler.ProcessRequest(HttpContext context)
            {
                context.Response.StatusCode = statusCode;
                context.Response.AddHeader("Location", url);
            }
        }
    }

    public class GlobalAsax : System.Web.HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            HttpContext.Current.Response.AddHeader("Access-Control-Allow-Origin", "*");

            if (HttpContext.Current.Request.HttpMethod == "OPTIONS")
            {
                //These headers are handling the "pre-flight" OPTIONS call sent by the browser
                HttpContext.Current.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE");
                HttpContext.Current.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept");
                HttpContext.Current.Response.AddHeader("Access-Control-Max-Age", "3600");
                HttpContext.Current.Response.End();
            }

            // Make ".html" suffix optional for certain pages
            if (s_extensionless.Contains(Context.Request.Path.ToLowerInvariant()))
            {
                Context.RewritePath(Context.Request.Path + ".html");
                return;
            }
        }

        private static HashSet<string> s_extensionless = new HashSet<string> {
            "/booklet",
            "/poster",
            "/world",
            "/old",
            "/doc/about",
            "/doc/api",
            "/doc/credits",
            "/doc/fileformats",
            "/doc/secondsurvey"
        };

        private static void RegisterRoutes(RouteCollection routes)
        {
            var DEFAULT_JSON = new RouteValueDictionary { { "accept", JsonConstants.MediaType } };

            // Navigation  --------------------------------------------------

            routes.Add(new RegexRoute(@"^/go/(?<sector>[^/]+)$", new RedirectRouteHandler("/?sector={sector}", statusCode: 302)));
            routes.Add(new RegexRoute(@"^/go/(?<sector>[^/]+)/(?<hex>[0-9]{4})$", new RedirectRouteHandler("/?sector={sector}&hex={hex}", statusCode: 302)));

            routes.Add(new RegexRoute(@"^/booklet/(?<sector>[^/]+)$", new RedirectRouteHandler("/booklet?sector={sector}", statusCode: 302)));
            routes.Add(new RegexRoute(@"^/sheet/(?<sector>[^/]+)/(?<hex>[0-9]{4})$", new RedirectRouteHandler("/world?sector={sector}&hex={hex}", statusCode: 302)));

            // Administration -----------------------------------------------

            routes.Add(new RegexRoute(@"^/admin/admin$", new GenericRouteHandler(typeof(AdminHandler))));
            routes.Add(new RegexRoute(@"^/admin/flush$", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "flush" })));
            routes.Add(new RegexRoute(@"^/admin/reindex$", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "reindex" })));
            routes.Add(new RegexRoute(@"^/admin/profile$", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "profile" })));
            routes.Add(new RegexRoute(@"^/admin/codes$", new GenericRouteHandler(typeof(CodesHandler))));
            routes.Add(new RegexRoute(@"^/admin/dump$", new GenericRouteHandler(typeof(DumpHandler))));
            routes.Add(new RegexRoute(@"^/admin/errors$", new GenericRouteHandler(typeof(ErrorsHandler))));
            routes.Add(new RegexRoute(@"^/admin/overview$", new GenericRouteHandler(typeof(OverviewHandler))));

            // Search -------------------------------------------------------

            routes.Add(new RegexRoute(@"^/api/search$", new GenericRouteHandler(typeof(SearchHandler)), DEFAULT_JSON));

#if LEGACY_ASPX
            routes.Add(new RegexRoute(@"^/Search.aspx$", new GenericRouteHandler(typeof(SearchHandler)), caseInsensitive: true));
#endif

            // Rendering ----------------------------------------------------
            
            routes.Add(new RegexRoute(@"^/api/jumpmap$", new GenericRouteHandler(typeof(JumpMapHandler))));
            routes.Add(new RegexRoute(@"^/api/poster$", new GenericRouteHandler(typeof(PosterHandler))));
            routes.Add(new RegexRoute(@"^/api/poster/(?<sector>[^/]+)$", new GenericRouteHandler(typeof(PosterHandler))));
            routes.Add(new RegexRoute(@"^/api/poster/(?<sector>[^/]+)/(?<subsector>[^/]+)$", new GenericRouteHandler(typeof(PosterHandler))));
            routes.Add(new RegexRoute(@"^/api/tile$", new GenericRouteHandler(typeof(TileHandler))));
#if LEGACY_ASPX
            routes.Add(new RegexRoute(@"^/JumpMap.aspx$", new GenericRouteHandler(typeof(JumpMapHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/Poster.aspx$", new GenericRouteHandler(typeof(PosterHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/Tile.aspx$", new GenericRouteHandler(typeof(TileHandler)), caseInsensitive: true));
#endif
            
            // Location Queries ---------------------------------------------
            
            routes.Add(new RegexRoute(@"^/api/coordinates$", new GenericRouteHandler(typeof(CoordinatesHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/credits$", new GenericRouteHandler(typeof(CreditsHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/jumpworlds$", new GenericRouteHandler(typeof(JumpWorldsHandler)), DEFAULT_JSON));
#if LEGACY_ASPX
            routes.Add(new RegexRoute(@"^/Coordinates.aspx$", new GenericRouteHandler(typeof(CoordinatesHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/Credits.aspx$", new GenericRouteHandler(typeof(CreditsHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/JumpWorlds.aspx$", new GenericRouteHandler(typeof(JumpWorldsHandler)), caseInsensitive: true));
#endif
            
            // Data Retrieval - API-centric ---------------------------------
            
            routes.Add(new RegexRoute(@"^/api/universe$", new GenericRouteHandler(typeof(UniverseHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/sec$", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"^/api/sec/(?<sector>[^/]+)$", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"^/api/sec/(?<sector>[^/]+)/(?<subsector>[^/]+)$", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", 0 } }));
            routes.Add(new RegexRoute(@"^/api/metadata$", new GenericRouteHandler(typeof(SectorMetaDataHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/metadata/(?<sector>[^/]+)$", new GenericRouteHandler(typeof(SectorMetaDataHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/msec$", new GenericRouteHandler(typeof(MSECHandler))));
            routes.Add(new RegexRoute(@"^/api/msec/(?<sector>[^/]+)$", new GenericRouteHandler(typeof(MSECHandler))));
#if LEGACY_ASPX
            routes.Add(new RegexRoute(@"^/Universe.aspx$", new GenericRouteHandler(typeof(UniverseHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/SEC.aspx$", new GenericRouteHandler(typeof(SECHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/SectorMetaData.aspx$", new GenericRouteHandler(typeof(SectorMetaDataHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/MSEC.aspx$", new GenericRouteHandler(typeof(MSECHandler)), caseInsensitive: true));
#endif
            
            // Data Retrieval - RESTful -------------------------------------
            
            routes.Add(new RegexRoute(@"^/data$", new GenericRouteHandler(typeof(UniverseHandler)), DEFAULT_JSON));

            // Sector, e.g. /data/Spinward Marches
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)$", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/sec$", new GenericRouteHandler(typeof(SECHandler))));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/tab$", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "TabDelimited" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/coordinates$", new GenericRouteHandler(typeof(CoordinatesHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/credits$", new GenericRouteHandler(typeof(CreditsHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/metadata$", new GenericRouteHandler(typeof(SectorMetaDataHandler)))); // NOTE: XML by default 
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/msec$", new GenericRouteHandler(typeof(MSECHandler))));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/image$", new GenericRouteHandler(typeof(PosterHandler))));

            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/booklet$", new RedirectRouteHandler("/booklet?sector={sector}", statusCode: 302)));

            // Subsector by Index, e.g. /data/Spinward Marches/C
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])$", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])/sec$", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])/tab$", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "TabDelimited" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])/image$", new GenericRouteHandler(typeof(PosterHandler))));

            // World e.g. /data/Spinward Marches/1910
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})$", new GenericRouteHandler(typeof(JumpWorldsHandler)), new RouteValueDictionary { { "accept", JsonConstants.MediaType }, { "jump", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/coordinates$", new GenericRouteHandler(typeof(CoordinatesHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/credits$", new GenericRouteHandler(typeof(CreditsHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/jump/(?<jump>\d+)$", new GenericRouteHandler(typeof(JumpWorldsHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/image$", new GenericRouteHandler(typeof(JumpMapHandler)), new RouteValueDictionary { { "jump", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/jump/(?<jump>\d+)/image$", new GenericRouteHandler(typeof(JumpMapHandler))));

            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/sheet$", new RedirectRouteHandler("/world?sector={sector}&hex={hex}", statusCode: 302)));

            // Subsector by Name e.g. /data/Spinward Marches/Regina
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[^/]+)$", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[^/]+)/sec$", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[^/]+)/tab$", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "TabDelimited" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[^/]+)/image$", new GenericRouteHandler(typeof(PosterHandler))));
        }
    }
}
