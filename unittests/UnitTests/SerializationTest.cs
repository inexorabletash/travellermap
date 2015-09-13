using Maps;
using Maps.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnitTests
{
    [TestClass]
    internal class SerializationTest
    {
        [TestMethod]
        public void ColumnFormatterTest()
        {
            ColumnSerializer formatter = new ColumnSerializer(new string[] {"1", "2", "3", "4"});
            formatter.AddRow(new string[] {"a", "bb", "ccc", "dddd"} );
            formatter.AddRow(new string[] { " A ", "  BB  ", "   CCC   ", "    DDDD    " });
            formatter.AddRow(new string[] { "w", "x", "y", "z" });
            StringWriter writer = new StringWriter();
                formatter.Serialize(writer);
            Assert.AreEqual(
                "1 2  3   4   \r\n" +
                "- -- --- ----\r\n" +
                "a bb ccc dddd\r\n" +
                "A BB CCC DDDD\r\n" +
                "w x  y   z   \r\n",
                writer.ToString());
        }

        [TestMethod]
        public void ColumnParserTest()
        {
            StringReader reader = new StringReader(
                "\r\n" +
                "# comment\r\n" +
                "\r\n" +
                "1    2    3   4        5         6 7\r\n" +
                "---- -    --- -------- --------- - ----\r\n" +
                "a a  b  x c   d     d          e f g   \r\n" +
                "AAAAABBBBBCCCCDDDDDDDDDEEEEEEEEEEFFGGGGGGGG\r\n");

            var parsed = new ColumnParser(reader);

            CollectionAssert.AreEqual(new string[] { "1", "2", "3", "4", "5", "6", "7" }, parsed.Fields.ToList());

            var data = parsed.Data;
            Assert.AreEqual(data.Count,2);

            var dict = data[0].dict;
            Assert.AreEqual("a a", dict["1"]);
            Assert.AreEqual("b", dict["2"]);
            Assert.AreEqual("c", dict["3"]);
            Assert.AreEqual("d     d", dict["4"]);
            Assert.AreEqual("        e", dict["5"]);
            Assert.AreEqual("f", dict["6"]);
            Assert.AreEqual("g", dict["7"]);
                
            dict = data[1].dict;
            Assert.AreEqual("AAAA", dict["1"]);
            Assert.AreEqual("B", dict["2"]);
            Assert.AreEqual("CCC", dict["3"]);
            Assert.AreEqual("DDDDDDDD", dict["4"]);
            Assert.AreEqual("EEEEEEEEE", dict["5"]);
            Assert.AreEqual("F", dict["6"]);
            Assert.AreEqual("GGGG", dict["7"]);
        }

        [TestMethod]
        public void MSECParserTest()
        {
            var stream = string.Join("\r\n", new string[] {
                "",
                "# comment",
                "",
                "sector Sector Name",
                "domain Domain of Function",
                "alpha Q1",
                "beta Q2",
                "gamma Q3",
                "delta Q4",
                "",
                "ally X7 Seven Evil Exes",
                "border 0001 0002 0003 0004",
                "border 0000 0001 0002 0003",
                "       0004 0005 0006 blue",
                "route 0101 0202",
                "route -1 -1 3240 1 1 0101 red",
                "label 0123 Your text here"
            }).ToStream(Encoding.UTF8);

            Assert.AreEqual("MSEC", SectorMetadataFileParser.SniffType(stream));
            Sector sector = SectorMetadataFileParser.ForType("MSEC").Parse(stream);

            Assert.AreEqual(1, sector.Names.Count);
            Assert.AreEqual("Sector Name", sector.Names[0].Text);

            Assert.AreEqual(1, sector.Allegiances.Count);
            Assert.AreEqual("X7", sector.Allegiances[0].T5Code);
            Assert.AreEqual("Seven Evil Exes", sector.Allegiances[0].Name);

            Assert.AreEqual(2, sector.Borders.Count);
            Assert.AreEqual("0001 0002 0003 0004", sector.Borders[0].PathString);
            Assert.AreEqual("0000 0001 0002 0003 0004 0005 0006", sector.Borders[1].PathString);
            Assert.AreEqual("Blue", sector.Borders[1].ColorHtml);

            Assert.AreEqual(2, sector.Routes.Count);
            Assert.AreEqual(new Point(0, 0), sector.Routes[0].StartOffset);
            Assert.AreEqual(0101, sector.Routes[0].Start.ToInt());
            Assert.AreEqual(0202, sector.Routes[0].End.ToInt());
            Assert.AreEqual(new Point(0, 0), sector.Routes[0].EndOffset);
            Assert.AreEqual(new Point(-1, -1), sector.Routes[1].StartOffset);
            Assert.AreEqual(3240, sector.Routes[1].Start.ToInt());
            Assert.AreEqual(0101, sector.Routes[1].End.ToInt());
            Assert.AreEqual(new Point(1, 1), sector.Routes[1].EndOffset);
            Assert.AreEqual("Red", sector.Routes[1].ColorHtml);

            Assert.AreEqual(1, sector.Labels.Count);
            Assert.AreEqual("Your text here", sector.Labels[0].Text);
        }

        [TestMethod]
        public void MSECWriterTest()
        {
            Sector sector = new Sector();
            sector.Names.Add(new Name("Sector Name"));
            sector.Allegiances.Add(new Allegiance("X7", "Seven Evil Exes"));
            sector.Borders.Add(new Border("0001 0002 0003 0004"));
            sector.Borders.Add(new Border("0000 0001 0002 0003 0004 0005 0006", "blue"));
            sector.Routes.Add(new Route(start: 0101, end: 0202));
            sector.Routes.Add(new Route(new Point(-1, -1), 3240, new Point(1, 1), 0101, "red"));
            sector.Labels.Add(new Label(0123, "Your text here"));

            StringWriter writer = new StringWriter();
            MSECSerializer msec = new MSECSerializer();
            msec.Serialize(writer, sector);

            string expected = string.Join("\r\n", new string[] {
                "# Generated by http://www.travellermap.com",
                "# <DATETIME>",
                "",
                "sector Sector Name",
                "",
                "",
                "# Other",
                "#",
                "border 0001 0002 0003 0004 red",
                "border 0000 0001 0002 0003 0004 0005 0006 blue",
                "label 0123 Your text here",
                "",
                "# Third Imperium",
                "#",
                "route -1 -1 3240 1 1 0101 red",
                "route 0101 0202 green",
                "",
                "# Seven Evil Exes",
                "#",
                "ally X7 Seven Evil Exes",
                ""
            });
            string actual = writer.ToString();
            actual = Regex.Replace(actual, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\S+", "<DATETIME>");
            Assert.AreEqual(expected, actual);
        }

    }
}
