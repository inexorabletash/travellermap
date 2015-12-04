using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Maps.Serialization
{
    internal abstract class SectorFileParser
    {
        public const int BUFFER_SIZE = 32768;

        public abstract string Name { get; }
        public abstract Encoding Encoding { get; }
        public void Parse(Stream stream, WorldCollection worlds, ErrorLogger errors)
        {
            using (var reader = new StreamReader(stream, Encoding, detectEncodingFromByteOrderMarks: true, bufferSize: BUFFER_SIZE))
            {
                Parse(reader, worlds, errors ?? worlds.ErrorList);
            }
        }

        public abstract void Parse(TextReader reader, WorldCollection worlds, ErrorLogger errors);

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
            if (string.IsNullOrWhiteSpace(s) || s == "-")
                return string.Empty;
            return s;
        }
    }

    internal class SecParser : SectorFileParser
    {
        public override string Name { get { return "SEC (Legacy)"; } }
        public override Encoding Encoding { get { return Encoding.UTF8; } }

        public override void Parse(TextReader reader, WorldCollection worlds, ErrorLogger errors)
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
                        ParseWorld(worlds, line, lineNumber, errors ?? worlds.ErrorList);
                        break;
                }
            }
        }

        private static readonly Regex uwpRegex = new Regex(@"\w\w\w\w\w\w\w-\w", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        // PERF: Big time sucker; consider optimizing (can save 5% by eliminating Unicode char classes)
        private static readonly Regex worldRegex = new Regex(@"^" +
            @"( \s*       (?<name>        .*                           ) )  " + // Name
            @"( \s*       (?<hex>         \d{4}                        ) )  " + // Hex
            @"( \s{1,2}   (?<uwp>         [ABCDEX][0-9A-Z]{6}-[0-9A-Z] ) )  " + // UWP (Universal World Profile)
            @"( \s{1,2}   (?<base>        [A-Zr1-9* \-]                ) )  " + // Base
            @"( \s{1,2}   (?<codes>       .{10,}?                      ) )  " + // Remarks
            @"( \s+       (?<zone>        [GARBFU \-]                  ) )? " + // Zone
            @"( \s{1,2}   (?<pbg>         [0-9X][0-9A-FX][0-9A-FX]     ) )  " + // PGB (Population multiplier, Belts, Gas giants)
            @"( \s{1,2}   (?<allegiance>  ([A-Za-z0-9][A-Za-z0-9?\-]|--)  ) )  " + // Allegiance
            @"( \s*       (?<rest>        .*?                          ) )  " + // Stellar data (etc)
            @"\s*$"
            , RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex nameFixupRegex = new Regex(@"[\.\+]*$", RegexOptions.Compiled);

        private static readonly Regex placeholderNameRegex = new Regex(
            @"^[A-P]-\d{1,2}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        private static void ParseWorld(WorldCollection worlds, string line, int lineNumber, ErrorLogger errors)
        {
            if (!uwpRegex.IsMatch(line))
            {
                if (errors != null)
                    errors.Warning("Ignoring non-UWP data", lineNumber, line);
                return;
            }
            Match match = worldRegex.Match(line);

            if (!match.Success)
            {
                if (errors != null)
                    errors.Error("SEC Parse", lineNumber, line);
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
                if (!string.IsNullOrEmpty(rest))
                    ParseRest(rest, lineNumber, line, world, errors);
            }
            catch (Exception e)
            {
                errors.Error("Parse error: " + e.Message, lineNumber, line);
                //throw new Exception(String.Format("UWP Parse Error in line {0}:\n{1}\n{2}", lineNumber, e.Message, line));
            }
        }

        private static void ParseRest(string rest, int lineNumber, string line, World world, ErrorLogger errors)
        {
            // Assume stellar data, try to parse it
            try
            {
                world.Stellar = StellarDataParser.Parse(rest, StellarDataParser.OutputFormat.Basic);
            }
            catch (StellarDataParser.InvalidSystemException)
            {
                if (errors != null)
                    errors.Warning(string.Format("Invalid stellar data: '{0}'", rest), lineNumber, line);
            }
        }
    }

    internal abstract class T5ParserBase : SectorFileParser
    {
        private const string HEX = @"[0123456789ABCDEFGHJKLMNPQRSTUVWXYZ]";

        // Regex checks are only done in Debug - data is trusted otherwise
        private static readonly Regex HEX_REGEX = new Regex(@"^\d\d\d\d$");
        private static readonly Regex UWP_REGEX = new Regex("^[ABCDEX]" + HEX + HEX + @"[0-AX]" + HEX + @"{3}-" + HEX + @"$");
        private static readonly Regex PBG_REGEX = new Regex("^[0-9X]{3}$");

        private static readonly Regex BASES_REGEX = new Regex(@"^C?D?E?K?M?N?R?S?T?V?W?X?$");
        private static readonly Regex ZONE_REGEX = new Regex(@"^(|A|R)$");
        private static readonly Regex NOBILITY_REGEX = new Regex(@"^[BcCDeEfFGH]*$");

        private const string STAR = @"(D|BD|BH|[OBAFGKM][0-9]\x20(?:Ia|Ib|II|III|IV|V|VI))";
        private static readonly Regex STARS_REGEX = new Regex("^(|" + STAR + @"(?:\x20" + STAR + @")*)$");

        [Flags]
        private enum CheckOptions
        {
            EmptyIfDash = 1,
            Warning = 2
        };

        private class FieldChecker
        {
            private StringDictionary dict;
            private ErrorLogger errors;
            private int lineNumber;
            private string line;
            bool hadError = false;

            public bool HadError { get { return hadError; } }
            public FieldChecker(StringDictionary dict, ErrorLogger errors, int lineNumber, string line)
            {
                this.dict = dict;
                this.errors = errors;
                this.lineNumber = lineNumber;
                this.line = line;
            }
            public string Check(string key, Regex regex, CheckOptions options = 0)
            {
                if (!regex.IsMatch(dict[key]))
                {
                    if (!options.HasFlag(CheckOptions.Warning))
                    {
                        if (errors != null)
                            errors.Error(string.Format("Unexpected value for {0}: '{1}'", key, dict[key]), lineNumber, line);
                        hadError = true;
                    }
                    else
                    {
                        if (errors != null)
                            errors.Warning(string.Format("Unexpected value for {0}: '{1}'", key, dict[key]), lineNumber, line);
                    }
                }

                string value = dict[key];

                if (options.HasFlag(CheckOptions.EmptyIfDash))
                    value = EmptyIfDash(value);

                return value;
            }

            public string Check(IEnumerable<string> keys, Regex regex = null, CheckOptions options = 0)
            {
                return Check(keys, value => regex == null || regex.IsMatch(value), options);
            }

            public string Check(IEnumerable<string> keys, Func<string, bool> validate, CheckOptions options = 0)
            {
                foreach (var key in keys)
                {
                    if (!dict.ContainsKey(key))
                        continue;
                    string value = dict[key];

                    if (options.HasFlag(CheckOptions.EmptyIfDash))
                        value = EmptyIfDash(value);

                    if (!validate(value))
                    {
                        if (!options.HasFlag(CheckOptions.Warning))
                        {
                            if (errors != null)
                                errors.Error(string.Format("Unexpected value for {0}: '{1}'", key, value), lineNumber, line);
                            hadError = true;
                        }
                        else
                        {
                            if (errors != null)
                                errors.Warning(string.Format("Unexpected value for {0}: '{1}'", key, value), lineNumber, line);
                        }
                    }

                    return value;
                }
                return null;
            }
        }

        protected static void ParseWorld(WorldCollection worlds, StringDictionary dict, string line, int lineNumber, ErrorLogger errors)
        {
            try
            {
                FieldChecker checker = new FieldChecker(dict, errors, lineNumber, line);
                World world = new World();
                world.Hex = checker.Check("Hex", HEX_REGEX);
                world.Name = dict["Name"];
                world.UWP = checker.Check("UWP", UWP_REGEX);
                world.Remarks = checker.Check(new string[] { "Remarks", "Trade Codes", "Comments" });
                world.Importance = checker.Check(new string[] { "{Ix}", "{ Ix }", "Ix" });
                world.Economic = checker.Check(new string[] { "(Ex)", "( Ex )", "Ex" });
                world.Cultural = checker.Check(new string[] { "[Cx]", "[ Cx ]", "Cx" });
                world.Nobility = checker.Check(new string[] { "N", "Nobility" }, NOBILITY_REGEX, CheckOptions.EmptyIfDash);
                world.Bases = checker.Check(new string[] { "B", "Bases" }, BASES_REGEX, CheckOptions.EmptyIfDash);
                world.Zone = checker.Check(new string[] { "Z", "Zone" }, ZONE_REGEX, CheckOptions.EmptyIfDash);
                world.PBG = checker.Check("PBG", PBG_REGEX);
                world.Allegiance = checker.Check(new string[] { "A", "Al", "Allegiance" },
                    // TODO: Allow unofficial sectors to have locally declared allegiances.
                    a => a.Length != 4 || SecondSurvey.IsKnownT5Allegiance(a));
                world.Stellar = checker.Check(new string[] { "Stellar", "Stars", "Stellar Data" }, STARS_REGEX, CheckOptions.Warning);

                byte w;
                if (byte.TryParse(checker.Check(new string[] { "W", "Worlds" }), NumberStyles.Integer, CultureInfo.InvariantCulture, out w))
                    world.Worlds = w;

                int ru;
                if (int.TryParse(dict["RU"], NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out ru))
                    world.ResourceUnits = ru;

                // Cleanup known placeholders
                if (world.Name == world.Name.ToUpperInvariant() && world.IsHi)
                    world.Name = Util.FixCapitalization(world.Name);

                if (worlds[world.X, world.Y] != null && errors != null)
                    errors.Warning("Duplicate World", lineNumber, line);

                if (!checker.HadError)
                {
                    worlds[world.X, world.Y] = world;
                }
            }
            catch (Exception e)
            {
                errors.Error("Parse Error: " + e.Message, lineNumber, line);
                //throw new Exception(String.Format("UWP Parse Error in line {0}:\n{1}\n{2}", lineNumber, e.Message, line));
            }
        }
    }

    internal class SecondSurveyParser : T5ParserBase
    {
        public override string Name { get { return "T5 Second Survey - Column Delimited"; } }
        public override Encoding Encoding { get { return Encoding.UTF8; } }

        public override void Parse(TextReader reader, WorldCollection worlds, ErrorLogger errors)
        {
            ColumnParser parser = new ColumnParser(reader);
            foreach (var row in parser.Data)
                ParseWorld(worlds, row.dict, row.line, row.lineNumber, errors);
        }
    }

    internal class TabDelimitedParser : T5ParserBase
    {
        public override string Name { get { return "T5 Second Survey - Tab Delimited"; } }
        public override Encoding Encoding { get { return Encoding.UTF8; } }

        public override void Parse(TextReader reader, WorldCollection worlds, ErrorLogger errors)
        {
            TSVParser parser = new TSVParser(reader);
            foreach (var row in parser.Data)
                ParseWorld(worlds, row.dict, row.line, row.lineNumber, errors);
        }
    }

    internal class TSVParser
    {
        private static readonly char[] TAB_DELIMITER = { '\t' };

        public TSVParser(TextReader reader)
        {
            int lineNumber = 0;
            string line;
            while (true)
            {
                line = reader.ReadLine();
                if (line == null)
                    return;
                ++lineNumber;

                if (line.Length == 0)
                    continue;
                if (line.StartsWith("#"))
                    continue;

                if (header == null)
                {
                    header = line.Split(TAB_DELIMITER);
                    continue;
                }

                ParseLine(line, lineNumber);
            }
        }

        private void ParseLine(string line, int lineNumber)
        {
            string[] cols = line.Split(TAB_DELIMITER);
            if (cols.Length != header.Length)
                throw new ParseException(string.Format("ERROR (Tab Parse) ({0}): {1}", lineNumber, line));

            StringDictionary dict = new StringDictionary();
            for (var i = 0; i < cols.Length; ++i)
                dict[header[i]] = cols[i].Trim();

            data.Add(new Row { dict = dict, lineNumber = lineNumber, line = line });
        }

        internal struct Row
        {
            public StringDictionary dict;
            public int lineNumber;
            public string line;
        }

        private string[] header;
        private List<Row> data = new List<Row>();
        public List<Row> Data { get { return data; } }
    }
}