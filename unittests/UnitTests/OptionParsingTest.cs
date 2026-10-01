using Maps;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Web;

namespace UnitTests
{
    [TestClass]
    public class OptionParsingTest
    {
        private static HttpRequest Request(string query) => new HttpRequest("", "http://localhost/api/test", query);

        private static readonly IDictionary<string, object> RouteDefaults =
            new Dictionary<string, object> { { "type", "SecondSurvey" }, { "metadata", "0" }, { "jump", 0 } };

        [TestMethod]
        public void StringOptions()
        {
            var r = Request("sector=Spinward%20Marches&type=TabDelimited");
            Assert.AreEqual("Spinward Marches", HandlerBase.GetStringOption(r, RouteDefaults, "sector"));
            Assert.AreEqual("TabDelimited", HandlerBase.GetStringOption(r, RouteDefaults, "type"), "request overrides route default");
            Assert.AreEqual("0", HandlerBase.GetStringOption(r, RouteDefaults, "metadata"), "route default");
            Assert.AreEqual("0", HandlerBase.GetStringOption(r, RouteDefaults, "jump"), "non-string route default");
            Assert.AreEqual("dflt", HandlerBase.GetStringOption(Request("a=1"), null, "sector", "dflt"));

            Assert.IsTrue(HandlerBase.HasOption(r, RouteDefaults, "sector"));
            Assert.IsTrue(HandlerBase.HasOption(r, RouteDefaults, "metadata"));
            Assert.IsFalse(HandlerBase.HasOption(Request("a=1"), null, "sector"));
        }

        [TestMethod]
        public void BoolOptions()
        {
            Assert.IsTrue(HandlerBase.GetBoolOption(Request("po=1"), null, "po", false));
            Assert.IsTrue(HandlerBase.GetBoolOption(Request("po=2"), null, "po", false), "any non-zero integer");
            Assert.IsFalse(HandlerBase.GetBoolOption(Request("routes=0"), null, "routes", true));
            Assert.IsFalse(HandlerBase.GetBoolOption(Request("a=1"), RouteDefaults, "metadata", true), "route default");

            // A bare flag means true (used by the admin pages, e.g. /admin/errors?hide-uwp).
            Assert.IsTrue(HandlerBase.GetBoolOption(Request("hide-uwp"), null, "hide-uwp", false));
            Assert.IsTrue(HandlerBase.GetBoolOption(Request("sector=spin&hide-uwp&hide-tl"), null, "hide-tl", false));
            Assert.IsFalse(HandlerBase.GetBoolOption(Request("hide-uwp"), null, "hide-tl", false));

            // Unparseable values and absent options use the default.
            Assert.IsTrue(HandlerBase.GetBoolOption(Request("po=yes"), null, "po", true));
            Assert.IsFalse(HandlerBase.GetBoolOption(Request("po=yes"), null, "po", false));
            Assert.IsTrue(HandlerBase.GetBoolOption(Request(""), null, "po", true));
        }
    }
}
