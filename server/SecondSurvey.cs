using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Globalization;

namespace Maps
{
    public static class SecondSurvey
    {
        #region eHex
        private const string HEX = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        // Decimal hi:              0000000000111111111122222222223333
        // Decimal lo:              0123456789012345678901234567890123

        public static char ToHex(int c)
        {
            if (c == -1)
                return 'S'; // Hack for "small" worlds

            if (0 <= c && c < HEX.Length)
                return HEX[c];

            throw new ArgumentOutOfRangeException(String.Format(CultureInfo.InvariantCulture, "Value out of range: '{0}'", c), "c");
        }

        public static int FromHex(char c)
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
        #endregion // eHex

        #region Bases
        // Bases should be string containing zero or more of: CDKMNRSTWXZ (plus nonstandard E, O)

        // Code  Owner      Description
        // ----  ---------  ------------------------
        // C     Vargr      Corsair base
        // D     Any        Depot
        // E     Hiver      Embassy -------------- NONSTANDARD
        // K     Any        Naval base
        // M     Any        Military base
        // N     Imperial   Naval base
        // O     K'kree     Naval Outpost -------- NONSTANDARD
        // R     Aslan      Clan base
        // S     Imperial   Scout base
        // T     Aslan      Tlaukhu base
        // V     Any        Exploration base
        // W     Imperial   Way station
        // X     Zhodani    Relay station

        private static RegexDictionary<string> s_legacyBaseDecodeTable = new GlobDictionary<string> {
            { "*.2", "NS" },  // Imperial Naval base + Scout base
            { "*.A", "NS" },  // Imperial Naval base + Scout base
            { "*.B", "NW" },  // Imperial Naval base + Scout Way station
            { "*.C", "C" },   // Vargr Corsair base
            { "*.D", "D" },   // Depot
            { "*.E", "E"},    // Hiver Embassy            - TODO: Approved T5SS code for Embassy
            { "So.F", "K" },  // Solomani Naval Base
            { "*.F", "KM" },  // Military & Naval Base
            { "*.G", "K" },   // Vargr Naval Base
            { "*.H", "CK" },  // Vargr Corsair Base + Naval Base
            { "*.J", "K" },   // Naval Base
            { "So.K", "KM" }, // Solomani Naval and Planetary Base
            { "*.K", "K" },   // K'kree Naval Base
            { "*.L", "K" },   // Hiver Naval Base
            { "*.M", "M" },   // Military base
            { "*.N", "N" },   // Naval base
            { "*.O", "O" },   // K'kree Naval Outpost     - TODO: Approved T5SS code for Outpost
            { "*.P", "K" },   // Droyne Naval Base
            { "*.Q", "M" },   // Droyne Military Garrison
            { "*.R", "R" },   // Aslan Clan Base
            { "*.S", "S" },   // Imperial Scout Base
            { "*.T", "T" },   // Aslan Tlaukhu Base
            { "*.U", "RT" },  // Aslan Tlaukhu and Clan Base
            { "*.V", "V" },   // Scout/Exploration
            { "*.W", "W" },   // Imperial Scout Way Station
            { "*.X", "X" },   // Zhodani Relay Station
            { "*.Y", "D" },   // Zhodani Depot
            { "*.Z", "KM" },   // Zhodani Naval/Military Base
        };

        public static string DecodeLegacyBases(string allegiance, string code)
        {
            allegiance = AllegianceCodeToBaseAllegianceCode(allegiance);
            string match = s_legacyBaseDecodeTable.Match(allegiance + "." + code);
            return (match != default(string)) ? match : code;
        }

        private static RegexDictionary<string> s_legacyBaseEncodeTable = new GlobDictionary<string> {
            { "*.NS", "A" },  // Imperial Naval base + Scout base
            { "*.NW", "B" },  // Imperial Naval base + Scout Way station
            { "*.C", "C" },   // Vargr Corsair base
            { "Zh.D", "Y" }, // Zhodani Depot
            { "*.D", "D" },   // Depot
            { "*.E", "E"},   // Hiver Embassy             - TODO: Approved T5SS code for Embassy
            { "*.KM", "F" },  // Military & Naval Base
            { "So.K", "F" },  // Solomani Naval Base
            { "V*.K", "G" },   // Vargr Naval Base
            { "*.CK", "H" },  // Vargr Corsair Base + Naval Base
            { "So.KM", "K" }, // Solomani Naval and Planetary Base
            { "Kk.K", "K" },   // K'kree Naval Base
            { "Hv.K", "L" },   // Hiver Naval Base
            { "Dr.K", "P" },   // Droyne Naval Base
            { "*.K", "J" },   // Naval Base
            { "*.M", "M" },   // Military base
            { "*.N", "N" },   // Naval base
            { "*.O", "O" },   // K'kree Naval Outpost     - TODO: Approved T5SS code for Outpost
            { "Dr.M", "Q" },   // Droyne Military Garrison
            { "*.R", "R" },   // Aslan Clan Base
            { "*.S", "S" },   // Imperial Scout Base
            { "*.T", "T" },   // Aslan Tlaukhu Base
            { "*.RT", "U" },  // Aslan Tlaukhu and Clan Base
            { "*.V", "V" },   // Exploration
            { "*.W", "W" },   // Imperial Scout Way Station
            { "*.X", "X" }, // Zhodani Relay Station
            { "Zh.KM", "Z" }, // Zhodani Naval/Military Base
        };

        public static string EncodeLegacyBases(string allegiance, string bases)
        {
            allegiance = AllegianceCodeToBaseAllegianceCode(allegiance);
            string match = s_legacyBaseEncodeTable.Match(allegiance + "." + bases);
            return (match != default(String)) ? match : bases;
        }
        #endregion // Bases

        #region Allegiance

        private class LegacyAllegiances : Dictionary<string, Allegiance>
        {
            public void Add(string code, string name)
            {
                this.Add(code, new Allegiance(code, name));
            }
        }
        private static LegacyAllegiances s_stockAllegiances = new LegacyAllegiances {
            { "As", "Aslan Hierate" },
            { "Cs", "Imperial Client State" },
            { "Dr", "Droyne" },
            { "Hv", "Hive Federation" },
            { "Im", "Third Imperium" },
            { "J-", "Julian Protectorate" },
            { "Jp", "Julian Protectorate" },
            { "Kk", "The Two Thousand Worlds" },
            { "Na", "Non-Aligned" },
            { "So", "Solomani Confederation" },
            { "Va", "Vargr (Non-Aligned)" },
            { "Zh", "Zhodani Consulate" },

            { "A0", "Yerlyaruiwo Tlaukhu Bloc" },
            { "A1", "Khaukeairl Tlaukhu Bloc" },
            { "A2", "Syoisuis Tlaukhu Bloc" },
            { "A3", "Tralyeaeawi Tlaukhu Bloc" },
            { "A4", "Eakhtiyho Tlaukhu Bloc" },
            { "A5", "Hlyueawi/Isoitiyro Tlaukhu Bloc" },
            { "A6", "Uiktawa Tlaukhu Bloc" },
            { "A7", "Ikhtealyo Tlaukhu Bloc" },
            { "A8", "Seieakh Tlaukhu Bloc" },
            { "A9", "Aokhalte Tlaukhu Bloc" }
        };

        public static Allegiance GetStockAllegianceFromCode(string code)
        {
            if (code == null)
                return null;
            if (s_t5Allegiances.ContainsKey(code))
                return s_t5Allegiances[code];
            if (s_legacyAllegianceToT5.ContainsKey(code))
                return s_t5Allegiances[s_legacyAllegianceToT5[code]];
            if (s_stockAllegiances.ContainsKey(code))
                return s_stockAllegiances[code];
            return null;
        }

        // TODO: This discounts the Sector's allegiance/base definitions, if any.
        public static string AllegianceCodeToBaseAllegianceCode(string code)
        {
            Allegiance alleg = GetStockAllegianceFromCode(code);
            if (alleg == null)
                return code;
            if (String.IsNullOrEmpty(alleg.Base))
                return code;
            return alleg.Base;
        }

        public static string T5AllegianceCodeToLegacyCode(string t5code)
        {
            if (!s_t5Allegiances.ContainsKey(t5code))
                return t5code;
            return s_t5Allegiances[t5code].LegacyCode;
        }

        private static StringDictionary s_legacyAllegianceToT5 = new StringDictionary {
            // { "As", "AsXX" }, // Acceptable for a world, but not a polity
            { "A0", "AsT0" },
            { "A1", "AsT1" },
            { "A2", "AsT2" },
            { "A3", "AsT3" },
            { "A4", "AsT4" },
            { "A5", "AsT5" },
            { "A6", "AsT6" },
            { "A7", "AsT7" },
            { "A8", "AsT8" },
            { "A9", "AsT9" },
            { "Cs", "CsIm" },
            { "Cz", "CsZh" },
            { "J-", "JuPr" },
            { "Jp", "JuPr" },
            { "Ju", "JuPr" },
            { "Na", "NaHu" }, // Reasonable? NaXX is "unclaimed"
            { "So", "SoCf" },
            { "Va", "NaVa" },
            { "Zh", "ZhCo" },
            { "--", "XXXX" }
        };
        public static string LegacyAllegianceToT5(string code, Sector unused)
        {
            // unused sector argument to force conversion to only take place when done in the context of a sector
            // which may define overrides
            if (s_legacyAllegianceToT5.ContainsKey(code))
                return s_legacyAllegianceToT5[code];
            return code;
        }

        // TODO: Parse this from data file
        private class T5Allegiances : Dictionary<string, Allegiance>
        {
            public void Add(string t5code, string code, string baseCode, string name)
            {
                this.Add(t5code, new Allegiance(t5code, code, baseCode, name));
            }
        }
        private static T5Allegiances s_t5Allegiances = new T5Allegiances {
            // T5Code, LegacyCode, BaseCode, Name
            { "AnTC", "Ac", null, "Anubian Trade Coalition" },
            { "AsIf", "As", "As", "Iyeaao'fte" },
            { "AsMw", "As", "As", "Aslan Hierate, single multiple-world clan dominates" },
            { "AsOf", "As", "As", "Oleaiy'fte" }, // (Tlaukhu client state)
            { "AsSc", "As", "As", "Aslan Hierate, multiple clans split control" },
            { "AsSF", "As", "As", "Aslan Hierate, small station" }, // (temporary)
            { "AsT0", "A0", "As", "Aslan Hierate, Tlaukhu control, Yerlyaruiwo (1), Hrawoao (13), Eisohiyw (14), Ferekhearl (19)" },
            { "AsT1", "A1", "As", "Aslan Hierate, Tlaukhu control, Khauleairl (2), Estoieie' (16), Toaseilwi (22)" },
            { "AsT2", "A2", "As", "Aslan Hierate, Tlaukhu control, Syoisuis (3)" },
            { "AsT3", "A3", "As", "Aslan Hierate, Tlaukhu control, Tralyeaeawi (4), Yulraleh (12), Aiheilar (25), Riyhalaei (28)" },
            { "AsT4", "A4", "As", "Aslan Hierate, Tlaukhu control, Eakhtiyho (5), Eteawyolei' (11), Fteweyeakh (23)" },
            { "AsT5", "A5", "As", "Aslan Hierate, Tlaukhu control, Hlyueawi (6), Isoitiyro (15)" },
            { "AsT6", "A6", "As", "Aslan Hierate, Tlaukhu control, Uiktawa (7), Iykyasea (17), Faowaou (27)" },
            { "AsT7", "A7", "As", "Aslan Hierate, Tlaukhu control, Ikhtealyo (8), Tlerfearlyo (20), Yehtahikh (24)" },
            { "AsT8", "A8", "As", "Aslan Hierate, Tlaukhu control, Seieakh (9), Akatoiloh (18), We'okunir (29)" },
            { "AsT9", "A9", "As", "Aslan Hierate, Tlaukhu control, Aokhalte (10), Sahao' (21), Ouokhoi (26)" },
            { "AsTv", "As", "As", "Aslan Hierate, Tlaukhu vassal clan dominates" },
            { "AsTz", "As", "As", "Aslan Hierate, Zodia clan" }, // (Tralyeaeawi vassal)
            { "AsVc", "As", "As", "Aslan Hierate, vassal clan dominates" },
            { "AsWc", "As", "As", "Aslan Hierate, single one-world clan dominates" },
            { "AsXX", "As", "As", "Aslan Hierate, unknown" },
            { "BlSo", "Bs", null, "Belgardian Sojurnate" },
            { "CaAs", "Cb", null, "Carrillian Assembly" },
            { "CaPr", "Ca", null, "Principality of Caledon" },
            { "CaTe", "Ct", null, "Carter Technocracy" },
            { "CoLp", "Lp", null, "Council of Leh Perash" },
            { "CsCa", "Ca", null, "Client State, Principality of Caledon" },
            { "CsIm", "Cs", null, "Client State, Third Imperium" },
            { "CsZh", "Cz", null, "Client State, Zhodani Consulate" },
            { "CyUn", "Cu", null, "Cytralin Unity" },
            { "DaCf", "Da", null, "Darrian Confederation" },
            { "DuCf", "Cd", null, "Confederation of Duncinae" },
            { "FeHe", "Fh", null, "Federation of Heron" },
            { "FlLe", "Fl", null, "Florian League" },
            { "GaRp", "Gr", null, "Gamma Republic" },
            { "GdMh", "Ma", null, "Grand Duchy of Marlheim" },
            { "GdSt", "Gs", null, "Grand Duchy of Stoner" },
            { "GeOr", "Go", null, "Gerontocracy of Ormine" },
            { "GlEm", "Gl", "As", "Glorious Empire" }, // (Aslan independent clan, outcast)
            { "GlFe", "Gf", null, "Glimmerdrift Federation" },
            { "GnCl", "Gi", null, "Gniivi Collective" },
            { "ImAp", "Im", "Im", "Third Imperium, Amec Protectorate" },
            { "ImDa", "Im", "Im", "Third Imperium, Domain of Antares" },
            { "ImDc", "Im", "Im", "Third Imperium, Domain of Sylea" },
            { "ImDd", "Im", "Im", "Third Imperium, Domain of Deneb" },
            { "ImDg", "Im", "Im", "Third Imperium, Domain of Gateway" },
            { "ImDi", "Im", "Im", "Third Imperium, Domain of Ilelish" },
            { "ImDs", "Im", "Im", "Third Imperium, Domain of Sol" },
            { "ImDv", "Im", "Im", "Third Imperium, Domain of Vland" },
            { "ImLa", "Im", "Im", "Third Imperium, League of Antares" },
            { "ImLu", "Im", "Im", "Third Imperium, Luriani Cultural Association" },
            { "ImSy", "Im", "Im", "Third Imperium, Sylean Worlds" },
            { "ImVd", "Ve", "Im", "Third Imperium, Vegan Autonomous District" },
            { "IsDo", "Id", null, "Islaiat Dominate" },
            { "JuHl", "Hl", "Jp", "Julian Protectorate, Hegemony of Lorean" },
            { "JuPr", "Jp", "Jp", "Julian Protectorate, independent" },
            { "JuRu", "Jr", "Jp", "Julian Protectorate, Rukadukaz Republic" },
            { "KhLe", "Kl", null, "Khuur League" },
            { "LnRp", "Ln", null, "Loyal Nineworlds Republic" },
            { "LyCo", "Ly", null, "Lanyard Colonies" },
            { "MaEm", "Mk", null, "Maskai Empire" },
            { "NaAs", "As", "As", "Non-Aligned, Aslan-dominated" }, // (outside Hierate)
            { "NaHu", "Na", null, "Non-Aligned, Human-dominated" },
            { "NaVa", "Va", null, "Non-Aligned, Vargr-dominated" },
            { "NaXX", "Na", null, "Unclaimed" },
            { "OcWs", "Ow", null, "Outcasts of the Whispering Sky" },
            { "RaRa", "Ra", null, "Ral Ranta" },
            { "SeFo", "Sf", null, "Senlis Foederate" },
            { "SlLg", "Sl", null, "Shukikikar League" },
            { "SoCf", "So", "So", "Solomani Confederation" },
            { "StCl", "Sc", null, "Strend Cluster" },
            { "SwCf", "Sw", null, "Sword Worlds Confederation" },
            { "TeAr", "Ta", "As", "Tealou Arlaoh" }, // (Aslan independent clan, non-outcast)
            { "UnHa", "Uh", null, "Union of Harmony" },
            { "V40S", "Ve", "Va", "40th Squadron" },
            { "VARC", "Vr", "Va", "Anti-Rukh Coalition" },
            { "VAug", "Vu", "Va", "United Followers of Augurgh" },
            { "VCKd", "Vk", "Va", "Commonality of Kedzudh" },
            { "VDzF", "Vf", "Va", "Dzarrgh Federate" },
            { "VPGa", "Vg", "Va", "Pact of Gaerr" },
            { "VRuk", "Vn", "Va", "Worlds of Leader Rukh" },
            { "VSDp", "Vs", "Va", "Seaknouth Dependency" },
            { "VSEq", "Vd", "Va", "Society of Equals" },
            { "VThE", "Vt", "Va", "Thoengling Empire" },
            { "VTzE", "Vp", "Va", "Thirz Empire" },
            { "VUru", "Vu", "Va", "Nation of Urukhu" },
            { "XXXX", "Xx", null, "Unknown" },
            { "ZhCo", "Zh", "Zh", "Zhodani Consulate" }
        };

        private static readonly HashSet<string> s_defaultAllegiances = new HashSet<string> {
            // NOTE: Do not use this for autonomous/cultural regional codes (e.g. Vegan, Sylean, etc).
            // Use <Allegiance Code="Ve" Base="Im">Vegan Autonomous Region</Allegiance> in metadata instead
            "Im", // Classic Imperium
            "ImAp", // Third Imperium, Amec Protectorate (Dagu)
            "ImDa", // Third Imperium, Domain of Antares (Anta/Empt/Lish)
            "ImDc", // Third Imperium, Domain of Sylea (Core/Delp/Forn/Mass)
            "ImDd", // Third Imperium, Domain of Deneb (Dene/Reft/Spin/Troj)
            "ImDg", // Third Imperium, Domain of Gateway (Glim/Hint/Ley)
            "ImDi", // Third Imperium, Domain of Ilelish (Daib/Ilel/Reav/Verg/Zaru)
            "ImDs", // Third Imperium, Domain of Sol (Alph/Dias/Magy/Olde/Solo)
            "ImDv", // Third Imperium, Domain of Vland (Corr/Dagu/Gush/Reft/Vlan)
            "ImLa", // Third Imperium, League of Antares (Anta)
            "ImLu", // Third Imperium, Luriani Cultural Association (Ley/Forn)
            "ImSy", // Third Imperium, Sylean Worlds (Core)
            "ImVd", // Third Imperium, Vegan Autonomous District (Solo)
            "XXXX", // Unknown
            "--", // Placeholder - show as blank
        };

        public static bool IsDefaultAllegiance(string code)
        {
            return s_defaultAllegiances.Contains(code);
        }

        public static bool IsKnownT5Allegiance(string code)
        {
            return s_t5Allegiances.ContainsKey(code);
        }

        #endregion Allegiance

        #region Sophonts
        private static Dictionary<string, string> s_sophontCodes = new Dictionary<string,string> {
            { "Adda", "Addaxur" },
            { "Aqua", "Aquans" },
            { "Asla", "Aslan" },
            { "Bhun", "Brunj" },
            { "Bruh", "Bruhre" },
            { "Buru", "Burugdi" },
            { "Bwap", "Bwaps" },
            { "Chir", "Chirpers" },
            { "Darm", "Darmine" },
            { "Dolp", "Dolphins" },
            { "Droy", "Droyne" },
            { "Gray", "Graytch" },
            { "Hama", "Hamaran" },
            { "Huma", "Human" },
            { "Jala", "Jala'lak" },
            { "Jend", "Jenda" },
            { "Jonk", "Jonkeereen" },
            { "Kiak", "Kiakh'iee" },
            { "Lamu", "Lamura Gav/Teg" },
            { "Lanc", "Lancians" },
            { "Luri", "Luriani" },
            { "Mask", "Maskai" },
            { "Orca", "Orca" },
            { "Ormi", "Ormine" },
            { "Scan", "Scanians" },
            { "Sele", "Selenites" },
            { "S'mr", "S'mrii" },
            { "Stal", "Stalkers" },
            { "Sydi", "Sydites" },
            { "Syle", "Syleans" },
            { "Tapa", "Tapazmal" },
            { "Tent", "Tentrassi" },
            { "UApe", "Uplifted Apes" },
            { "Ulan", "Ulane" },
            { "Ursa", "Ursa" },
            { "Urun", "Urunishani" },
            { "Varg", "Vargr" },
            { "Vega", "Vegans" },
            { "Zhod", "Zhodani" },
            { "Ziad", "Ziadd" },
        };
        public static string SophontCodeToName(string code)
        {
            if (s_sophontCodes.ContainsKey(code))
                return s_sophontCodes[code];
            return null;
        }
        public static IEnumerable<string> SophontCodes { get { return s_sophontCodes.Keys.ToList(); } }
        #endregion // Sophonts
    }
}
