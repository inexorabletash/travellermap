#nullable enable
using Json;
using Maps.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace Maps
{
    public class World
    {
        internal Sector? Sector { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Hex { get => hex.ToString(); set => hex = new Hex(value); }

        internal string SubsectorHex => hex.ToSubsectorString();
        [XmlElement("UWP"), JsonName("UWP")]
        public string UWP { get; set; } = "X000000-0";

        [XmlElement("PBG"), JsonName("PBG")]
        public string PBG { get; set; } = "000";
        public string Zone
        {
            get => zone;
            set => zone = (value == " " || value == "G") ? string.Empty : value;
        }
        private string zone = string.Empty;

        public string Bases { get; set; } = string.Empty;
        public string Allegiance { get; set; } = "Na";
        public string Stellar { get; set; } = string.Empty;

        // T5
        public string SS => "" + (char)('A' + Subsector);

        [XmlElement("Ix"), JsonName("Ix")]
        public string? Importance { get; set; }

        internal int? ImportanceValue
        {
            get
            {
                int? value = null;
                string? ix = Importance;
                if (!string.IsNullOrWhiteSpace(ix) && int.TryParse(ix?.Replace('{', ' ')?.Replace('}', ' ') ?? "",
                    NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int tmp))
                {
                    value = tmp;
                }
                return value;
            }
        }

        private int? calculatedImportance; // cache
        public int CalculatedImportance
        {
            get
            {
                if (!calculatedImportance.HasValue)
                    calculatedImportance = CalculateImportance();
                return calculatedImportance.Value;
            }
        }


        [XmlElement("Ex"), JsonName("Ex")]
        public string? Economic { get; set; }
        [XmlElement("Cx"), JsonName("Cx")]
        public string? Cultural { get; set; }
        public string? Nobility { get; set; }
        public byte Worlds { get; set; }
        public int ResourceUnits { get; set; }

        private Hex hex;
        internal byte X => hex.X;
        internal byte Y => hex.Y;
        public int Subsector => ((X - 1) / Astrometrics.SubsectorWidth) + ((Y - 1) / Astrometrics.SubsectorHeight) * 4;
        public int Quadrant => ((X - 1) / (Astrometrics.SubsectorWidth * 2)) + ((Y - 1) / (Astrometrics.SubsectorHeight * 2)) * 4;

        internal Point Coordinates
        {
            get
            {
                if (Sector == null)
                    throw new InvalidOperationException("Can't get coordinates for a world not assigned to a sector");

                return Astrometrics.LocationToCoordinates(Sector.Location, new Hex(X, Y));
            }
        }

        // For API data
        public int WorldX => Coordinates.X;
        public int WorldY => Coordinates.Y;

        public const int PBG_P = 0;
        public const int PBG_B = 1;
        public const int PBG_G = 2;

        public const int UWP_St = 0;
        public const int UWP_S = 1;
        public const int UWP_A = 2;
        public const int UWP_H = 3;
        public const int UWP_P = 4;
        public const int UWP_G = 5;
        public const int UWP_L = 6;
        public const int UWP_TL = 8;

        internal char Starport => UWP[UWP_St];
        internal int Size => Char.ToUpperInvariant(UWP[UWP_S]) == 'S' ? -1 : SecondSurvey.FromHex(UWP[UWP_S], valueIfUnknown: -1);
        internal int Atmosphere => SecondSurvey.FromHex(UWP[UWP_A], valueIfUnknown: -1);
        internal int Hydrographics => SecondSurvey.FromHex(UWP[UWP_H], valueIfUnknown: -1);
        internal int PopulationExponent => SecondSurvey.FromHex(UWP[UWP_P], valueIfUnknown: 0);
        internal int Government => SecondSurvey.FromHex(UWP[UWP_G], valueIfUnknown: 0);
        internal int Law => SecondSurvey.FromHex(UWP[UWP_L], valueIfUnknown: 0);
        internal int TechLevel => SecondSurvey.FromHex(UWP[UWP_TL], valueIfUnknown: 0);

        internal int PopulationMantissa
        {
            get
            {
                int mantissa = SecondSurvey.FromHex(PBG[PBG_P], valueIfUnknown: 0);
                // Hack for legacy data w/o PBG
                if (mantissa == 0 && PopulationExponent > 0)
                    return 1;
                return mantissa;
            }
        }

        internal int Belts => SecondSurvey.FromHex(PBG[PBG_B], valueIfUnknown: 0);
        internal int GasGiants => SecondSurvey.FromHex(PBG[PBG_G], valueIfUnknown: 0);
        internal double Population => Math.Pow(10, PopulationExponent) * PopulationMantissa;
        internal bool WaterPresent => (Hydrographics > 0) && (Atmosphere.InRange(2, 9) || Atmosphere.InRange(0xD, 0xF));

        // Only define what is needed.
        // TODO: Unify with validation checks.

        // Planetary
        internal bool IsAs => Size == 0;
        internal bool IsVa => Atmosphere == 0;

        // Population
        internal bool IsHi => PopulationExponent >= 9;

        // Economic
        internal bool IsAg => Atmosphere.InRange(4, 9) && Hydrographics.InRange(4, 8) && PopulationExponent.InRange(5, 7);
        internal bool IsIn => Atmosphere.InList(0, 1, 2, 4, 7, 9, 10, 11, 12) && PopulationExponent >= 9;
        internal bool IsRi => Atmosphere.InList(6, 8) && PopulationExponent.InList(6, 7, 8);

        internal bool IsCp => HasCode("Cp");
        internal bool IsCs => HasCode("Cs");
        internal bool IsCx => HasCode("Cx");
        internal bool IsCapital => codes.Any(s => s == "Cp" || s == "Cs" || s == "Cx" || s == "Capital");

        internal bool IsPenalColony => HasCode("Pe");
        internal bool IsPrisonExileCamp => HasCode("Px") || HasCode("Ex");         // TODO: "Pr" is used in some legacy files, conflicts with T5 "Pre-Rich" - convert codes on import/export
        internal bool IsReserve => HasCode("Re");
        internal string ResearchStation => GetCodePrefix("Rs");

        internal bool IsPlaceholder => UWP == "XXXXXXX-X" || UWP == "???????-?";
        internal bool IsAnomaly => HasCode("{Anomaly}");

        public bool HasCode(string code)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));

            return codes.Any(s => s.Equals(code, StringComparison.InvariantCultureIgnoreCase));
        }

        public string GetCodePrefix(string code)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));

            return codes.Where(s => s.StartsWith(code, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
        }

        public bool HasCodePrefix(string code)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));

            return codes.Any(s => s.StartsWith(code, StringComparison.InvariantCultureIgnoreCase));
        }

        // Keep storage as a string and parse on demand to reduce memory consumption.
        // (Many worlds * many codes = lots of strings and container overhead)
        // "[Sophont]" - major race homeworld
        // "(Sophont)" - minor race homeworld
        // "(Sophont)0" - minor race homeworld (population in tenths)
        // "Di(Sophont)" - minor race (dieback)
        // "{comment ... }" - arbitrary comment
        // "xyz" - other code
        private static readonly Tuple<string, char>[] MARKERS = new Tuple<string, char>[]
        {
            Tuple.Create("[", ']'),
            Tuple.Create("(", ')'),
            Tuple.Create("{", '}'),
            Tuple.Create("Di(", ')')
        };

        private class CodeList : IEnumerable<string>
        {
            public CodeList(string codes = "") { this.codes = codes; }
            private readonly string codes;

            public IEnumerator<string> GetEnumerator()
            {
                int pos = 0;
                while (pos < codes.Length)
                {
                    if (codes[pos] == ' ')
                    {
                        ++pos;
                        continue;
                    }

                    int begin = pos;
                    bool found = false;

                    foreach (var tuple in MARKERS)
                    {
                        string start = tuple.Item1;
                        char endchar = tuple.Item2;

                        if (codes.MatchAt(start, pos))
                        {
                            pos += start.Length;
                            pos = codes.IndexOf(endchar, pos);
                            if (pos != -1)
                                pos = codes.IndexOf(' ', pos);
                            if (pos == -1)
                                pos = codes.Length;
                            yield return codes.Substring(begin, pos - begin);
                            found = true;
                            break;
                        }
                    }
                    if (found)
                        continue;

                    pos = codes.IndexOf(' ', pos);
                    if (pos == -1)
                        pos = codes.Length;
                    yield return codes.Substring(begin, pos - begin);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public string Remarks
        {
            get => string.Join(" ", codes);
            set => codes = new CodeList(value ?? throw new ArgumentNullException(nameof(value)));
        }

        internal IEnumerable<string> Codes => codes;
        private CodeList codes = new CodeList();

        public string LegacyBaseCode
        {
            get => SecondSurvey.EncodeLegacyBases(Allegiance, Bases);
            set => Bases = SecondSurvey.DecodeLegacyBases(Allegiance, value);
        }

        internal bool IsAmber => Zone == "A" || Zone == "U";
        internal bool IsRed => Zone == "R" || Zone == "F";
        internal bool IsTNEBalkanized => Zone == "B";

        [XmlAttribute("Sector"), JsonName("Sector")]
        public string SectorName => Sector?.Names[0].Text ?? "";
        public string SubsectorName => Sector?.Subsector(Subsector)?.Name ?? "";

        public string? SectorAbbreviation => Sector?.Abbreviation;

        public string AllegianceName => Sector?.GetAllegianceFromCode(Allegiance)?.Name ?? "";

        internal string BaseAllegiance => Sector?.AllegianceCodeToBaseAllegianceCode(Allegiance) ?? Allegiance;

        internal string LegacyAllegiance => SecondSurvey.T5AllegianceCodeToLegacyCode(Allegiance);


        private static readonly Regex SOPHPOP_CODE_REGEX = new Regex(@"^(....)([0-9W])$", RegexOptions.Compiled);
        private static readonly Regex SOPHPOP_MINOR_CODE_REGEX = new Regex(@"^\((.*)\)([0-9])?$", RegexOptions.Compiled);
        private static readonly Regex SOPHPOP_MAJOR_CODE_REGEX = new Regex(@"^\[(.*)\]([0-9])?$", RegexOptions.Compiled);

        internal void Validate(ErrorLogger errors, int lineNumber, string line)
        {
            // TODO: Validate partial UWPs
            if (UWP.Contains('?') || UWP == "XXXXXXX-X") return;

            #region Helpers
            void Error(string message) { errors.Warning(message, lineNumber, line); }
            void ErrorIf(bool test, string message) { if (test) Error(message); }
            void ErrorUnless(bool test, string message) { if (!test) Error(message); }

            static bool Check(int value, string hex)
            {
                foreach (char c in hex)
                {
                    if (value == SecondSurvey.FromHex(c))
                        return true;
                }
                return false;
            }

            bool CC(string code, bool calc)
            {
                if (calc)
                    ErrorUnless(HasCode(code), $"Missing code: {code}");
                else
                    ErrorUnless(!HasCode(code), $"Extraneous code: {code}");
                return calc;
            }
            #endregion

            #region UWP

            bool customGov = BaseAllegiance == "Kk";
            bool customLaw = BaseAllegiance == "Kk";

            // UWP
            ErrorIf(Atmosphere > 15,
                $"UWP: Atm={Atmosphere} out of range; should be: 0...F");
            ErrorIf(Hydrographics > 10,
                $"UWP: Hyd={Hydrographics} out of range; should be: 0...A");
            ErrorIf(PopulationExponent > 15,
                $"UWP: Pop={PopulationExponent} out of range; should be: 0...F");
            ErrorUnless(customGov || Government.InRange(PopulationExponent - 5, Math.Max(15, PopulationExponent + 5)),
                $"UWP: Gov={Government} out of range; should be: Pop(={PopulationExponent}) + Flux");
            ErrorUnless(customLaw || Law.InRange(Government - 5, Math.Max(18, Government + 5)),
                $"UWP: Law={Law} out of range; should be: Gov(={Government}) + Flux");
            int tlmod =
                (Starport == 'A' ? 6 : 0) + (Starport == 'B' ? 4 : 0) + (Starport == 'C' ? 2 : 0) + (Starport == 'X' ? -4 : 0) +
                (Size == 0 || Size == 1 ? 2 : 0) + (Size == 2 || Size == 3 || Size == 4 ? 1 : 0) +
                (Atmosphere <= 3 ? 1 : 0) + (Atmosphere >= 10 ? 1 : 0) +
                (Hydrographics == 9 ? 1 : 0) + (Hydrographics == 10 ? 2 : 0) +
                (PopulationExponent.InRange(1, 5) ? 1 : 0) + (PopulationExponent == 9 ? 2 : 0) + (PopulationExponent >= 10 ? 4 : 0) +
                (Government == 0 || Government == 5 ? 1 : 0) + (Government == 13 ? -2 : 0);
            ErrorUnless(TechLevel.InRange(tlmod + 1, tlmod + 6) ||
                (PopulationExponent == 0 && TechLevel == 0),
                $"UWP: TL={TechLevel} out of range; should be: mods(={tlmod}) + 1D");
            #endregion

            #region Codes
#pragma warning disable IDE0059 // Unnecessary assignment of a value
            // Planetary
            bool As = CC("As", Check(Size, "0") /*&& Check(Atmosphere, "0") && Check(Hydrographics, "0")*/);
            bool De = CC("De", Check(Atmosphere, "23456789") && Check(Hydrographics, "0"));
            bool Fl = CC("Fl", Check(Atmosphere, "ABC") && Check(Hydrographics, "123456789A"));
            bool Ga = CC("Ga", Check(Size, "678") && Check(Atmosphere, "568") && Check(Hydrographics, "567"));
            bool He = CC("He", Check(Size, "3456789ABC") && Check(Atmosphere, "2479ABC") && Check(Hydrographics, "012"));
            bool Ic = CC("Ic", Check(Atmosphere, "01") && Check(Hydrographics, "123456789A"));
            bool Oc = CC("Oc", Check(Size, "ABCDEF") && Check(Atmosphere, "3456789DEF") && Check(Hydrographics, "A"));
            bool Va = CC("Va", Check(Atmosphere, "0"));
            bool Wa = CC("Wa", Check(Size, "3456789") && Check(Atmosphere, "3456789DEF") && Check(Hydrographics, "A"));

            // Population
            bool Di = (PopulationExponent == 0 /*&& Government == 0 && Law == 0*/ && TechLevel > 0);
            ErrorIf(Di && !HasCodePrefix("Di"), "Missing code: Di or Di(sophont) is required if Pop=0 and TL>0");
            bool Ba = CC("Ba", PopulationExponent == 0 /*&& Government == 0 && Law == 0*/ && TechLevel == 0);
            bool Lo = CC("Lo", Check(PopulationExponent, "123"));
            bool Ni = CC("Ni", Check(PopulationExponent, "456"));
            bool Ph = CC("Ph", Check(PopulationExponent, "8"));
            bool Hi = CC("Hi", Check(PopulationExponent, "9ABCDEF"));

            // Economic
            bool Pa = CC("Pa", Check(Atmosphere, "456789") && Check(Hydrographics, "45678") && Check(PopulationExponent, "48"));
            bool Ag = CC("Ag", Check(Atmosphere, "456789") && Check(Hydrographics, "45678") && Check(PopulationExponent, "567"));
            bool Na = CC("Na", Check(Atmosphere, "0123") && Check(Hydrographics, "0123") && Check(PopulationExponent, "6789ABCDEF"));
            bool Pi = CC("Pi", Check(Atmosphere, "012479") && Check(PopulationExponent, "78"));
            bool In = CC("In", Check(Atmosphere, "012479ABC") && Check(PopulationExponent, "9ABCDEF"));
            bool Po = CC("Po", Check(Atmosphere, "2345") && Check(Hydrographics, "0123"));
            bool Pr = CC("Pr", Check(Atmosphere, "68") && Check(PopulationExponent, "59"));
            bool Ri = CC("Ri", Check(Atmosphere, "68") && Check(PopulationExponent, "678"));
#pragma warning restore IDE0059 // Unnecessary assignment of a value

            ErrorUnless(As == IsAs, "Internal code failure: As/IsAs definitions");
            ErrorUnless(Va == IsVa, "Internal code failure: Va/IsVa definitions");
            ErrorUnless(Ag == IsAg, "Internal code failure: Ag/IsAg definitions");
            ErrorUnless(In == IsIn, "Internal code failure: In/IsIn definitions");
            ErrorUnless(Ri == IsRi, "Internal code failure: Ri/IsRi definitions");
            #endregion

            #region Sophonts
            int sophpop = 0;
            List<string> sophpops = new List<string>();
            foreach (var code in Codes)
            {
                Match m;
                string soph;
                char pop;
                if ((m = SOPHPOP_CODE_REGEX.Match(code)).Success)
                {
                    soph = m.Groups[1].Value;
                    pop = m.Groups[2].Value[0];
                    ErrorUnless(SecondSurvey.SophontCodes.Contains(soph), $"Codes: Nonstandard sophont code: {soph}");
                }
                else if ((m = SOPHPOP_MINOR_CODE_REGEX.Match(code)).Success ||
                    (m = SOPHPOP_MAJOR_CODE_REGEX.Match(code)).Success)
                {
                    soph = m.Groups[1].Value;
                    pop = m.Groups[2].Success ? m.Groups[2].Value[0] : 'W';
                    // TODO: Check species name against T5SS table?
                }
                else
                {
                    continue;
                }
                sophpops.Add(code);
                sophpop += (pop == 'W') ? 10 : ((int)pop - (int)'0');
            }
            ErrorIf(sophpop > 10, $"Codes: Sophont pop codes > 100%: {string.Join(" ", sophpops)}");
            #endregion


            // {Ix}
            int imp = 0;
            if (!string.IsNullOrWhiteSpace(Importance))
            {
                string ix = Importance?.Replace('{', ' ').Replace('}', ' ').Trim() ?? "";
                if (ix != "")
                {
                    imp = CalculateImportance();

                    ErrorUnless(Int32.Parse(ix) == imp,
                        $"{{Ix}} Importance={ix} incorrect; should be: {imp}");
                }
            }

            // (Ex)
            if (!string.IsNullOrWhiteSpace(Economic))
            {
                string ex = Economic?.Replace('(', ' ').Replace(')', ' ').Trim() ?? "";
                if (ex != "")
                {
                    int resources = SecondSurvey.FromHex(ex[0]);
                    int labor = SecondSurvey.FromHex(ex[1]);
                    int infrastructure = SecondSurvey.FromHex(ex[2]);
                    int efficiency = Int32.Parse(ex.Substring(3));

                    if (TechLevel < 8)
                        ErrorUnless(resources.InRange(2, 12),
                            $"(Ex) Resources={resources} out of range; should be: 2D if TL(={TechLevel})<8");
                    else
                        ErrorUnless(resources.InRange(2 + GasGiants + Belts, 12 + GasGiants + Belts),
                            $"(Ex) Resources={resources} out of range; should be: 2D + GG(={GasGiants}) + Belts(={Belts}) if TL(={TechLevel})=8+");

                    ErrorUnless(labor == Math.Max(0, PopulationExponent - 1),
                        $"(Ex) Labor={labor} incorrect; should be: Pop(={PopulationExponent}) - 1");

                    if (Ba)
                        ErrorUnless(infrastructure == 0,
                            $"(Ex) Infrastructure={infrastructure} incorrect; should be: 0 if Ba");
                    else if (Lo)
                        ErrorUnless(infrastructure == 1,
                            $"(Ex) Infrastructure={infrastructure} incorrect; should be: 1 if Lo");
                    else if (Ni)
                        ErrorUnless(infrastructure.InRange(Math.Max(0, imp + 1), Math.Max(0, imp + 6)),
                            $"(Ex) Infrastructure={infrastructure} out of range for Ni; should be: Imp(={imp}) + 1D");
                    else
                        ErrorUnless(infrastructure.InRange(Math.Max(0, imp + 2), Math.Max(0, imp + 12)),
                            $"(Ex) Infrastructure={infrastructure} out of range; should be: Imp(={imp}) + 2D");

                    ErrorUnless(efficiency.InRange(-5, 5),
                        $"(Ex) Efficiency={efficiency} out of range; should be: Flux");

                    ErrorIf(efficiency == 0,
                        $"(Ex) Efficiency=0 should be coded as +1 (T5SS, implied by T5.10 Book 3 pp.18)");
                }
            }

            // [Cx]
            if (!string.IsNullOrWhiteSpace(Cultural))
            {
                string cx = Cultural?.Replace('[', ' ').Replace(']', ' ').Trim() ?? "";
                if (cx != "")
                {
                    int heterogeneity = SecondSurvey.FromHex(cx[0]);
                    int acceptance = SecondSurvey.FromHex(cx[1]);
                    int strangeness = SecondSurvey.FromHex(cx[2]);
                    int symbols = SecondSurvey.FromHex(cx[3]);

                    if (PopulationExponent == 0)
                    {
                        ErrorUnless(heterogeneity == 0,
                            $"[Cx] Heterogeneity={heterogeneity} incorrect; should be: 0 if Pop=0");
                        ErrorUnless(acceptance == 0,
                            $"[Cx] Acceptance={acceptance} incorrect; should be: 0 if Pop=0");
                        ErrorUnless(strangeness == 0,
                            $"[Cx] Strangeness={strangeness} incorrect; should be: 0 if Pop=0");
                        ErrorUnless(symbols == 0,
                            $"[Cx] Symbols={symbols} incorrect; should be: 0 if Pop=0");
                    }
                    else
                    {
                        ErrorUnless(heterogeneity.InRange(Math.Max(1, PopulationExponent - 5), Math.Max(1, PopulationExponent + 5)),
                            $"[Cx] Heterogeneity={heterogeneity} out of range; should be: Pop(={PopulationExponent}) + Flux");
                        ErrorUnless(acceptance == Math.Max(1, PopulationExponent + imp),
                            $"[Cx] Acceptance={acceptance} incorrect; should be: Pop(={PopulationExponent}) + Imp(={imp})");
                        ErrorUnless(strangeness.InRange(Math.Max(1, 5 - 5), Math.Max(1, 5 + 5)),
                            $"[Cx] Strangeness={strangeness} out of range; should be: Flux + 5");
                        ErrorUnless(symbols.InRange(Math.Max(1, TechLevel - 5), Math.Max(1, TechLevel + 5)),
                            $"[Cx] Symbols={symbols} out of range; should be: TL(={TechLevel}) + Flux");
                    }
                }
            }

            // Ownership
            if (Government == 6 && !(HasCodePrefix("O:") || HasCodePrefix("Mr") ||
                HasCode("Re") || HasCode("Px") || HasCode("Pe") || HasCode("Cy")))
                Error("Gov 6 (captive/colony) missing one of: O:/Mr/Re/Px");

            // PBG
            ErrorIf(SecondSurvey.FromHex(PBG[PBG_P]) == 0 && PopulationExponent > 0,
                $"PBG: Pop Multiplier = 0 but Population Exponent (={PopulationExponent}) > 0");

            // Worlds
            int min_worlds = 1 + GasGiants + Belts;
            ErrorIf(Worlds != 0 && !((int)Worlds).InRange(min_worlds + 2, min_worlds + 12),
                $"Worlds: W={Worlds} out of range, should be MW (1) + GasGiants ({GasGiants}) + Belts ({Belts}) + 2D");

            // TODO: Nobility
        }

        private int CalculateImportance()
        {
            int imp = 0;
            if ("AB".Contains(Starport)) ++imp;
            if ("DEX".Contains(Starport)) --imp;
            if (TechLevel >= 10) ++imp;
            if (TechLevel >= 16) ++imp;
            if (TechLevel <= 8) --imp;
            if (PopulationExponent <= 6) --imp;
            if (PopulationExponent >= 9) ++imp;
            if (IsAg) ++imp;
            if (IsRi) ++imp;
            if (IsIn) ++imp;
            if (Bases == "NS" || Bases == "NW" || Bases == "W" || Bases == "X" || Bases == "D" || Bases == "RT" || Bases == "CK" || Bases == "KM" || Bases == "KV") ++imp;
            return imp;
        }

        private string? routes = null;
        [XmlIgnore,JsonIgnore]
        public string? Routes
        {
            get
            {
                if (routes == null && Sector != null)
                    routes = string.Join(" ", Sector.RoutesForWorld(this).OrderBy(s => s));
                return routes;
            }
        }
    }
}
