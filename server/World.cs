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

        public string Hex
        {
            get { return Astrometrics.IntToHex(X * 100 + Y); }
            set { int hex = Astrometrics.HexToInt(value); X = hex / 100; Y = hex % 100; }
        }

        [XmlIgnore,JsonIgnore]
        public string SubsectorHex
        {
            get { return Astrometrics.IntToHex(((X - 1) % Astrometrics.SubsectorWidth + 1) * 100 + ((Y - 1) % Astrometrics.SubsectorHeight + 1)); }
        }

        [XmlElement("UWP"),JsonName("UWP")]
        public string UWP { get; set; }

        [XmlElement("PBG"), JsonName("PBG")]
        public string PBG { get; set; }
        public string Zone
        {
            get { return m_zone; }
            set
            {
                m_zone = (value == " " || value == "G") ? String.Empty : value;
            }
        }
        private string m_zone;

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

        [XmlIgnore, JsonIgnore]
        public int? ImportanceValue
        {
            get
            {
                int? value = null;
                int tmp;
                string ix = Importance;
                if (!String.IsNullOrWhiteSpace(ix) && Int32.TryParse(ix.Replace('{', ' ').Replace('}', ' '),
                    NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out tmp))
                {
                    value = tmp;
                }
                return value;
            }
        }

        [XmlElement("Ex"), JsonName("Ex")]
        public string Economic { get; set; }
        [XmlElement("Cx"), JsonName("Cx")]
        public string Cultural { get; set; }
        public string Nobility { get; set; }
        public int Worlds { get; set; }
        public int ResourceUnits { get; set; }

        // Derived
        [XmlIgnore, JsonIgnore]
        public int X { get; set; }
        [XmlIgnore, JsonIgnore]
        public int Y { get; set; }

        public int Subsector
        {
            get
            {
                return ((X - 1) / Astrometrics.SubsectorWidth) + ((Y - 1) / Astrometrics.SubsectorHeight) * 4;
            }
        }

        public int Quadrant
        {
            get
            {
                return ((X - 1) / (Astrometrics.SubsectorWidth * 2)) + ((Y - 1) / (Astrometrics.SubsectorHeight * 2)) * 4;
            }
        }


        [XmlIgnore,JsonIgnore]
        public Point Coordinates
        {
            get
            {
                if (this.Sector == null)
                    throw new InvalidOperationException("Can't get coordinates for a world not assigned to a sector");

                return Astrometrics.LocationToCoordinates(this.Sector.Location, this.Location);
            }
        }


        [XmlIgnore, JsonIgnore]
        public Point Location { get { return new Point(X, Y); } }

        [XmlIgnore, JsonIgnore]
        public char Starport { get { return UWP[0]; } }
        [XmlIgnore, JsonIgnore]
        public int Size { get { return Char.ToUpperInvariant(UWP[1]) == 'S' ? -1 : SecondSurvey.FromHex(UWP[1]); } }
        [XmlIgnore, JsonIgnore]
        public int Atmosphere { get { return SecondSurvey.FromHex(UWP[2]); } }
        [XmlIgnore, JsonIgnore]
        public int Hydrographics { get { return SecondSurvey.FromHex(UWP[3]); } }
        [XmlIgnore, JsonIgnore]
        public int PopulationExponent { get { return SecondSurvey.FromHex(UWP[4], valueIfX: 0); } }
        [XmlIgnore, JsonIgnore]
        public int Government { get { return SecondSurvey.FromHex(UWP[5]); } }
        [XmlIgnore, JsonIgnore]
        public int Law { get { return SecondSurvey.FromHex(UWP[6]); } }
        [XmlIgnore, JsonIgnore]
        public int TechLevel { get { return SecondSurvey.FromHex(UWP[8]); } }

        [XmlIgnore, JsonIgnore]
        public int PopulationMantissa
        {
            get
            {
                int mantissa = SecondSurvey.FromHex(PBG[0], valueIfX: 0);
                // Hack for legacy data w/o PBG
                if (mantissa == 0 && PopulationExponent > 0)
                    return 1;
                return mantissa;
            }
        }

        [XmlIgnore, JsonIgnore]
        public int Belts { get { return SecondSurvey.FromHex(PBG[1]); } }

        [XmlIgnore, JsonIgnore]
        public int GasGiants { get { return SecondSurvey.FromHex(PBG[2]); } }

        [XmlIgnore, JsonIgnore]
        public double Population { get { return Math.Pow(10, PopulationExponent) * PopulationMantissa; } }

        [XmlIgnore, JsonIgnore]
        public bool WaterPresent { get { return (Hydrographics > 0) && (Util.InRange(Atmosphere, 2, 9) || Util.InRange(Atmosphere, 0xD, 0xF)); } }

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
        public bool IsPenalColony { get { return HasCode("Pe"); } }
        [XmlIgnore, JsonIgnore]
        public bool IsReserve { get { return HasCode("Re"); } }
        [XmlIgnore, JsonIgnore]
        public bool IsPrisonExileCamp { get { return HasCode("Px") || HasCode("Ex"); } } // Px is T5, Ex is legacy
        // TODO: "Pr" is used in some legacy files, conflicts with T5 "Pre-Rich" - convert codes on import/export
        [XmlIgnore, JsonIgnore]
        public string ResearchStation { get { return HasCodePrefix("Rs"); } }

        [XmlIgnore, JsonIgnore]
        public bool IsPlaceholder { get { return UWP == "XXXXXXX-X"; } }

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

            // TODO: Performance impact - this must be an O(n) lookup bypassing the hash
            return m_codes.Any(s => s.Equals(code, StringComparison.InvariantCultureIgnoreCase));
        }

        public string HasCodePrefix(string code)
        {
            if (code == null)
                throw new ArgumentNullException("code");

            return m_codes.Where(s => s.StartsWith(code, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
        }

        public void UpdateCode(string oldCode, string newCode)
        {
            if (m_codes.Contains(oldCode))
            {
                m_codes.Remove(oldCode);
                m_codes.Add(newCode);
            }
        }

        // "[Sophont]" - major race homeworld
        // "(Sophont)" - minor race homeworld
        // "(Sophont)0" - minor race homeworld (population in tenths)
        // "{comment ... }" - arbitrary comment
        // "xyz" - other code
        private static Regex codeRegex = new Regex(@"(\(.*?\)\S*|\[.*?\]\S*|\{.*?\}\S*|\S+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
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

        private ListHashSet<string> m_codes = new ListHashSet<string>();

        public string LegacyBaseCode
        {
            get { return SecondSurvey.EncodeLegacyBases(this.Allegiance, Bases); }
            set { Bases = SecondSurvey.DecodeLegacyBases(this.Allegiance, value); }
        }

        [XmlIgnore, JsonIgnore]
        public bool IsAmber { get { return Zone == "A" || Zone == "U"; } }
        [XmlIgnore, JsonIgnore]
        public bool IsRed { get { return Zone == "R" || Zone == "F"; } }
        [XmlIgnore, JsonIgnore]
        public bool IsBlue { get { return Zone == "B"; } } // TNE Technologically Elevated Dictatorship

        [XmlAttribute("Sector"), JsonName("Sector")]
        public string SectorName { get { return this.Sector.Names[0].Text; } }

        public string SubsectorName
        {
            get
            {
                var ss = this.Sector[this.Subsector];
                return ss == null ? "" : ss.Name;
            }
        }

        public string AllegianceName
        {
            get
            {
                if (this.Sector == null)
                    return "";
                var allegiance = this.Sector.GetAllegianceFromCode(this.Allegiance);
                return allegiance == null ? "" : allegiance.Name;
            }
        }

        [XmlIgnore, JsonIgnore]
        public string BaseAllegiance
        {
            get
            {
                if (this.Sector == null)
                    return this.Allegiance;
                return Sector.AllegianceCodeToBaseAllegianceCode(this.Allegiance);
            }
        }

        [XmlIgnore, JsonIgnore]
        public string LegacyAllegiance
        {
            get
            {
                return SecondSurvey.T5AllegianceCodeToLegacyCode(this.Allegiance);
            }
        }

    }
}
