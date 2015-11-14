#define LEGACY_ASPX

using Json;
using Maps.Admin;
using Maps.API;
using Maps.HTTP;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Routing;

namespace Maps
{
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
            "/world",
            "/doc/about",
            "/doc/api",
            "/doc/credits",
            "/doc/custom",
            "/doc/fileformats",
            "/doc/metadata",
            "/doc/secondsurvey",
            "/doc/submit",
            "/make/borders",
            "/make/booklet",
            "/make/poster",
            "/make/routes",
            "/make/path",
        };

        private static void RegisterRoutes(RouteCollection routes)
        {
            var DEFAULT_JSON = new RouteValueDictionary { { "accept", JsonConstants.MediaType } };

            // Navigation  --------------------------------------------------

            routes.Add(new RegexRoute(@"/go/(?<sector>[^/]+)", new RedirectRouteHandler("/?sector={sector}", statusCode: 302)));
            routes.Add(new RegexRoute(@"/go/(?<sector>[^/]+)/(?<hex>[0-9]{4})", new RedirectRouteHandler("/?sector={sector}&hex={hex}", statusCode: 302)));
            routes.Add(new RegexRoute(@"/go/(?<sector>[^/]+)/(?<subsector>[^/]+)", new RedirectRouteHandler("/?sector={sector}&subsector={subsector}", statusCode: 302)));

            routes.Add(new RegexRoute(@"/booklet/(?<sector>[^/]+)", new RedirectRouteHandler("/make/booklet?sector={sector}", statusCode: 302)));
            routes.Add(new RegexRoute(@"/sheet/(?<sector>[^/]+)/(?<hex>[0-9]{4})", new RedirectRouteHandler("/world?sector={sector}&hex={hex}", statusCode: 302)));

            // Administration -----------------------------------------------

            routes.Add(new RegexRoute(@"/admin/admin", new GenericRouteHandler(typeof(AdminHandler))));
            routes.Add(new RegexRoute(@"/admin/flush", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "flush" })));
            routes.Add(new RegexRoute(@"/admin/reindex", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "reindex" })));
            routes.Add(new RegexRoute(@"/admin/profile", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "profile" })));
            routes.Add(new RegexRoute(@"/admin/codes", new GenericRouteHandler(typeof(CodesHandler))));
            routes.Add(new RegexRoute(@"/admin/dump", new GenericRouteHandler(typeof(DumpHandler))));
            routes.Add(new RegexRoute(@"/admin/errors", new GenericRouteHandler(typeof(ErrorsHandler))));
            routes.Add(new RegexRoute(@"/admin/overview", new GenericRouteHandler(typeof(OverviewHandler))));

            // Search -------------------------------------------------------

            routes.Add(new RegexRoute(@"/api/search", new GenericRouteHandler(typeof(SearchHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/route", new GenericRouteHandler(typeof(RouteHandler)), DEFAULT_JSON));

#if LEGACY_ASPX
            routes.Add(new RegexRoute(@"/Search.aspx", new GenericRouteHandler(typeof(SearchHandler)), caseInsensitive: true));
#endif

            // Rendering ----------------------------------------------------

            routes.Add(new RegexRoute(@"/api/jumpmap", new GenericRouteHandler(typeof(JumpMapHandler))));
            routes.Add(new RegexRoute(@"/api/poster", new GenericRouteHandler(typeof(PosterHandler))));
            routes.Add(new RegexRoute(@"/api/poster/(?<sector>[^/]+)", new GenericRouteHandler(typeof(PosterHandler))));
            routes.Add(new RegexRoute(@"/api/poster/(?<sector>[^/]+)/(?<quadrant>(?:alpha|beta|gamma|delta))", new GenericRouteHandler(typeof(PosterHandler))));
            routes.Add(new RegexRoute(@"/api/poster/(?<sector>[^/]+)/(?<subsector>[^/]+)", new GenericRouteHandler(typeof(PosterHandler))));
            routes.Add(new RegexRoute(@"/api/tile", new GenericRouteHandler(typeof(TileHandler))));
#if LEGACY_ASPX
            routes.Add(new RegexRoute(@"/JumpMap.aspx", new GenericRouteHandler(typeof(JumpMapHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"/Poster.aspx", new GenericRouteHandler(typeof(PosterHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"/Tile.aspx", new GenericRouteHandler(typeof(TileHandler)), caseInsensitive: true));
#endif

            // Location Queries ---------------------------------------------

            routes.Add(new RegexRoute(@"/api/coordinates", new GenericRouteHandler(typeof(CoordinatesHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/credits", new GenericRouteHandler(typeof(CreditsHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/jumpworlds", new GenericRouteHandler(typeof(JumpWorldsHandler)), DEFAULT_JSON));
#if LEGACY_ASPX
            routes.Add(new RegexRoute(@"/Coordinates.aspx", new GenericRouteHandler(typeof(CoordinatesHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"/Credits.aspx", new GenericRouteHandler(typeof(CreditsHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"/JumpWorlds.aspx", new GenericRouteHandler(typeof(JumpWorldsHandler)), caseInsensitive: true));
#endif

            // Data Retrieval - API-centric ---------------------------------

            routes.Add(new RegexRoute(@"/api/universe", new GenericRouteHandler(typeof(UniverseHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/sec", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"/api/sec/(?<sector>[^/]+)", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"/api/sec/(?<sector>[^/]+)/(?<quadrant>alpha|beta|gamma|delta)", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", 0 } }));
            routes.Add(new RegexRoute(@"/api/sec/(?<sector>[^/]+)/(?<subsector>[^/]+)", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", 0 } }));
            routes.Add(new RegexRoute(@"/api/metadata", new GenericRouteHandler(typeof(SectorMetaDataHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/metadata/(?<sector>[^/]+)", new GenericRouteHandler(typeof(SectorMetaDataHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/msec", new GenericRouteHandler(typeof(MSECHandler))));
            routes.Add(new RegexRoute(@"/api/msec/(?<sector>[^/]+)", new GenericRouteHandler(typeof(MSECHandler))));
#if LEGACY_ASPX
            routes.Add(new RegexRoute(@"/Universe.aspx", new GenericRouteHandler(typeof(UniverseHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"/SEC.aspx", new GenericRouteHandler(typeof(SECHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"/SectorMetaData.aspx", new GenericRouteHandler(typeof(SectorMetaDataHandler)), caseInsensitive: true));
            routes.Add(new RegexRoute(@"/MSEC.aspx", new GenericRouteHandler(typeof(MSECHandler)), caseInsensitive: true));
#endif

            // Data Retrieval - RESTful -------------------------------------

            routes.Add(new RegexRoute(@"/data", new GenericRouteHandler(typeof(UniverseHandler)), DEFAULT_JSON));

            // Sector, e.g. /data/Spinward Marches
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/sec", new GenericRouteHandler(typeof(SECHandler))));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/tab", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "TabDelimited" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/coordinates", new GenericRouteHandler(typeof(CoordinatesHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/credits", new GenericRouteHandler(typeof(CreditsHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/metadata", new GenericRouteHandler(typeof(SectorMetaDataHandler)))); // NOTE: XML by default 
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/msec", new GenericRouteHandler(typeof(MSECHandler))));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/image", new GenericRouteHandler(typeof(PosterHandler))));

            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/booklet", new RedirectRouteHandler("/make/booklet?sector={sector}", statusCode: 302)));

            // Quadrant, e.g. /data/Spinward Marches/Alpha
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<quadrant>alpha|beta|gamma|delta)", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<quadrant>alpha|beta|gamma|delta)/sec", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<quadrant>alpha|beta|gamma|delta)/tab", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "TabDelimited" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<quadrant>alpha|beta|gamma|delta)/image", new GenericRouteHandler(typeof(PosterHandler))));

            // Subsector by Index, e.g. /data/Spinward Marches/C
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])/sec", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])/tab", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "TabDelimited" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<subsector>[A-Pa-p])/image", new GenericRouteHandler(typeof(PosterHandler))));

            // World e.g. /data/Spinward Marches/1910
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})", new GenericRouteHandler(typeof(JumpWorldsHandler)), new RouteValueDictionary { { "accept", JsonConstants.MediaType }, { "jump", "0" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/coordinates", new GenericRouteHandler(typeof(CoordinatesHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/credits", new GenericRouteHandler(typeof(CreditsHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/jump/(?<jump>\d+)", new GenericRouteHandler(typeof(JumpWorldsHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/image", new GenericRouteHandler(typeof(JumpMapHandler)), new RouteValueDictionary { { "jump", "0" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/jump/(?<jump>\d+)/image", new GenericRouteHandler(typeof(JumpMapHandler))));

            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/sheet", new RedirectRouteHandler("/world?sector={sector}&hex={hex}", statusCode: 302)));

            // Subsector by Name e.g. /data/Spinward Marches/Regina
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<subsector>[^/]+)", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<subsector>[^/]+)/sec", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<subsector>[^/]+)/tab", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "TabDelimited" }, { "metadata", "0" } }));
            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<subsector>[^/]+)/image", new GenericRouteHandler(typeof(PosterHandler))));

            // T5SS Stock Data -------------------------------------

            routes.Add(new RegexRoute(@"/t5ss/allegiances", new GenericRouteHandler(typeof(AllegianceCodesHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/t5ss/sophonts", new GenericRouteHandler(typeof(SophontCodesHandler)), DEFAULT_JSON));
        }
    }
}
