﻿using Json;
using Maps.Admin;
using Maps.API;
using Maps.HTTP;
using System;
using System.Globalization;
using System.Net;
using System.Web.Routing;

namespace Maps
{
    public class GlobalAsax : System.Web.HttpApplication
    {
        public static readonly DateTime startup_time = DateTime.Now;

        protected void Application_Start(object sender, EventArgs e)
        {
            // Shouldn't be necessary (included in 4.5 by default?), but ensure TLS 1.2 is used.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            RegisterRoutes(RouteTable.Routes);
        }

        private static void RegisterRoutes(RouteCollection routes)
        {
            var DEFAULT_JSON = new RouteValueDictionary { { "accept", JsonConstants.MediaType } };

            // Navigation  --------------------------------------------------

            routes.Add(new RegexRoute(@"/go/(?<sector>[^/]+)", new RedirectRouteHandler("/?sector={sector}", statusCode: 302)));
            routes.Add(new RegexRoute(@"/go/(?<sector>[^/]+)/(?<hex>[0-9]{4})", new RedirectRouteHandler("/?sector={sector}&hex={hex}", statusCode: 302)));
            routes.Add(new RegexRoute(@"/go/(?<sector>[^/]+)/(?<subsector>[^/]+)", new RedirectRouteHandler("/?sector={sector}&subsector={subsector}", statusCode: 302)));

            routes.Add(new RegexRoute(@"/booklet/(?<sector>[^/]+)", new RedirectRouteHandler("/make/booklet?sector={sector}", statusCode: 302)));
            routes.Add(new RegexRoute(@"/sheet/(?<sector>[^/]+)/(?<hex>[0-9]{4})", new RedirectRouteHandler("/print/world?sector={sector}&hex={hex}", statusCode: 302)));

            // Administration -----------------------------------------------

            routes.Add(new RegexRoute(@"/admin/admin", new GenericRouteHandler(typeof(AdminHandler))));
            routes.Add(new RegexRoute(@"/admin/flush", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "flush" })));
            routes.Add(new RegexRoute(@"/admin/reindex", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "reindex" })));
            routes.Add(new RegexRoute(@"/admin/profile", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "profile" })));
            routes.Add(new RegexRoute(@"/admin/uptime", new GenericRouteHandler(typeof(AdminHandler)),
                new RouteValueDictionary(new { action = "uptime" })));
            routes.Add(new RegexRoute(@"/admin/codes", new GenericRouteHandler(typeof(CodesHandler))));
            routes.Add(new RegexRoute(@"/admin/routes", new GenericRouteHandler(typeof(RoutesHandler))));
            routes.Add(new RegexRoute(@"/admin/dump", new GenericRouteHandler(typeof(DumpHandler))));
            routes.Add(new RegexRoute(@"/admin/errors", new GenericRouteHandler(typeof(ErrorsHandler))));
            routes.Add(new RegexRoute(@"/admin/overview", new GenericRouteHandler(typeof(OverviewHandler))));

            // Search -------------------------------------------------------

            routes.Add(new RegexRoute(@"/api/search", new GenericRouteHandler(typeof(SearchHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/route", new GenericRouteHandler(typeof(RouteHandler)), DEFAULT_JSON));

            // Rendering ----------------------------------------------------

            routes.Add(new RegexRoute(@"/api/jumpmap", new GenericRouteHandler(typeof(JumpMapHandler))));
            routes.Add(new RegexRoute(@"/api/poster", new GenericRouteHandler(typeof(PosterHandler))));
            routes.Add(new RegexRoute(@"/api/poster/(?<sector>[^/]+)", new GenericRouteHandler(typeof(PosterHandler))));
            routes.Add(new RegexRoute(@"/api/poster/(?<sector>[^/]+)/(?<quadrant>(?:alpha|beta|gamma|delta))", new GenericRouteHandler(typeof(PosterHandler))));
            routes.Add(new RegexRoute(@"/api/poster/(?<sector>[^/]+)/(?<subsector>[^/]+)", new GenericRouteHandler(typeof(PosterHandler))));
            routes.Add(new RegexRoute(@"/api/tile", new GenericRouteHandler(typeof(TileHandler))));

            // Location Queries ---------------------------------------------

            routes.Add(new RegexRoute(@"/api/coordinates", new GenericRouteHandler(typeof(CoordinatesHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/credits", new GenericRouteHandler(typeof(CreditsHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/jumpworlds", new GenericRouteHandler(typeof(JumpWorldsHandler)), DEFAULT_JSON));

            // Data Retrieval - API-centric ---------------------------------

            routes.Add(new RegexRoute(@"/api/universe", new GenericRouteHandler(typeof(UniverseHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/milieux", new GenericRouteHandler(typeof(MilieuxCodesHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/sec", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"/api/sec/(?<sector>[^/]+)", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" } }));
            routes.Add(new RegexRoute(@"/api/sec/(?<sector>[^/]+)/(?<quadrant>alpha|beta|gamma|delta)", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", 0 } }));
            routes.Add(new RegexRoute(@"/api/sec/(?<sector>[^/]+)/(?<subsector>[^/]+)", new GenericRouteHandler(typeof(SECHandler)), new RouteValueDictionary { { "type", "SecondSurvey" }, { "metadata", 0 } }));
            routes.Add(new RegexRoute(@"/api/metadata", new GenericRouteHandler(typeof(SectorMetaDataHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/metadata/(?<sector>[^/]+)", new GenericRouteHandler(typeof(SectorMetaDataHandler)), DEFAULT_JSON));
            routes.Add(new RegexRoute(@"/api/msec", new GenericRouteHandler(typeof(MSECHandler))));
            routes.Add(new RegexRoute(@"/api/msec/(?<sector>[^/]+)", new GenericRouteHandler(typeof(MSECHandler))));

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

            routes.Add(new RegexRoute(@"/data/(?<sector>[^/]+)/(?<hex>[0-9]{4})/sheet", new RedirectRouteHandler("/print/world?sector={sector}&hex={hex}", statusCode: 302)));

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
