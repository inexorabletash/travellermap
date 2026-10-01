using Maps;
using Maps.Admin;
using Maps.API;
using Maps.HTTP;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Web;
using System.Web.Routing;

namespace UnitTests
{
    /// <summary>
    /// Checks that documented URLs reach the intended handler with the intended values.
    /// Routes are matched in registration order, so this guards against reordering
    /// regressions (e.g. /data/{sector}/sec being captured by /data/{sector}/{subsector}).
    /// </summary>
    [TestClass]
    public class RoutingTest
    {
        private class FakeRequest : HttpRequestBase
        {
            private readonly string path;
            public FakeRequest(string path) { this.path = path; }
            public override string Path => path;
        }

        private class FakeContext : HttpContextBase
        {
            private readonly HttpRequestBase request;
            public FakeContext(string path) { request = new FakeRequest(path); }
            public override HttpRequestBase Request => request;
        }

        private static RouteCollection routes;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            routes = new RouteCollection();
            GlobalAsax.RegisterRoutes(routes);
        }

        // Same precedence as RouteCollection.GetRouteData (first match wins), without
        // needing a hosted ASP.NET environment.
        private static RouteData Resolve(string path)
        {
            var context = new FakeContext(path);
            foreach (RouteBase route in routes)
            {
                RouteData data = route.GetRouteData(context);
                if (data != null)
                    return data;
            }
            return null;
        }

        private static RouteData AssertHandler(string path, Type handler, params string[] expectedValues)
        {
            RouteData data = Resolve(path);
            Assert.IsNotNull(data, $"No route for {path}");
            var generic = data.RouteHandler as GenericRouteHandler;
            Assert.IsNotNull(generic, $"{path} is not handled by a GenericRouteHandler");
            Assert.AreEqual(handler, generic.HandlerType, path);
            AssertValues(path, data, expectedValues);
            return data;
        }

        private static void AssertRedirect(string path, string target, params string[] expectedValues)
        {
            RouteData data = Resolve(path);
            Assert.IsNotNull(data, $"No route for {path}");
            var redirect = data.RouteHandler as RedirectRouteHandler;
            Assert.IsNotNull(redirect, $"{path} is not a redirect");
            Assert.AreEqual(target, redirect.Target, path);
            AssertValues(path, data, expectedValues);
        }

        // expectedValues are "key=value" pairs, or "!key" for a value that must be absent
        // (several routes share a handler, e.g. /data/{sector}/sec and /data/{sector}/{subsector},
        // so the values are what distinguish them).
        private static void AssertValues(string path, RouteData data, string[] expectedValues)
        {
            foreach (var kv in expectedValues)
            {
                if (kv.StartsWith("!"))
                {
                    Assert.IsFalse(data.Values.ContainsKey(kv.Substring(1)), $"{path}: unexpected {kv.Substring(1)}");
                    continue;
                }
                string[] parts = kv.Split(new[] { '=' }, 2);
                Assert.IsTrue(data.Values.ContainsKey(parts[0]), $"{path}: missing {parts[0]}");
                Assert.AreEqual(parts[1], data.Values[parts[0]].ToString(), $"{path}: {parts[0]}");
            }
        }

        [TestMethod]
        public void ApiRoutes()
        {
            AssertHandler("/api/coordinates", typeof(CoordinatesHandler), "accept=application/json");
            AssertHandler("/api/search", typeof(SearchHandler));
            AssertHandler("/api/route", typeof(RouteHandler));
            AssertHandler("/api/tile", typeof(TileHandler));
            AssertHandler("/api/jumpmap", typeof(JumpMapHandler));
            AssertHandler("/api/poster/Spinward Marches", typeof(PosterHandler), "sector=Spinward Marches");
            AssertHandler("/api/poster/Spinward Marches/alpha", typeof(PosterHandler), "quadrant=alpha");
            AssertHandler("/api/poster/Spinward Marches/Regina", typeof(PosterHandler), "subsector=Regina");
            AssertHandler("/api/sec/Spinward Marches", typeof(SECHandler), "type=SecondSurvey");
            AssertHandler("/api/metadata/Spinward Marches", typeof(SectorMetaDataHandler));
            AssertHandler("/api/msec/Spinward Marches", typeof(MSECHandler));
            AssertHandler("/t5ss/allegiances", typeof(AllegianceCodesHandler));
            AssertHandler("/t5ss/sophonts", typeof(SophontCodesHandler));
        }

        [TestMethod]
        public void DataRoutes()
        {
            AssertHandler("/data", typeof(UniverseHandler));
            AssertHandler("/data/Spinward Marches", typeof(SECHandler), "sector=Spinward Marches", "type=SecondSurvey", "!subsector");
            AssertHandler("/data/Spinward Marches/sec", typeof(SECHandler), "!subsector", "!type");
            AssertHandler("/data/Spinward Marches/tab", typeof(SECHandler), "type=TabDelimited", "!subsector");
            AssertHandler("/data/Spinward Marches/metadata", typeof(SectorMetaDataHandler));
            AssertHandler("/data/Spinward Marches/msec", typeof(MSECHandler));
            AssertHandler("/data/Spinward Marches/image", typeof(PosterHandler));
            AssertHandler("/data/Spinward Marches/coordinates", typeof(CoordinatesHandler));
            AssertHandler("/data/Spinward Marches/credits", typeof(CreditsHandler));

            // Quadrant, subsector by index, subsector by name
            AssertHandler("/data/Spinward Marches/alpha", typeof(SECHandler), "quadrant=alpha", "metadata=0", "!subsector");
            AssertHandler("/data/Spinward Marches/alpha/image", typeof(PosterHandler), "quadrant=alpha", "!subsector");
            AssertHandler("/data/Spinward Marches/C", typeof(SECHandler), "subsector=C", "metadata=0");
            AssertHandler("/data/Spinward Marches/C/tab", typeof(SECHandler), "subsector=C", "type=TabDelimited");
            AssertHandler("/data/Spinward Marches/Regina", typeof(SECHandler), "subsector=Regina");
            AssertHandler("/data/Spinward Marches/Regina/image", typeof(PosterHandler), "subsector=Regina");

            // World
            AssertHandler("/data/Spinward Marches/1910", typeof(JumpWorldsHandler), "hex=1910", "jump=0");
            AssertHandler("/data/Spinward Marches/1910/jump/3", typeof(JumpWorldsHandler), "hex=1910", "jump=3");
            AssertHandler("/data/Spinward Marches/1910/image", typeof(JumpMapHandler), "jump=0", "!subsector");
            AssertHandler("/data/Spinward Marches/1910/jump/3/image", typeof(JumpMapHandler), "jump=3");
            AssertHandler("/data/Spinward Marches/1910/coordinates", typeof(CoordinatesHandler), "hex=1910");
        }

        [TestMethod]
        public void RoutesAreCaseInsensitive()
        {
            AssertHandler("/DATA/spinward marches/TAB", typeof(SECHandler), "type=TabDelimited");
            AssertHandler("/Api/Coordinates", typeof(CoordinatesHandler));
        }

        [TestMethod]
        public void RedirectRoutes()
        {
            AssertRedirect("/go/Spinward Marches", "/?sector={sector}");
            AssertRedirect("/go/Spinward Marches/1910", "/?sector={sector}&hex={hex}", "hex=1910");
            AssertRedirect("/go/Spinward Marches/Regina", "/?sector={sector}&subsector={subsector}");
            AssertRedirect("/booklet/Spinward Marches", "/make/booklet?sector={sector}");
            AssertRedirect("/sheet/Spinward Marches/1910", "/print/world?sector={sector}&hex={hex}");
            AssertRedirect("/data/Spinward Marches/booklet", "/make/booklet?sector={sector}");
            AssertRedirect("/data/Spinward Marches/1910/sheet", "/print/world?sector={sector}&hex={hex}");
        }

        [TestMethod]
        public void AdminRoutes()
        {
            AssertHandler("/admin/flush", typeof(AdminHandler), "action=flush");
            AssertHandler("/admin/reindex", typeof(AdminHandler), "action=reindex");
            AssertHandler("/admin/errors", typeof(ErrorsHandler));
        }

        [TestMethod]
        public void UnknownPathsDoNotMatch()
        {
            Assert.IsNull(Resolve("/"));
            Assert.IsNull(Resolve("/index.html"));
            Assert.IsNull(Resolve("/api/nonexistent"));
            Assert.IsNull(Resolve("/data/a/b/c/d/e"));
        }
    }
}
