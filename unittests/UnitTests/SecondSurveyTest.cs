using Maps;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace UnitTests
{
    [TestClass]
    public class SecondSurveyTest
    {
        [TestMethod]
        public void HexDigits()
        {
            // Traveller "eHex": 0-9, then A-Z skipping I and O.
            Assert.AreEqual('9', SecondSurvey.ToHex(9));
            Assert.AreEqual('A', SecondSurvey.ToHex(10));
            Assert.AreEqual('H', SecondSurvey.ToHex(17));
            Assert.AreEqual('J', SecondSurvey.ToHex(18));
            Assert.AreEqual('P', SecondSurvey.ToHex(23));
            for (int i = 0; i <= 33; ++i)
                Assert.AreEqual(i, SecondSurvey.FromHex(SecondSurvey.ToHex(i)), $"round trip {i}");
            Assert.AreEqual(-1, SecondSurvey.FromHex('?', valueIfUnknown: -1));
        }

        [TestMethod]
        public void AllegianceCodes()
        {
            Assert.IsTrue(SecondSurvey.IsKnownT5Allegiance("ImDd"));
            Assert.IsFalse(SecondSurvey.IsKnownT5Allegiance("Zzzz"));
            Assert.AreEqual("Third Imperium, Domain of Deneb", SecondSurvey.GetStockAllegianceFromCode("ImDd").Name);

            // T5 code -> base code, and legacy (2-letter) codes
            Assert.AreEqual("Im", SecondSurvey.AllegianceCodeToBaseAllegianceCode("ImDd"));
            Assert.AreEqual("Zh", SecondSurvey.AllegianceCodeToBaseAllegianceCode("ZhCo"));
            Assert.AreEqual("Im", SecondSurvey.T5AllegianceCodeToLegacyCode("ImDd"));
            Assert.IsNotNull(SecondSurvey.GetStockAllegianceFromCode("Im"), "legacy code resolves");
            Assert.IsNull(SecondSurvey.GetStockAllegianceFromCode("Zzzz"));
        }

        [TestMethod]
        public void SophontCodes()
        {
            Assert.AreEqual("Vargr", SecondSurvey.SophontCodeToName("Varg"));
            Assert.IsNull(SecondSurvey.SophontCodeToName("Zzzz"));
            Assert.IsTrue(SecondSurvey.SophontCodes.Contains("Asla"));
        }

        // (allegiance, T5 bases, legacy base code) per the tables' comments.
        private static readonly string[][] LegacyBases = {
            new[] { "ImDd", "NS", "A" },  // Imperial Naval + Scout
            new[] { "ImDd", "NW", "B" },  // Imperial Naval + Way station
            new[] { "ImDd", "N", "N" },
            new[] { "ImDd", "S", "S" },
            new[] { "ImDd", "KM", "F" },  // Military & Naval
            new[] { "SoCf", "KM", "K" },  // Solomani Naval and Planetary
            new[] { "ZhCo", "KM", "Z" },  // Zhodani Naval/Military
            new[] { "ZhCo", "W", "X" },   // Zhodani Relay Station
            new[] { "ZhCo", "D", "Y" },   // Zhodani Depot
            new[] { "Dr", "M", "Q" },     // Droyne Military Garrison
            new[] { "Dr", "K", "P" },     // Droyne Naval Base
        };

        [TestMethod]
        public void EncodeLegacyBases()
        {
            foreach (var c in LegacyBases)
                Assert.AreEqual(c[2], SecondSurvey.EncodeLegacyBases(c[0], c[1]), $"{c[0]} {c[1]}");
        }

        [TestMethod]
        public void DecodeLegacyBases()
        {
            foreach (var c in LegacyBases)
                Assert.AreEqual(c[1], SecondSurvey.DecodeLegacyBases(c[0], c[2]), $"{c[0]} {c[2]}");

            // TNE Hiver supply base
            Assert.AreEqual("H", SecondSurvey.DecodeLegacyBases("Sc", "H"));
            // Unknown codes pass through
            Assert.AreEqual("?", SecondSurvey.DecodeLegacyBases("ImDd", "?"));
        }
    }
}
