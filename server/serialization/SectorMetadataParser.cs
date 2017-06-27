using Maps.Utilities;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Linq;

namespace Maps.Serialization
{
    internal abstract class SectorMetadataFileParser
    {
        public const int BUFFER_SIZE = 32768;

        public abstract Encoding Encoding { get; }

        public virtual Sector Parse(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding, detectEncodingFromByteOrderMarks: true, bufferSize: BUFFER_SIZE))
            {
                return Parse(reader);
            }
        }
        public abstract Sector Parse(TextReader reader);

        public static SectorMetadataFileParser ForType(string mediaType)
        {
            switch (mediaType)
            {
                case "MSEC": return new MSECParser();
                case "XML":
                default: return new XmlSectorMetadataParser();
            }
        }

        private static readonly Regex sniff_xml = new Regex(@"<\?xml", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string SniffType(Stream stream)
        {
            long pos = stream.Position;
            try
            {
                using (var reader = new NoCloseStreamReader(stream, Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true, bufferSize: BUFFER_SIZE))
                {
                    string line = reader.ReadLine();
                    if (line != null && sniff_xml.IsMatch(line))
                        return "XML";
                }
                return "MSEC";
            }
            finally
            {
                stream.Position = pos;
            }
        }
    }

    internal class XmlSectorMetadataParser : SectorMetadataFileParser
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override Sector Parse(Stream stream)
        {
            try
            {
                XmlDocument xd = new XmlDocument();
                xd.Load(stream);
                return Parse(xd);
            }
            catch (System.InvalidOperationException ex) when (ex.InnerException is XmlException)
            {
                throw ex.InnerException;
            }
        }

        private static string ParseString(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }
        private static bool ParseBool(string s)
        {
            return s == "True";
        }
        private static int? ParseInt(string s)
        {
            return int.TryParse(s, out int i) ? (int?)i : null;
        }
        private static float? ParseFloat(string s)
        {
            return float.TryParse(s, out float f) ? (float?)f : null;
        }
        private static TEnum? ParseEnum<TEnum>(string s) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;
            try
            {
                return (TEnum)System.Enum.Parse(typeof(TEnum), s);
            }
            catch
            {
                return null;
            }
        }

        private Sector Parse(XmlDocument xd)
        {
            Sector sector = new Sector();

            foreach (var name in xd.SelectNodes("/Sector/Name").OfType<XmlElement>())
            {
                sector.Names.Add(new Name() {
                    Lang = ParseString(name.GetAttribute("lang")),
                    Text = name.InnerText,
                });
            }
            foreach (var subsector in xd.SelectNodes("/Sector/Subsectors/Subsector").OfType<XmlElement>())
            {
                sector.Subsectors.Add(new Subsector()
                {
                    Index = subsector.GetAttribute("Index"),
                    Name = subsector.InnerText,
                });
            }
            foreach (var route in xd.SelectNodes("/Sector/Routes/Route").OfType<XmlElement>())
            {
                var r = new Route()
                {
                    Allegiance = ParseString(route.GetAttribute("Allegiance")),
                    ColorHtml = ParseString(route.GetAttribute("Color")),
                    StartHex = ParseString(route.GetAttribute("Start")),
                    EndHex = ParseString(route.GetAttribute("End")),
                    Style = ParseEnum<LineStyle>(route.GetAttribute("Style")),
                    Type = ParseString(route.GetAttribute("Type")),
                    Width = ParseFloat(route.GetAttribute("Width")),
                };
                r.StartOffsetX = ParseInt(route.GetAttribute("StartOffsetX")) ?? r.StartOffsetX;
                r.StartOffsetY = ParseInt(route.GetAttribute("StartOffsetY")) ?? r.StartOffsetY;
                r.EndOffsetX = ParseInt(route.GetAttribute("EndOffsetX")) ?? r.EndOffsetX;
                r.EndOffsetY = ParseInt(route.GetAttribute("EndOffsetY")) ?? r.EndOffsetY;
                sector.Routes.Add(r);
            }
            foreach (var border in xd.SelectNodes("/Sector/Borders/Border").OfType<XmlElement>())
            {
                sector.Borders.Add(new Border()
                {
                    Allegiance = ParseString(border.GetAttribute("Allegiance")),
                    ColorHtml = ParseString(border.GetAttribute("Color")),
                    Label = ParseString(border.GetAttribute("Label")),
                    LabelPositionHex = ParseString(border.GetAttribute("Position")),
                    PathString = border.InnerText,
                    ShowLabel = ParseBool(border.GetAttribute("ShowLabel")),
                    Style = ParseEnum<LineStyle>(border.GetAttribute("Style")),
                    WrapLabel = ParseBool(border.GetAttribute("WrapLabel")),
                });
            }
            foreach (var alleg in xd.SelectNodes("/Sector/Allegiances/Allegiance").OfType<XmlElement>())
            {
                sector.Allegiances.Add(new Allegiance()
                {
                    Base = ParseString(alleg.GetAttribute("Base")),
                    T5Code = ParseString(alleg.GetAttribute("Code")),
                    Name = alleg.InnerText,
                });
            }
            foreach (var label in xd.SelectNodes("/Sector/Labels/Label").OfType<XmlElement>())
            {
                sector.Labels.Add(new Label()
                {
                    Allegiance = ParseString(label.GetAttribute("Allegiance")),
                    ColorHtml = ParseString(label.GetAttribute("Color")),
                    Hex = ParseInt(label.GetAttribute("Hex")) ?? 0,
                    OffsetY = ParseFloat(label.GetAttribute("OffsetY")) ?? 0,
                    Size = ParseString(label.GetAttribute("Size")),
                    Text = label.InnerText,
                });
            }
            sector.StylesheetText = (xd.SelectSingleNode("/Sector/Stylesheet") as XmlElement)?.InnerText;
            sector.Credits = (xd.SelectSingleNode("/Sector/Credits") as XmlElement)?.InnerText;

            return sector;
        }

        public override Sector Parse(TextReader reader)
        {
            try
            {
                XmlDocument xd = new XmlDocument();
                xd.Load(reader);
                return Parse(xd);
            }
            catch (System.InvalidOperationException ex) when (ex.InnerException is System.Xml.XmlException)
            {
                throw ex.InnerException;
            }
        }
    }
}
