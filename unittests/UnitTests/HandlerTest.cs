using Maps.Admin;
using Maps.API;
using Maps.HTTP;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Web.Routing;

namespace UnitTests
{
    [TestClass]
    public class HandlerTest
    {
        [TestMethod]
        public void AdminKeyTest()
        {
            // Correct key
            Assert.IsTrue(AdminHandlerBase.IsValidAdminKey("s3cret", "s3cret"));

            // Wrong or missing key
            Assert.IsFalse(AdminHandlerBase.IsValidAdminKey("wrong", "s3cret"));
            Assert.IsFalse(AdminHandlerBase.IsValidAdminKey("s3cre", "s3cret"));
            Assert.IsFalse(AdminHandlerBase.IsValidAdminKey("s3cretX", "s3cret"));
            Assert.IsFalse(AdminHandlerBase.IsValidAdminKey("", "s3cret"));
            Assert.IsFalse(AdminHandlerBase.IsValidAdminKey(null, "s3cret"));

            // Unconfigured key never matches (previously null == null granted access)
            Assert.IsFalse(AdminHandlerBase.IsValidAdminKey(null, null));
            Assert.IsFalse(AdminHandlerBase.IsValidAdminKey("", ""));
            Assert.IsFalse(AdminHandlerBase.IsValidAdminKey("anything", null));

            // Placeholder from Web.config.sample never matches
            Assert.IsFalse(AdminHandlerBase.IsValidAdminKey(
                AdminHandlerBase.PlaceholderAdminKey, AdminHandlerBase.PlaceholderAdminKey));
        }

        [TestMethod]
        public void ImageSizeTest()
        {
            // Normal tile and the largest legitimate poster (full sector, scale 128, dpr 2)
            Assert.IsTrue(ImageHandlerBase.IsImageSizeAllowed(256, 256, bitmap: true));
            Assert.IsTrue(ImageHandlerBase.IsImageSizeAllowed(7205, 10291, bitmap: true));

            // Degenerate sizes
            Assert.IsFalse(ImageHandlerBase.IsImageSizeAllowed(0, 256, bitmap: true));
            Assert.IsFalse(ImageHandlerBase.IsImageSizeAllowed(256, -1, bitmap: false));
            Assert.IsFalse(ImageHandlerBase.IsImageSizeAllowed(double.NaN, 256, bitmap: false));

            // Too many pixels for a bitmap, but fine as vector output
            Assert.IsFalse(ImageHandlerBase.IsImageSizeAllowed(20000, 20000, bitmap: true));
            Assert.IsTrue(ImageHandlerBase.IsImageSizeAllowed(20000, 20000, bitmap: false));

            // Sizes that would overflow int are rejected for any output
            Assert.IsFalse(ImageHandlerBase.IsImageSizeAllowed(1e10, 100, bitmap: false));
            Assert.IsFalse(ImageHandlerBase.IsImageSizeAllowed(double.PositiveInfinity, 100, bitmap: false));
            Assert.IsFalse(ImageHandlerBase.IsImageSizeAllowed(65537, 65537, bitmap: true));
        }

        [TestMethod]
        public void RedirectTargetTest()
        {
            var values = new RouteValueDictionary { { "sector", "Spinward Marches" }, { "hex", "1910" } };
            Assert.AreEqual("/print/world?sector=Spinward%20Marches&hex=1910",
                RedirectRouteHandler.ExpandTarget("/print/world?sector={sector}&hex={hex}", values));

            // Characters that would otherwise break the query string are encoded
            values = new RouteValueDictionary { { "sector", "A&B #1+2" } };
            Assert.AreEqual("/?sector=A%26B%20%231%2B2",
                RedirectRouteHandler.ExpandTarget("/?sector={sector}", values));
        }

        [TestMethod]
        public void ServerFaultTest()
        {
            Assert.IsTrue(DataHandlerBase.IsServerFault(new NullReferenceException()));
            Assert.IsTrue(DataHandlerBase.IsServerFault(new IndexOutOfRangeException()));
            Assert.IsTrue(DataHandlerBase.IsServerFault(new FileNotFoundException()));

            // Types also used for request input errors stay 400
            Assert.IsFalse(DataHandlerBase.IsServerFault(new ApplicationException("Sector not found")));
            Assert.IsFalse(DataHandlerBase.IsServerFault(new ArgumentException()));
            Assert.IsFalse(DataHandlerBase.IsServerFault(new FormatException()));
            Assert.IsFalse(DataHandlerBase.IsServerFault(new Maps.Utilities.ParseException()));
        }
    }
}
