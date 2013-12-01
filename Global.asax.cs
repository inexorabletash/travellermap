using Json;
using Maps.Admin;
using Maps.API;
using System;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Routing;

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

            routes.Add(new RegexRoute(@"^/admin/admin$", typeof(AdminHandler)));
            routes.Add(new RegexRoute(@"^/admin/flush$", typeof(AdminHandler),
                new RouteValueDictionary(new { action = "flush" })));
            routes.Add(new RegexRoute(@"^/admin/reindex$", typeof(AdminHandler),
                new RouteValueDictionary(new { action = "reindex" })));
            routes.Add(new RegexRoute(@"^/admin/codes$", typeof(CodesHandler)));
            routes.Add(new RegexRoute(@"^/admin/dump$", typeof(DumpHandler)));
            routes.Add(new RegexRoute(@"^/admin/errors$", typeof(ErrorsHandler)));
            routes.Add(new RegexRoute(@"^/admin/overview$", typeof(OverviewHandler)));

            // Search
            routes.Add(new RegexRoute(@"^/api/search$", typeof(SearchHandler), DEFAULT_JSON));

            routes.Add(new RegexRoute(@"^/Search.aspx$", typeof(SearchHandler), caseInsensitive: true));

            // Rendering
            routes.Add(new RegexRoute(@"^/api/jumpmap$", typeof(JumpMapHandler)));
            routes.Add(new RegexRoute(@"^/api/poster$", typeof(PosterHandler)));
            routes.Add(new RegexRoute(@"^/api/tile$", typeof(TileHandler)));

            routes.Add(new RegexRoute(@"^/JumpMap.aspx$", typeof(JumpMapHandler), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/Poster.aspx$", typeof(PosterHandler), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/Tile.aspx$", typeof(TileHandler), caseInsensitive: true));

            // Location Queries
            routes.Add(new RegexRoute(@"^/api/coordinates$", typeof(CoordinatesHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/credits$", typeof(CreditsHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/jumpworlds$", typeof(JumpWorldsHandler), DEFAULT_JSON));

            routes.Add(new RegexRoute(@"^/Coordinates.aspx$", typeof(CoordinatesHandler), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/Credits.aspx$", typeof(CreditsHandler), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/JumpWorlds.aspx$", typeof(JumpWorldsHandler), caseInsensitive: true));

            // Data Retrieval - API-centric
            routes.Add(new RegexRoute(@"^/api/universe$", typeof(UniverseHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/sec$", typeof(SECHandler), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"^/api/sec/(?<sector>[^/]+)$", typeof(SECHandler), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"^/api/metadata$", typeof(SectorMetaDataHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/metadata/(?<sector>[^/]+)$", typeof(SectorMetaDataHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/api/msec$", typeof(MSECHandler)));
            routes.Add(new RegexRoute(@"^/api/msec/(?<sector>[^/]+)$", typeof(MSECHandler)));

            routes.Add(new RegexRoute(@"^/Universe.aspx$", typeof(UniverseHandler), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/SEC.aspx$", typeof(SECHandler), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/SectorMetaData.aspx$", typeof(SectorMetaDataHandler), caseInsensitive: true));
            routes.Add(new RegexRoute(@"^/MSEC.aspx$", typeof(MSECHandler), caseInsensitive: true));

            // Data Retrieval - RESTful
            routes.Add(new RegexRoute(@"^/data$", typeof(UniverseHandler), DEFAULT_JSON));

            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)$", typeof(SECHandler), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/sec$", typeof(SECHandler)));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/tab$", typeof(SECHandler), new RouteValueDictionary { { "type", "TabDelimited" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/coordinates$", typeof(CoordinatesHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/credits$", typeof(CreditsHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/metadata$", typeof(SectorMetaDataHandler))); // NOTE: XML by default 
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/msec$", typeof(MSECHandler)));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/image$", typeof(PosterHandler)));

            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])$", typeof(SECHandler), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])/sec$", typeof(SECHandler), new RouteValueDictionary { { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])/tab$", typeof(SECHandler), new RouteValueDictionary { { "type", "TabDelimited" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])/image$", typeof(PosterHandler)));

            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>\d\d\d\d)$", typeof(JumpWorldsHandler), new RouteValueDictionary { { "accept", JsonConstants.MediaType }, { "jump", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>\d\d\d\d)/coordinates$", typeof(CoordinatesHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>\d\d\d\d)/credits$", typeof(CreditsHandler), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>\d\d\d\d)/jump/(?<jump>\d+)$", typeof(JumpWorldsHandler), DEFAULT_JSON));

            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>\d\d\d\d)/image$", typeof(JumpMapHandler), new RouteValueDictionary { { "jump", "0" } }));
            routes.Add(new RegexRoute(@"^/data/(?<sector>[^/]+)/(?<hex>\d\d\d\d)/jump/(?<jump>\d+)/image$", typeof(JumpMapHandler)));

            // TODO: Support subsectors by name

        }
    }
}
