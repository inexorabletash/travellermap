using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Maps.Serialization
{
    internal class MSECParser : SectorMetadataFileParser 
    {
        public override Encoding Encoding { get { return Encoding.GetEncoding(1252); } }

        private static void Apply(string line, Sector sector)
        {
            string[] kv = line.Split(null, 2);
            string key = kv[0].Trim().ToUpperInvariant();
            string value = kv[1].Trim();

            if (Regex.IsMatch(key, @"^\d{4}$")) {
                // Value is full name for world in hex
                return;
            }

            if (Regex.IsMatch(key, @"^[A-P]$")) {
                Subsector ss = new Subsector();
                ss.Index = key;
                ss.Name = value;
                sector.Subsectors.Add(ss);
                return;
            }

            switch (key)
            {
                case "DOMAIN": sector.Domain = value; return;

                case "SECTOR":
                    {
                        // TODO: Add sector name
                        sector.Names.Add(new Name(value));
                        return;
                    }

                case "ALPHA": sector.AlphaQuadrant = value; return;
                case "BETA": sector.BetaQuadrant = value; return;
                case "GAMMA": sector.GammaQuadrant = value; return;
                case "DELTA": sector.DeltaQuadrant = value; return;

                case "ALLY":
                    {
                        Match match = Regex.Match(value, @"^(..)\s+(.*)$");
                        if (match.Success)
                        {
                            var code = match.Groups[1].Value;
                            var name = match.Groups[2].Value;
                            sector.Allegiances.Add(new Allegiance(code, name));
                            return;
                        }
                        break;
                    }
                case "BASE":
                    {
                        Match match = Regex.Match(value, @"^(.)\s+(..)$/"); // Base decodes to two bases
                        if (match.Success)
                        {
                            //var code = match.Groups[1].Value;
                            //var bases = match.Groups[2].Value;
                            // TODO: Base decodes
                            return;
                        }
                        match = Regex.Match(value, @"^(.)\s+(\S+)\s(\S+)\s+(.*)$");
                        if (match.Success)
                        {
                            //var code = match.Groups[1].Value;
                            //var zapf = match.Groups[2].Value;
                            //var color = match.Groups[3].Value;
                            //var name = match.Groups[4].Value;
                            // TODO: Base symbols
                            return;
                        }
                        break;
                    }
                case "REGION":
                    {
                        string[] tokens = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (!Regex.IsMatch(tokens.Last(), @"^\d{4}$"))
                            sector.Regions.Add(new Region(string.Join(" ", tokens.Take(tokens.Count() - 1)), tokens.Last()));
                        else
                            sector.Regions.Add(new Region(string.Join(" ", tokens)));
                        return;
                    }

                case "BORDER":
                    {
                        string[] tokens = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (!Regex.IsMatch(tokens.Last(), @"^\d{4}$"))
                            sector.Borders.Add(new Border(string.Join(" ", tokens.Take(tokens.Count() - 1)), tokens.Last()));
                        else
                            sector.Borders.Add(new Border(string.Join(" ", tokens)));
                        return;
                    }

                case "ROUTE":
                    {
                        var tokens = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        int cur = 0;
                        Route route = new Route();

                        if (Regex.IsMatch(tokens[cur], @"^[-+]?[01]$"))
                            route.StartOffsetX = sbyte.Parse(tokens[cur++], NumberStyles.Integer, CultureInfo.InvariantCulture);
                        if (Regex.IsMatch(tokens[cur], @"^[-+]?[01]$"))
                            route.StartOffsetY = sbyte.Parse(tokens[cur++], NumberStyles.Integer, CultureInfo.InvariantCulture);
                        route.Start = new Hex(tokens[cur++]);
                        if (Regex.IsMatch(tokens[cur], @"^[-+]?[01]$"))
                            route.EndOffsetX = sbyte.Parse(tokens[cur++], NumberStyles.Integer, CultureInfo.InvariantCulture);
                        if (Regex.IsMatch(tokens[cur], @"^[-+]?[01]$"))
                            route.EndOffsetY = sbyte.Parse(tokens[cur++], NumberStyles.Integer, CultureInfo.InvariantCulture);
                        route.End = new Hex(tokens[cur++]);
                        if (cur < tokens.Length)
                            route.ColorHtml = tokens[cur++];

                        sector.Routes.Add(route);
                        return;
                    }

                case "LABEL":
                    {
                        Match match = Regex.Match(value, @"^(..)(..)[,\/]?([\S]+)?\s+(.*)$");
                        if (match.Success)
                        {
                            var c = match.Groups[1].Value;
                            var r = match.Groups[2].Value;
                            var options = match.Groups[3].Value;
                            var text = match.Groups[4].Value;
                            Label label = new Label(int.Parse(c + r), text);
                            foreach (var option in options.ToLowerInvariant().Split(','))
                            {
                                if (option == "low")
                                {
                                    label.OffsetY = 0.85f;
                                    continue;
                                }
                                if (Regex.IsMatch(option, @"[-+](\d+)$"))
                                {
                                    int offset = 0;
                                    if (int.TryParse(option, out offset))
                                        label.OffsetY = offset / 100f;
                                    continue;
                                }
                                if (option == "right" || option == "left")
                                {
                                    // TODO: Implement
                                    continue;
                                }
                                if (option == "large" || option == "small")
                                {
                                    label.Size = option;
                                    continue;
                                }
                                if (option.StartsWith("subsec"))
                                {
                                    label.RenderType = "Subsector";
                                    continue;
                                }
                                if (option.StartsWith("quad"))
                                {
                                    label.RenderType = "Quadrant";
                                    continue;
                                }
                                if (option.StartsWith("sect"))
                                {
                                    label.RenderType = "Sector";
                                    continue;
                                }
                                if (option.StartsWith("custom"))
                                {
                                    label.RenderType = "Custom";
                                    continue;
                                }
                                if (option.Length > 0)
                                {
                                    label.ColorHtml = option;
                                }
                            }

                            sector.Labels.Add(label);
                            return;
                        }
                        break;
                    }
            }
        }

        public override Sector Parse(TextReader reader)
        {
            Sector sector = new Sector();

            StringBuilder accum = new StringBuilder();
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null)
                    break;
                if (Regex.IsMatch(line, @"^\s*$"))
                    continue;
                if (Regex.IsMatch(line, @"^\s*#"))
                    continue;
                if (Char.IsWhiteSpace(line[0]))
                {
                    accum.Append(" ");
                    accum.Append(Regex.Replace(line, @"^\s+", ""));
                    continue;
                }

                if (accum.Length > 0)
                    Apply(accum.ToString(), sector);

                accum.Clear();
                accum.Append(line);
            }
            if (accum.Length > 0)
            {
                Apply(accum.ToString(), sector);
            }

            return sector;
        }
    }
}