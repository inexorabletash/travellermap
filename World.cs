using Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace Maps
{
    public delegate bool WorldFilter(World w);

    public class World
    {
        [XmlIgnore,JsonIgnore]
        public Sector Sector { get; set; }

        public World()
        {
            // Defaults for auto-properties
            Name = "";
            UWP = "X000000-0";
            PBG = "000";
            Bases = "";
            Zone = "";
            Allegiance = "Na";
            Stellar = "";
        }

        public string Name { get; set; }
        public int Hex { get; set; }

        [XmlIgnore,JsonIgnore]
        public int SubsectorHex
        {
            get
            {
                return
                    ((X - 1) % 8 + 1) * 100 +
                    ((Y - 1) % 10 + 1);
            }
        }

        [XmlElement("UWP"),JsonName("UWP")]
        public string UWP { get; set; }

        [XmlElement("PBG"), JsonName("PBG")]
        public string PBG { get; set; }
        public string Zone { get; set; }
        
        [XmlIgnore,JsonIgnore]
        public string Bases { get; set; }
        public string Allegiance { get; set; }
        public string Stellar { get; set; }

        // T5
        public string SS
        {
            get
            {
                return "" + (char)('A' + Subsector);
            }
        }

        [XmlElement("Ix"), JsonName("Ix")]
        public string Importance { get; set; }
        [XmlElement("Ex"), JsonName("Ex")]
        public string Economic { get; set; }
        [XmlElement("Cx"), JsonName("Cx")]
        public string Cultural { get; set; }
        public string Nobility { get; set; }
        public int Worlds { get; set; }
        public int ResourceUnits { get; set; }

        // Derived
        [XmlIgnore, JsonIgnore]
        public int X { get { return Hex / 100; } }
        [XmlIgnore, JsonIgnore]
        public int Y { get { return Hex % 100; } }

        public int Subsector
        {
            get
            {
                return ((X - 1) / 8) + ((Y - 1) / 10) * 4;
            }
        }


        [XmlIgnore,JsonIgnore]
        public Point Coordinates
        {
            get
            {
                if (this.Sector == null)
                {
                    throw new Exception("Can't get coordinates for a world not assigned to a sector");
                }

                return Astrometrics.LocationToCoordinates(this.Sector.Location, this.Location);
            }
        }


        [XmlIgnore, JsonIgnore]
        public Point Location { get { return new Point(Hex / 100, Hex % 100); } }

        [XmlIgnore, JsonIgnore]
        public char Starport { get { return UWP[0]; } }
        [XmlIgnore, JsonIgnore]
        public int Size { get { return (UWP[1] == 'S' || UWP[1] == 's') ? -1 : FromHex(UWP[1]); } }
        [XmlIgnore, JsonIgnore]
        public int Atmosphere { get { return FromHex(UWP[2]); } }
        [XmlIgnore, JsonIgnore]
        public int Hydrographics { get { return FromHex(UWP[3]); } }
        [XmlIgnore, JsonIgnore]
        public int PopulationExponent { get { return FromHex(UWP[4]); } }
        [XmlIgnore, JsonIgnore]
        public int Government { get { return FromHex(UWP[5]); } }
        [XmlIgnore, JsonIgnore]
        public int Law { get { return FromHex(UWP[6]); } }
        [XmlIgnore, JsonIgnore]
        public int TechLevel { get { return FromHex(UWP[8]); } }

        [XmlIgnore, JsonIgnore]
        public int PopulationMantissa
        {
            get
            {
                int mantissa = FromHex(PBG[0]);
                if (mantissa == 0 && PopulationExponent > 0)
                {
                    // Hack for legacy data w/o PBG
                    return 1;
                }
                return mantissa;
            }
        }
        
        [XmlIgnore, JsonIgnore]
        public int Belts { get { return FromHex(PBG[1]); } }

        [XmlIgnore, JsonIgnore]
        public int GasGiants { get { return FromHex(PBG[2]); } }

        [XmlIgnore, JsonIgnore]
        public double Population { get { return Math.Pow(10, PopulationExponent) * PopulationMantissa; } }

        [XmlIgnore, JsonIgnore]
        public bool WaterPresent { get { return (Hydrographics > 0) && (Atmosphere > 1) && (Atmosphere < 10); } }

        [XmlIgnore, JsonIgnore]
        public bool IsBa { get { return PopulationExponent == 0; } }
        [XmlIgnore, JsonIgnore]
        public bool IsLo { get { return PopulationExponent < 4; } }
        [XmlIgnore, JsonIgnore]
        public bool IsHi { get { return PopulationExponent >= 9; } }

        [XmlIgnore, JsonIgnore]
        public bool IsAg { get { return Util.InRange(Atmosphere, 4, 9) && Util.InRange(Hydrographics, 4, 8) && Util.InRange(PopulationExponent, 5, 7); } }
        [XmlIgnore, JsonIgnore]
        public bool IsNa { get { return Util.InRange(Atmosphere, 0, 3) && Util.InRange(Hydrographics, 0, 3) && Util.InRange(PopulationExponent, 6, 10); } }
        [XmlIgnore, JsonIgnore]
        public bool IsIn { get { return Util.InList(Atmosphere, 0, 1, 2, 4, 7, 9) && Util.InList(PopulationExponent, 9, 10); } }
        [XmlIgnore, JsonIgnore]
        public bool IsNi { get { return Util.InRange(PopulationExponent, 1, 6); } }
        [XmlIgnore, JsonIgnore]
        public bool IsRi { get { return Util.InList(Atmosphere, 6, 7, 8) && Util.InList(PopulationExponent, 6, 7, 8) && Util.InList(Government, 4, 5, 6, 7, 8, 9); } }
        [XmlIgnore, JsonIgnore]
        public bool IsPo { get { return Util.InList(Atmosphere, 2, 3, 4, 5) && Util.InList(Hydrographics, 0, 1, 2, 3) && PopulationExponent > 0; } }

        [XmlIgnore, JsonIgnore]
        public bool IsWa { get { return Hydrographics == 10; } }
        [XmlIgnore, JsonIgnore]
        public bool IsDe { get { return Util.InRange(Atmosphere, 2, 10) && Hydrographics == 0; } }
        [XmlIgnore, JsonIgnore]
        public bool IsAs { get { return Size == 0; } }
        [XmlIgnore, JsonIgnore]
        public bool IsVa { get { return Util.InRange(Size, 1, 10) && Atmosphere == 0; } }
        [XmlIgnore, JsonIgnore]
        public bool IsIc { get { return Util.InList(Atmosphere, 0, 1) && Util.InRange(Hydrographics, 1, 10); } }
        [XmlIgnore, JsonIgnore]
        public bool IsFl { get { return Atmosphere == 10 && Util.InRange(Hydrographics, 1, 10); } }

        [XmlIgnore, JsonIgnore]
        public bool IsCp { get { return HasCode("Cp"); } }
        [XmlIgnore, JsonIgnore]
        public bool IsCs { get { return HasCode("Cs"); } }
        [XmlIgnore, JsonIgnore]
        public bool IsCx { get { return HasCode("Cx"); } }

        [XmlIgnore, JsonIgnore]
        public bool IsPrison { get { return HasCode("Pr"); } }
        [XmlIgnore, JsonIgnore]
        public bool IsPenalColony { get { return HasCode("Pe"); } }
        [XmlIgnore, JsonIgnore]
        public bool IsReserve { get { return HasCode("Re"); } }
        [XmlIgnore, JsonIgnore]
        public bool IsExileCamp { get { return HasCode("Ex") || HasCode("Pr"); } }
        [XmlIgnore, JsonIgnore]
        public string ResearchStation { get { return HasCodePrefix("Rs"); } }

        [XmlIgnore, JsonIgnore]
        public bool IsCapital
        {
            get
            {
                return m_codes.Any(s => s == "Cp" || s == "Cs" || s == "Cx" || s == "Capital");
            }
        }

        public bool HasCode(string code)
        {
            if (code == null)
                throw new ArgumentNullException("code");

            return m_codes.Any(s => s.Equals(code, StringComparison.InvariantCultureIgnoreCase));
        }

        public string HasCodePrefix(string code)
        {
            if (code == null)
                throw new ArgumentNullException("code");

            return m_codes.Where(s => s.StartsWith(code, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
        }

        // "(foo bar)" "(foo)9" "baz" "bat"
        private static Regex codeRegex = new Regex(@"(\(.*?\)\S*|\S+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        public string Remarks
        {
            get { return String.Join(" ", m_codes); }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("codes");
                m_codes.Clear();
                m_codes.AddRange(codeRegex.Matches(value).Cast<Match>().Select(match => match.Groups[1].Value));
            }
        }

        [XmlIgnore, JsonIgnore]
        public IEnumerable<string> Codes { get { return m_codes; } }

        private List<string> m_codes = new List<string>();

        [XmlElement("Bases"), JsonName("Bases")]
        public string CompactLegacyBases
        {
            get { return EncodeLegacyBases(this.Allegiance, Bases); }
            set { Bases = DecodeLegacyBases(this.Allegiance, value); }
        }

        [XmlIgnore, JsonIgnore]
        public bool IsAmber { get { return Zone == "A" || Zone == "U"; } }
        [XmlIgnore, JsonIgnore]
        public bool IsRed { get { return Zone == "R" || Zone == "F"; } }
        [XmlIgnore, JsonIgnore]
        public bool IsBlue { get { return Zone == "B"; } } // TNE Technologically Elevated Dictatorship

        private const string HEX = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        // Decimal hi:              0000000000111111111122222222223333
        // Decimal lo:              0123456789012345678901234567890123

        private static char ToHex(int c)
        {
            if (c == -1)
            {
                return 'S'; // Hack for "small" worlds
            }

            if (0 <= c && c < HEX.Length)
            {
                return HEX[c];
            }

            throw new ArgumentOutOfRangeException(String.Format(CultureInfo.InvariantCulture, "Value out of range: '{0}'", c), "c");
        }

        private static int FromHex(char c)
        {
            c = Char.ToUpperInvariant(c);
            int value = HEX.IndexOf(c);
            if (value != -1)
                return value;
            switch (c)
            {
                case '_': return 0; // Unknown
                case '?': return 0; // Unknown
                case 'O': return 0; // Typo found in some data files
                case 'I': return 1; // Typo found in some data files
            }
            throw new ArgumentOutOfRangeException(String.Format(CultureInfo.InvariantCulture, "Character out of range: '{0}'", c), "c");
        }

        // Bases should be string containing zero or more of: CDLNPSW

        private static RegexDictionary<string> s_legacyBaseDecodeTable = new GlobDictionary<string> {
            // Overrides
            { "Zh.D", "Y" }, // Zhodani Depot (map T5 to legacy Y for distinct glyph }
            { "Zh.F", "Z" }, // Zhodani Base (map T5 to legacy Z for distinct glyph }
            { "So.K", "JM" }, // Solomani Naval and Planetary Base (map T5 to legacy }
            { "Hv.F", "LM" }, // Hiver Military & Naval Base
            { "So.F", "J" }, // Solomani Naval Base

            // Legacy Decodes
            { "*.2", "NS" }, // Imperial Naval Base + Scout Base
            { "*.A", "NS" }, // (ditto }
            { "*.B", "NW" }, // Imperial Naval Base + Scout Waystation
            { "*.F", "JM" }, // Military & Naval Base
            { "V?.H", "CG" }, // Vargr Corsair Base + Naval Base
            { "A?.U", "RT" }, // Aslan Tlaukhu Base & Clan Base
        };

        private static RegexDictionary<string> s_t5BaseDecodeTable = new GlobDictionary<string> {
            // Overrides - check if still needed
            { "Zh.D", "Y" }, // Zhodani Depot (map T5 to legacy Y for distinct glyph }
            { "Zh.F", "Z" }, // Zhodani Base (map T5 to legacy Z for distinct glyph }
            { "So.K", "JM" }, // Solomani Naval and Planetary Base (map T5 to legacy }

            // T5 Codes
            { "*.A", "NS" },
            { "*.B", "NW" },
            // *.C == Corsair
            // *.D == Depot
            { "*.E", "SL" },
            { "*.F", "WL" },
            { "*.G", "LC" },
            { "*.H", "NC" },
            { "*.J", "PL" },
            { "*.K", "NP" },
            // *.L == Minor Naval
            { "*.M", "NL" },
            // *.N == Naval
            // *.P == Planetary
            { "*.Q", "PC" },
            { "*.R", "SP" },
            // *.S == Scout
            // *.T == (reserved }
            // *.U == (reserved }
            // *.V == (reserved }
            // *.W == Way Station
            { "*.X", "WP" },
            // *.Y == (reserved }
            // *.Z == (reserved }
        };

        // TODO: This should be in the SEC file parser, but it complicates the formatting code
        private static string DecodeLegacyBases(string allegiance, string code)
        {
            string match = s_legacyBaseDecodeTable.Match(allegiance + "." + code);
            return (match != default(string)) ? match : code;
        }

        private static RegexDictionary<string> s_legacyBaseEncodeTable = new GlobDictionary<string> {
            // Legacy Encodes
            { "*.NS", "A" }, // Naval Base + Scout Base
            { "*.NW", "B" }, // Naval Base + Scout Waystation,
            { "*.JM", "F" }, // Mlitary & Naval Base,
            { "V?.CG", "H" }, // Vargr Corsair Base + Naval Base,
            { "A?.RT", "U" }, // Aslan Tlaukhu Base & Clan Base,
            { "Hv.LM", "F" }, // Hiver Military & Naval Base
        };

        // TODO: This should be in the SEC file parser, but it complicates the formatting code
        private static string EncodeLegacyBases(string allegiance, string bases)
        {
            string match = s_legacyBaseEncodeTable.Match(allegiance + "." + bases);
            return (match != default(String)) ? match : bases;
        }

        [XmlIgnore,JsonIgnore]
        public string BaseAllegiance { get { return this.Sector != null ? Sector.GetBaseAllegianceCode(this.Allegiance) : this.Allegiance; } }

        private static readonly HashSet<string> DefaultAllegiances = new HashSet<string>(new string[] { 
            // NOTE: Do not use this for autonomous/cultural regional codes (e.g. Vegan, Sylean, etc). 
            // Use <Allegiance Code="Ve" Base="Im">Vegan Autonomous Region</Allegiance> in metadata instead
            "Im", // Classic Imperium
            "--", // Placeholder - show as blank
        }, StringComparer.InvariantCultureIgnoreCase);

        public bool HasDefaultAllegiance()
        {
            return DefaultAllegiances.Contains(this.Allegiance);
        }
    }
}
