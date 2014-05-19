using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Maps.Serialization
{
    public abstract class SectorFileParser
    {
        public const int BUFFER_SIZE = 32768;

        public abstract Encoding Encoding { get; }
        public void Parse(Stream stream, WorldCollection worlds)
        {
            using (var reader = new StreamReader(stream, Encoding, detectEncodingFromByteOrderMarks: true, bufferSize: BUFFER_SIZE))
            {
                Parse(reader, worlds);
            }
        }

        public abstract void Parse(TextReader reader, WorldCollection worlds);

        public static SectorFileParser ForType(string mediaType)
        {
            switch (mediaType)
            {
                case "SecondSurvey": return new SecondSurveyParser();
                case "TabDelimited": return new TabDelimitedParser();
                case "SEC":
                default: return new SecParser();
            }
        }

        private static Regex comment = new Regex(@"^[#$@]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static Regex sniff_tab = new Regex(@"^[^\t]*(\t[^\t]*){9,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static Regex sniff_ss = new Regex(@"\{.*\} +\(.*\) +\[.*\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string SniffType(Stream stream)
        {
            long pos = stream.Position;
            try
            {
                using (var reader = new NoCloseStreamReader(stream, Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true, bufferSize: BUFFER_SIZE))
                {
                    for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        if (line.Length == 0 || comment.IsMatch(line))
                            continue;   

                        if (sniff_tab.IsMatch(line))
                            return "TabDelimited";

                        if (sniff_ss.IsMatch(line))
                            return "SecondSurvey";
                    }
                    return null;
                }
            }
            finally
            {
                stream.Position = pos;
            }
        }

        protected static string EmptyIfDash(string s)
        {
            if (String.IsNullOrWhiteSpace(s) || s == "-")
                return String.Empty;
            return s;
        }
    }

    public class SecParser : SectorFileParser
    {
        public override Encoding Encoding { get { return Encoding.UTF8; } }

        public override void Parse(TextReader reader, WorldCollection worlds)
        {
            int lineNumber = 0;
            for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                ++lineNumber;

                if (line.Length == 0)
                    continue;

                switch (line[0])
                {
                    case '#': break; // comment
                    case '$': break; // route
                    case '@': break; // subsector
                    default:
                        ParseUWP(worlds, line, lineNumber);
                        break;
                }
            }
        }

        // TEMP: Sec5Xml interrim data contains + suffix for new worlds, +/- prefix and * suffix for stars

        // PERF: Big time sucker; consider optimizing (can save 5% by eliminating Unicode char classes)
        private static readonly Regex worldRegex = new Regex(@"^" +
            @"( \s*       (?<name>        .*                           ) )  " +	// Name
            @"( \s*       (?<hex>         \d{4}                        ) )  " +	// Hex
            @"( \s{1,2}   (?<uwp>         [ABCDEX][0-9A-Z]{6}-[0-9A-Z] ) )  " +	// UWP (Universal World Profile)
            @"( \s{1,2}   (?<base>        [A-Zr1-9* \-]                ) )  " +	// Base
            @"( \s{1,2}   (?<codes>       .{10,}?                      ) )  " +	// Remarks
            @"( \s+       (?<zone>        [GARBFU \-]                  ) )? " +	// Zone
            @"( \s{1,2}   (?<pbg>         [0-9X][0-9A-FX][0-9A-FX]     ) )  " +	// PGB (Population multiplier, Belts, Gas giants)
            @"( \s{1,2}   (?<allegiance>  ([A-Z0-9][A-Za-z0-9?\-]|--)  ) )  " +	// Allegiance
            @"( \s*       (?<rest>        .*?                          ) )  " + // Stellar data (etc)
            @"\s*$"	
            , RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex nameFixupRegex = new Regex(@"[\.\+]*$", RegexOptions.Compiled);

        private static readonly Regex placeholderNameRegex = new Regex(
            @"^[A-P]-\d{1,2}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        private static void ParseUWP(WorldCollection worlds, string line, int lineNumber)
        {
            Match match = worldRegex.Match(line);
            if (!match.Success)
            {
#if DEBUG
                worlds.ErrorList.Add("ERROR (SEC Parse): " + line);
#endif
                return;
            }

            try
            {
                World world = new World();
                // Allegiance may affect interpretation of other values, e.g. bases, zones
                world.Allegiance = match.Groups["allegiance"].Value.Trim();

                // Crack the RegExpr data
                world.Name = nameFixupRegex.Replace(match.Groups["name"].Value.Trim(), "");
                world.Hex = match.Groups["hex"].Value.Trim();
                world.UWP = match.Groups["uwp"].Value.Trim();
                world.LegacyBaseCode = EmptyIfDash(match.Groups["base"].Value.Trim());
                world.Remarks = match.Groups["codes"].Value.Trim();
                world.Zone = EmptyIfDash(match.Groups["zone"].Value);
                world.PBG = match.Groups["pbg"].Value.Trim();

                // Cleanup known placeholders
                if (world.Name == match.Groups["hex"].Value || placeholderNameRegex.IsMatch(world.Name))
                    world.Name = "";
                if (world.Name == world.Name.ToUpperInvariant() && world.IsHi)
                    world.Name = Util.FixCapitalization(world.Name);

                worlds[world.X, world.Y] = world;

                string rest = match.Groups["rest"].Value;
                if (!String.IsNullOrEmpty(rest))
                {
                    ParseRest(rest, worlds, line, world);
                }

                // TODO: Would need to filter this on serialization as well, or round trip through SEC would fail
                // world.UpdateCode("Pr", "Px"); // Legacy: Pr = Prison; T5: Px = Prison/Exile Camp, Pr = Pre-Rich
            }
            catch (Exception e)
            {
                throw new Exception(String.Format("UWP Parse Error in line {0}:\n{1}\n{2}", lineNumber, e.Message, line));
            }
        }

        private static void ParseRest(string rest, WorldCollection worlds, string line, World world)
        {
            // Assume stellar data, try to parse it
            try
            {
                world.Stellar = StellarDataParser.Parse(rest, StellarDataParser.OutputFormat.Compact);
            }
            catch (StellarDataParser.InvalidSystemException)
            {
#if DEBUG
                worlds.ErrorList.Add("WARNING (Stellar Data): " + line);
#endif
            }
        }
    }

    public abstract class T5ParserBase : SectorFileParser
    {
        private const string HEX = @"[0123456789ABCDEFGHJKLMNPQRSTUVWXYZ]";
        private static readonly Regex UWP_REGEX = new Regex("^[ABCDEX]" + HEX + @"{6}-" + HEX + @"$");
        private static readonly Regex PBG_REGEX = new Regex("^[0-9X]{3}$");

        private const string STAR = @"(D|BD|[OBAFGKM][0-9]\x20(?:Ia|Ib|II|III|IV|V|VI))";
        private static readonly Regex STARS_REGEX = new Regex("^(|" + STAR + @"(?:\x20" + STAR + @")*)$");

        private static string Check(StringDictionary dict, string key, Regex regex)
        {
            if (!regex.IsMatch(dict[key]))
                throw new Exception(key);
            return dict[key];
        }

        private static string Check(StringDictionary dict, IEnumerable<string> keys, Regex regex = null)
        {
            foreach (var key in keys)
            {
                if (!dict.ContainsKey(key))
                    continue;
                if (regex != null && !regex.IsMatch(dict[key]))
                    throw new Exception(key);
                return dict[key];
            }
            return null;
        }

        protected static void ParseWorld(WorldCollection worlds, StringDictionary dict, string line, int lineNumber)
        {
            try
            {
                World world = new World();
                world.Hex = dict["Hex"];
                world.Name = dict["Name"];

                world.UWP = Check(dict, "UWP", UWP_REGEX);

                // TEMPORARY - T5SS spreadsheet sometimes leaves out leading 0s.
                dict["PBG"] = dict["PBG"].PadLeft(3, '0');

                
                world.PBG = Check(dict, "PBG", PBG_REGEX);
                world.Stellar = Check(dict, new string[] { "Stellar", "Stars", "Stellar Data" }, STARS_REGEX);

                // Allegiance may affect interpretation of other values, e.g. bases, zones
                world.Allegiance = Check(dict, new string[] { "A", "Allegiance" });

                if (SecondSurvey.T5AllegianceCodeToLegacyCode(world.Allegiance) == world.Allegiance)
                    throw new Exception("Unknown allegiance: " + world.Allegiance);

                world.Bases = EmptyIfDash(Check(dict, new string[] { "B", "Bases" })); // TODO: World.T5Bases ?
                world.Zone = EmptyIfDash(Check(dict, new string[] { "Z", "Zone" }));
                world.Remarks = Check(dict, new string[] { "Remarks", "Trade Codes", "Comments" } );

                // T5
                world.Importance = Check(dict, new string[] { "{Ix}", "{ Ix }", "Ix" });
                world.Economic = Check(dict, new string[] { "(Ex)", "( Ex )", "Ex" });
                world.Cultural = Check(dict, new string[] { "[Cx]", "[ Cx ]", "Cx" });
                world.Nobility = EmptyIfDash(Check(dict, new string[] { "N", "Nobility" }));

                string w = Check(dict, new string[] { "W", "Worlds" });
                if (!String.IsNullOrEmpty(w))
                    world.Worlds = Int32.Parse(w, NumberStyles.Integer, CultureInfo.InvariantCulture);
                string ru = dict["RU"];
                if (!String.IsNullOrEmpty(ru))
                    world.ResourceUnits = Int32.Parse(w, NumberStyles.Integer, CultureInfo.InvariantCulture);

                // Cleanup known placeholders
                if (world.Name == world.Name.ToUpperInvariant() && world.IsHi)
                {
                    world.Name = Util.FixCapitalization(world.Name);
                }
                // Fix "smart" apostrophe
                world.Name = world.Name.Replace('\x92', '\'');

#if DEBUG
                if (worlds[world.X, world.Y] != null)
                {
                    worlds.ErrorList.Add("ERROR (Duplicate): " + line);
                }
#endif
                worlds[world.X, world.Y] = world;
            }
#if DEBUG
            catch (Exception e)
            {
                worlds.ErrorList.Add(String.Format("ERROR (TAB Parse - {0}): ", e.Message) + line);
            }
#else
            catch (Exception)
            {
            }
#endif
        }
    }

    public class TabDelimitedParser : T5ParserBase
    {
        public override Encoding Encoding { get { return Encoding.UTF8; } }

        private static readonly char[] TAB_DELIMITER = { '\t' };
        public override void Parse(TextReader reader, WorldCollection worlds)
        {
            int lineNumber = 0;
            string line;
            string[] header = null;
            while (true) {
                line = reader.ReadLine();
                if (line == null)
                    return;
                ++lineNumber;

                if (line.Length == 0)
                    continue;
                if (line.StartsWith("#"))
                    continue;

                if (header == null) {
                    header = line.Split(TAB_DELIMITER);
                    continue;
                }

                ParseLine(worlds, header, line, lineNumber);
            }
        }


        private static void ParseLine(WorldCollection worlds, string[] header, string line, int lineNumber)
        {
            string[] cols = line.Split(TAB_DELIMITER);
            if (cols.Length != header.Length) {
#if DEBUG
                worlds.ErrorList.Add("ERROR (TAB Parse): " + line);
#endif
                return;
            }
            StringDictionary dict = new StringDictionary();
            for (var i = 0; i < cols.Length; ++i ) {
                dict[header[i]] = cols[i].Trim();
            }

            ParseWorld(worlds, dict, line, lineNumber);
        }
    }

    public class SecondSurveyParser : T5ParserBase
    {
        public override Encoding Encoding { get { return Encoding.UTF8; } }

        public override void Parse(TextReader reader, WorldCollection worlds)
        {
            ColumnParser parser = new ColumnParser(reader);
            foreach (var row in parser.Data) {
                ParseWorld(worlds, row.dict, row.line, row.lineNumber);
            }
        }
    }

}