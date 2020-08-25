#nullable enable 
using Maps.Utilities;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Linq;
using System.Web;

namespace Maps.Serialization
{
    internal abstract class SectorMetadataFileParser
    {
        public const int BUFFER_SIZE = 32768;

        public abstract Encoding Encoding { get; }

        public virtual Sector Parse(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding, detectEncodingFromByteOrderMarks: true, bufferSize: BUFFER_SIZE);
            return Parse(reader);
        }
        public abstract Sector Parse(TextReader reader);

        public static SectorMetadataFileParser ForType(string mediaType) =>
            mediaType switch
            {
                "MSEC" => new MSECParser(),
                "XML" => new XmlSectorMetadataParser(),
                _ => new XmlSectorMetadataParser(),
            };

        private static readonly Regex SNIFF_XML_REGEX = new Regex(@"<\?xml", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string SniffType(Stream stream)
        {
            long pos = stream.Position;
            try
            {
                using (var reader = new NoCloseStreamReader(stream, Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true, bufferSize: BUFFER_SIZE))
                {
                    string line = reader.ReadLine();
                    if (line != null && SNIFF_XML_REGEX.IsMatch(line))
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

        private static string? ParseString(string s) => string.IsNullOrEmpty(s) ? null : s;

        private static bool? ParseBool(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return s == "true" || (s == "false" ? false : throw new Exception($"'{s}' is not a valid boolean"));
        }
        private static int? ParseInt(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            try { return int.Parse(s); }
            catch (Exception) { throw new Exception($"'{s}' is not a valid integer"); }
        }
        private static float? ParseFloat(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            try { return float.Parse(s); }
            catch (Exception) { throw new Exception($"'{s}' is not a valid number"); }
        }
        private static TEnum? ParseEnum<TEnum>(string s) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;
            try { return (TEnum)Enum.Parse(typeof(TEnum), s); }
            catch { throw new Exception($"'{s}' is not a valid {typeof(TEnum).Name}"); }
        }

        private static void ParseErrorAppender(XmlElement elem, Action<XmlElement> action)
        {
            try { action(elem); }
            catch (Exception ex) { throw new Exception($"{ex.Message}\n in {elem.OuterXml}"); }
        }

        private Sector Parse(XmlDocument xd)
        {
            Sector sector = new Sector();

            foreach (var e in xd.SelectNodes("/Sector/Name").OfType<XmlElement>())
            {
                ParseErrorAppender(e, name => sector.Names.Add(new Name()
                {
                    Lang = ParseString(name.GetAttribute("lang")),
                    Text = name.InnerText,
                }));
            }
            foreach (var e in xd.SelectNodes("/Sector/Subsectors/Subsector").OfType<XmlElement>())
            {
                ParseErrorAppender(e, subsector => sector.Subsectors.Add(new Subsector()
                {
                    Index = subsector.GetAttribute("Index"),
                    Name = subsector.InnerText,
                }));
            }
            foreach (var e in xd.SelectNodes("/Sector/Routes/Route").OfType<XmlElement>())
            {
                ParseErrorAppender(e, route =>
                {
                    var r = new Route()
                    {
                        Allegiance = ParseString(route.GetAttribute("Allegiance")),
                        ColorHtml = ParseString(route.GetAttribute("Color")),
                        Style = ParseEnum<LineStyle>(route.GetAttribute("Style")),
                        Type = ParseString(route.GetAttribute("Type")),
                        Width = ParseFloat(route.GetAttribute("Width")),

                        // These assignments must precede Start/EndHex as the latter may
                        // adjust Start/EndOffsetX/Y (e.g. if 0000/3341).
                        StartOffsetX = ParseInt(route.GetAttribute("StartOffsetX")) ?? 0,
                        StartOffsetY = ParseInt(route.GetAttribute("StartOffsetY")) ?? 0,
                        EndOffsetX = ParseInt(route.GetAttribute("EndOffsetX")) ?? 0,
                        EndOffsetY = ParseInt(route.GetAttribute("EndOffsetY")) ?? 0,

                        StartHex = ParseString(route.GetAttribute("Start")) ?? throw new ParseException("Route missing Start"),
                        EndHex = ParseString(route.GetAttribute("End")) ?? throw new ParseException("Route missing Start"),
                    };
                    sector.Routes.Add(r);
                });
            }
            foreach (var e in xd.SelectNodes("/Sector/Borders/Border").OfType<XmlElement>())
            {
                ParseErrorAppender(e, border => sector.Borders.Add(new Border()
                {
                    Allegiance = ParseString(border.GetAttribute("Allegiance")),
                    ColorHtml = ParseString(border.GetAttribute("Color")),
                    Label = ParseString(border.GetAttribute("Label")),
                    LabelPositionHex = ParseString(border.GetAttribute("LabelPosition")) ?? string.Empty,
                    PathString = border.InnerText,
                    ShowLabel = ParseBool(border.GetAttribute("ShowLabel")) ?? true,
                    Style = ParseEnum<LineStyle>(border.GetAttribute("Style")),
                    WrapLabel = ParseBool(border.GetAttribute("WrapLabel")) ?? false,
                }));
            }
            foreach (var e in xd.SelectNodes("/Sector/Regions/Region").OfType<XmlElement>())
            {
                ParseErrorAppender(e, region => sector.Regions.Add(new Region()
                {
                    Allegiance = ParseString(region.GetAttribute("Allegiance")),
                    ColorHtml = ParseString(region.GetAttribute("Color")),
                    Label = ParseString(region.GetAttribute("Label")),
                    LabelPositionHex = ParseString(region.GetAttribute("LabelPosition")) ?? string.Empty,
                    PathString = region.InnerText,
                    ShowLabel = ParseBool(region.GetAttribute("ShowLabel")) ?? true,
                    Style = ParseEnum<LineStyle>(region.GetAttribute("Style")),
                    WrapLabel = ParseBool(region.GetAttribute("WrapLabel")) ?? false,
                }));
            }
            foreach (var e in xd.SelectNodes("/Sector/Allegiances/Allegiance").OfType<XmlElement>())
            {
                ParseErrorAppender(e, alleg => sector.Allegiances.Add(new Allegiance()
                {
                    Base = ParseString(alleg.GetAttribute("Base")),
                    T5Code = ParseString(alleg.GetAttribute("Code")) ?? string.Empty,
                    Name = alleg.InnerText,
                }));
            }
            foreach (var e in xd.SelectNodes("/Sector/Labels/Label").OfType<XmlElement>())
            {
                ParseErrorAppender(e, label => sector.Labels.Add(new Label()
                {
                    Allegiance = ParseString(label.GetAttribute("Allegiance")),
                    ColorHtml = ParseString(label.GetAttribute("Color")),
                    Hex = new Hex(ParseString(label.GetAttribute("Hex")) ?? string.Empty),
                    OffsetY = ParseFloat(label.GetAttribute("OffsetY")) ?? 0,
                    Size = ParseString(label.GetAttribute("Size")),
                    Wrap = ParseBool(label.GetAttribute("Wrap")) ?? false,
                    Text = label.InnerText,
                }));
            }
            if (xd.SelectSingleNode("/Sector/Stylesheet") is XmlElement stylesheet)
                ParseErrorAppender(stylesheet, e => sector.StylesheetText = e.InnerText);

            if (xd.SelectSingleNode("/Sector/Credits") is XmlElement credits)
                ParseErrorAppender(credits, e => sector.Credits = e.InnerText);

            if (xd.SelectSingleNode("/Sector/DataFile") is XmlElement dataFile)
            {
                ParseErrorAppender(dataFile, elem => {
                    sector.DataFile = new DataFile()
                    {
                        Milieu = ParseString(elem.GetAttribute("Milieu"))
                    };
                });
            }

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
