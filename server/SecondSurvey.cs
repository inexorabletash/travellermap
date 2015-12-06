using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;

namespace Maps
{
    internal static class SecondSurvey
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

            throw new ArgumentOutOfRangeException("c", string.Format(CultureInfo.InvariantCulture, "Value out of range: '{0}'", c));
        }

        public static int FromHex(char c, int? valueIfX = null)
        {
            c = Char.ToUpperInvariant(c);

            if (c == 'X' && valueIfX.HasValue)
                return valueIfX.Value;

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
            throw new ArgumentOutOfRangeException("c", string.Format(CultureInfo.InvariantCulture, "Character out of range: '{0}'", c));
        }
        #endregion // eHex

        #region Bases
        // Bases should be string containing zero or more of: CDKMNRSTWXZ (plus nonstandard E, O)

        // Code  Owner      Description
        // ----  ---------  ------------------------
        // C     Vargr      Corsair base
        // D     Any        Depot
        // E     Hiver      Embassy
        // K     Any        Naval base
        // M     Any        Military base
        // N     Imperial   Naval base
        // O     K'kree     Naval Outpost -------- NONSTANDARD
        // R     Aslan      Clan base
        // S     Imperial   Scout base
        // T     Aslan      Tlaukhu base
        // V     Any        Exploration base
        // W     Any        Way station

        private static RegexDictionary<string> s_legacyBaseDecodeTable = new GlobDictionary<string> {
            { "*.2", "NS" },  // Imperial Naval base + Scout base
            { "*.A", "NS" },  // Imperial Naval base + Scout base
            { "*.B", "NW" },  // Imperial Naval base + Scout Way station
            { "*.C", "C" },   // Vargr Corsair base
            { "*.D", "D" },   // Depot
            { "*.E", "E"},    // Hiver Embassy
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
            { "*.W", "W" },   // Way Station
            { "*.X", "W" },   // Zhodani Relay Station
            { "*.Y", "D" },   // Zhodani Depot
            { "*.Z", "KM" },  // Zhodani Naval/Military Base
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
            { "*.E", "E"},   // Hiver Embassy
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
            { "Zh.W", "X" },  // Zhodani Relay Station
            { "*.W", "W" },   // Imperial Scout Way Station
            { "Zh.KM", "Z" }, // Zhodani Naval/Military Base
        };

        public static string EncodeLegacyBases(string allegiance, string bases)
        {
            allegiance = AllegianceCodeToBaseAllegianceCode(allegiance);
            string match = s_legacyBaseEncodeTable.Match(allegiance + "." + bases);
            return (match != default(string)) ? match : bases;
        }
        #endregion // Bases

        #region Allegiance

        private class LegacyAllegiances : Dictionary<string, Allegiance>
        {
            public void Add(string code, string name)
            {
                Add(code, new Allegiance(code, name));
            }
        }
        private static LegacyAllegiances s_stockAllegiances = new LegacyAllegiances {
            { "As", "Aslan Hierate" },
            { "Cs", "Imperial Client State" },
            { "Dr", "Droyne" },
            { "Hv", "Hiver Federation" },
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
            if (string.IsNullOrEmpty(alleg.Base))
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
            { "Hv", "HvFd" },
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
                Add(t5code, new Allegiance(t5code, code, baseCode, name));
            }
        }
        private static T5Allegiances s_t5Allegiances = new T5Allegiances {
            // T5Code, LegacyCode, BaseCode, Name
            // Allegiance Table Begin
            { "3EoG", "Ga", "Ga", "Third Empire of Gashikan" },
            { "4Wor", "Fw", "FW", "Four Worlds" },
            { "AkUn", "Ak", null, "Akeena Union" },
            { "AlCo", "Al", null, "Altarean Confederation" },
            { "AnTC", "Ac", null, "Anubian Trade Coalition" },
            { "AsIf", "As", "As", "Iyeaao'fte" }, // (Tlaukhu client state)
            { "AsMw", "As", "As", "Aslan Hierate, single multiple-world clan dominates" },
            { "AsOf", "As", "As", "Oleaiy'fte" }, // (Tlaukhu client state)
            { "AsSF", "As", "As", "Aslan Hierate, small facility" }, // (temporary)
            { "AsSc", "As", "As", "Aslan Hierate, multiple clans split control" },
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
            { "AsTA", "Ta", "As", "Tealou Arlaoh" }, // (Aslan independent clan, non-outcast)
            { "AsTv", "As", "As", "Aslan Hierate, Tlaukhu vassal clan dominates" },
            { "AsTz", "As", "As", "Aslan Hierate, Zodia clan" }, // (Tralyeaeawi vassal)
            { "AsVc", "As", "As", "Aslan Hierate, vassal clan dominates" },
            { "AsWc", "As", "As", "Aslan Hierate, single one-world clan dominates" },
            { "AsXX", "As", "As", "Aslan Hierate, unknown" },
            { "Bium", "Bi", "B", "The Biumvirate" },
            { "BlSo", "Bs", null, "Belgardian Sojurnate" },
            { "CAEM", "Es", null, "Comsentient Alliance, Eslyat Magistracy" },
            { "CAKT", "Kt", null, "Comsentient Alliance, Kajaani Triumverate" },
            { "CAin", "Co", null, "Comsentient Alliance" }, // independent
            { "CaAs", "Cb", null, "Carrillian Assembly" },
            { "CaPr", "Ca", null, "Principality of Caledon" },
            { "CaTe", "Ct", null, "Carter Technocracy" },
            { "CoBa", "Ba", "Ba", "Confederation of Bammesuka" },
            { "CoLp", "Lp", null, "Council of Leh Perash" },
            { "CsCa", "Ca", null, "Client state, Principality of Caledon" },
            { "CsHv", "Hc", null, "Client state, Hiver Federation" },
            { "CsIm", "Cs", null, "Client state, Third Imperium" },
            { "CsMP", "Ms", null, "Client state, Ma'Gnar Primarchic" },
            { "CsZh", "Cz", null, "Client state, Zhodani Consulate" },
            { "CyUn", "Cu", null, "Cytralin Unity" },
            { "DaCf", "Da", null, "Darrian Confederation" },
            { "DeHg", "Dh", "D", "Descarothe Hegemony" },
            { "DiWb", "Dw", null, "Die Weltbund" },
            { "DoAl", "Az", "A", "Domain of Alntzar" },
            { "DuCf", "Cd", null, "Confederation of Duncinae" },
            { "FCSA", "Fc", null, "Four Corners Sovereign Array" },
            { "FeAl", "Fa", "F", "Federation of Alsas" },
            { "FeAm", "FA", null, "Federation of Amil" },
            { "FeHe", "Fh", null, "Federation of Heron" },
            { "FlLe", "Fl", null, "Florian League" },
            { "GaFd", "Ga", null, "Galian Federation" },
            { "GaRp", "Gr", null, "Gamma Republic" },
            { "GdKa", "Rm", null, "Grand Duchy of Kalradin" },
            { "GdMh", "Ma", null, "Grand Duchy of Marlheim" },
            { "GdSt", "Gs", null, "Grand Duchy of Stoner" },
            { "GeOr", "Go", null, "Gerontocracy of Ormine" },
            { "GlEm", "Gl", "As", "Glorious Empire" }, // (Aslan independent clan, outcast)
            { "GlFe", "Gf", null, "Glimmerdrift Federation" },
            { "GnCl", "Gi", null, "Gniivi Collective" },
            { "GrCo", "Gr", null, "Grossdeutchland Confederation" },
            { "HaCo", "Hc", "H", "Haladon Cooperative" },
            { "HoPA", "Ho", null, "Hochiken People's Assembly" },
            { "HvFd", "Hv", "Hv", "Hiver Federation" },
            { "HyLe", "Hy", null, "Hyperion League" },
            { "IHPr", "IS", null, "I'Sred*Ni Protectorate" },
            { "ImAp", "Im", "Im", "Third Imperium, Amec Protectorate" },
            { "ImDa", "Im", "Im", "Third Imperium, Domain of Antares" },
            { "ImDc", "Im", "Im", "Third Imperium, Domain of Sylea" },
            { "ImDd", "Im", "Im", "Third Imperium, Domain of Deneb" },
            { "ImDg", "Im", "Im", "Third Imperium, Domain of Gateway" },
            { "ImDi", "Im", "Im", "Third Imperium, Domain of Ilelish" },
            { "ImDs", "Im", "Im", "Third Imperium, Domain of Sol" },
            { "ImDv", "Im", "Im", "Third Imperium, Domain of Vland" },
            { "ImLa", "Im", "Im", "Third Imperium, League of Antares" },
            { "ImLc", "Im", "Im", "Third Imperium, Lancian Cultural Region" },
            { "ImLu", "Im", "Im", "Third Imperium, Luriani Cultural Association" },
            { "ImSy", "Im", "Im", "Third Imperium, Sylean Worlds" },
            { "ImVd", "Ve", "Im", "Third Imperium, Vegan Autonomous District" },
            { "IsDo", "Id", null, "Islaiat Dominate" },
            { "JAOz", "Jo", "Jp", "Julian Protectorate, Alliance of Ozuvon" },
            { "JAsi", "Ja", "Jp", "Julian Protectorate, Asimikigir Confederation" },
            { "JCoK", "Jc", "Jp", "Julian Protectorate, Constitution of Koekhon" },
            { "JHhk", "Jh", "Jp", "Julian Protectorate, Hhkar Sphere" },
            { "JLum", "Jd", "Jp", "Julian Protectorate, Lumda Dower" },
            { "JMen", "Jm", "Jp", "Julian Protectorate, Commonwealth of Mendan" },
            { "JPSt", "Jp", "Jp", "Julian Protectorate, Pirbarish Starlane" },
            { "JRar", "Vw", "Jp", "Julian Protectorate, Rar Errall/Wolves Warren" },
            { "JUkh", "Ju", "Jp", "Julian Protectorate, Ukhanzi Coordinate" },
            { "JVug", "Jv", "Jp", "Julian Protectorate, Vugurar Dominion" },
            { "JaPa", "Ja", null, "Jarnac Pashalic" },
            { "JuHl", "Hl", "Jp", "Julian Protectorate, Hegemony of Lorean" },
            { "JuPr", "Jp", "Jp", "Julian Protectorate" }, // independent
            { "JuRu", "Jr", "Jp", "Julian Protectorate, Rukadukaz Republic" },
            { "KPel", "Pe", "Pe", "Kingdom of Peladon" },
            { "KaCo", "KC", null, "Katowice Conquest" },
            { "KaWo", "KW", null, "Karhyri Worlds" },
            { "KhLe", "Kl", null, "Khuur League" },
            { "KoEm", "Ko", "Ko", "Korsumug Empire" },
            { "KoPm", "Pm", "Pm", "Percavid Marches" },
            { "KrBu", "Kr", null, "Kranzbund" },
            { "LaCo", "Lc", null, "Langemarck Coalition" },
            { "LeSu", "Ls", "L", "League of Suns" },
            { "LnRp", "Ln", null, "Loyal Nineworlds Republic" },
            { "LyCo", "Ly", null, "Lanyard Colonies" },
            { "MaCl", "Ma", null, "Mapepire Cluster" },
            { "MaEm", "Mk", null, "Maskai Empire" },
            { "MaPr", "MF", null, "Ma'Gnar Primarchic" },
            { "MeCo", "Me", null, "Megusard Corporate" },
            { "MiCo", "Mi", null, "Mische Conglomerate" },
            { "MnPr", "Mn", "N", "Mnemosyne Principality" },
            { "MrCo", "MC", null, "Mercantile Concord" },
            { "NaAs", "As", "As", "Non-Aligned, Aslan-dominated" }, // (outside Hierate)
            { "NaHu", "Na", "Na", "Non-Aligned, Human-dominated" },
            { "NaVa", "Va", "Va", "Non-Aligned, Vargr-dominated" },
            { "NaXX", "Na", "Na", "Non-Aligned, unclaimed" },
            { "OcWs", "Ow", null, "Outcasts of the Whispering Sky" },
            { "OlWo", "Ow", null, "Old Worlds" },
            { "PiFe", "Pi", null, "Pionier Fellowship" },
            { "PlLe", "Pl", null, "Plavian League" },
            { "Prot", "Pt", "P", "The Protectorate" },
            { "RaRa", "Ra", null, "Ral Ranta" },
            { "ReUn", "Re", null, "Renkard Union" },
            { "Reac", "Rh", null, "The Reach" },
            { "SaCo", "Sc", "S", "Salinaikin Concordance" },
            { "Sark", "Sc", "Sc", "Sarkan Constellation" },
            { "SeFo", "Sf", null, "Senlis Foederate" },
            { "SlLg", "Sl", null, "Shukikikar League" },
            { "SoCf", "So", "So", "Solomani Confederation" },
            { "SoNS", "So", "So", "Solomani Confederation, New Slavic Solidarity" },
            { "SoRD", "So", "So", "Solomani Confederation, Reformed Dootchen Estates" },
            { "SoWu", "So", "So", "Solomani Confederation, Wuan Technology Association" },
            { "StCl", "Sc", null, "Strend Cluster" },
            { "SwCf", "Sw", null, "Sword Worlds Confederation" },
            { "SwFW", "Sw", null, "Swanfei Free Worlds" },
            { "SyRe", "Sy", null, "Syzlin Republic" },
            { "TeCl", "Tc", null, "Tellerian Cluster" },
            { "TrCo", "Tr", null, "Trindel Confederacy" },
            { "TrDo", "Td", null, "Trelyn Domain" },
            { "TroC", "Tr", "Tr", "Trooles Confederation" },
            { "UnGa", "Ug", "U", "Union of Garth" },
            { "UnHa", "Uh", null, "Union of Harmony" },
            { "V17D", "V7", null, "17th Disjucture" },
            { "V40S", "Ve", null, "40th Squadron" }, // (Ekhelle Ksafi)
            { "VARC", "Vr", null, "Anti-Rukh Coalition" }, // (Gnoerrgh Rukh Lloell)
            { "VAnP", "Vx", null, "Antares Pact" },
            { "VAug", "Vu", null, "United Followers of Augurgh" },
            { "VBkA", "Vb", null, "Bakne Alliance" },
            { "VCKd", "Vk", null, "Commonality of Kedzudh" }, // (Kedzudh Aeng)
            { "VDzF", "Vf", null, "Dzarrgh Federate" },
            { "VFFD", "V1", null, "First Fleet of Dzo" },
            { "VGoT", "Vg", null, "Glory of Taarskoerzn" },
            { "VIrM", "Vh", null, "Irrgh Manifest" },
            { "VJoF", "Vj", null, "Jihad of Faarzgaen" },
            { "VLIn", "Vi", null, "Llaeghskath Interacterate" },
            { "VLPr", "Vl", null, "Lair Protectorate" },
            { "VNgC", "Vn", null, "Ngath Confederation" },
            { "VOpp", "Vo", null, "Opposition Alliance" },
            { "VPGa", "Vg", null, "Pact of Gaerr" }, // (Gaerr Thue)
            { "VRo5", "V5", null, "Ruler of Five" },
            { "VRrS", "VW", null, "Rranglloez Stronghold" },
            { "VRuk", "Vn", null, "Worlds of Leader Rukh" }, // (Rukh Aegz)
            { "VSDp", "Vs", null, "Saeknouth Dependency" }, // (Saeknouth Igz)
            { "VSEq", "Vd", null, "Society of Equals" }, // (Dzen Aeng Kho)
            { "VThE", "Vt", null, "Thoengling Empire" }, // (Thoengling Raghz)
            { "VTzE", "Vp", null, "Thirz Empire" }, // (Thirz Uerra)
            { "VUru", "Vu", null, "Urukhu" },
            { "VVar", "Ve", null, "Empire of Varroerth" },
            { "VVoS", "Vv", null, "Voekhaeb Society" },
            { "VWP2", "V2", null, "Windhorn Pact of Two" },
            { "VWan", "Vw", null, "People of Wanz" },
            { "ViCo", "Vi", null, "Viyard Concourse" },
            { "XXXX", "Xx", null, "Unknown" },
            { "ZePr", "Zp", "Z", "Zelphic Primacy" },
            { "ZhCa", "Ca", "Zh", "Zhodani Consulate, Colonnade Province" },
            { "ZhCh", "Zh", "Zh", "Zhodani Consulate, Chtierabl Province" },
            { "ZhCo", "Zh", "Zh", "Zhodani Consulate" }, // undetermined
            { "ZhIN", "Zh", "Zh", "Zhodani Consulate, Iadr Nsobl Province" },
            { "ZhIa", "Zh", "Zh", "Zhodani Consulate, Iabrensh Province" },
            { "ZhJp", "Zh", "Zh", "Zhodani Consulate, Jadlapriants Province" },
            { "ZhMe", "Zh", "Zh", "Zhodani Consulate, Meqlemianz Province" },
            { "ZhOb", "Zh", "Zh", "Zhodani Consulate, Obrefripl Province" },
            { "ZhSh", "Zh", "Zh", "Zhodani Consulate, Shtochiadr Province" },
            { "ZhVQ", "Zh", "Zh", "Zhodani Consulate, Vlanchiets Qlom Province" },
            { "Zuug", "Zu", "Zu", "Zuugabish Tripartite" },
            { "ZyCo", "Zc", null, "Zydarian Codominion" },
            // Allegiance Table End
        };
        public static IEnumerable<string> AllegianceCodes { get { return s_t5Allegiances.Keys; } }

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
            "ImLc", // Third Imperium, Lancian Cultural Region (Corr/Dagu/Gush)
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
            // Sophont Table Begin
            { "Adda", "Addaxur" },
            { "Akee", "Akeed" },
            { "Aqua", "Aquans (Daga)/Aquamorphs (Alph)" },
            { "Asla", "Aslan" },
            { "Bhun", "Brunj" },
            { "Bruh", "Bruhre" },
            { "Buru", "Burugdi" },
            { "Bwap", "Bwaps" },
            { "Chir", "Chirpers" },
            { "Darm", "Darmine" },
            { "Dary", "Daryen" },
            { "Dolp", "Dolphins" },
            { "Droy", "Droyne" },
            { "Esly", "Eslyat" },
            { "Flor", "Floriani" },
            { "Geon", "Geonee" },
            { "Gnii", "Gniivi" },
            { "Gray", "Graytch" },
            { "Guru", "Gurungan" },
            { "Hama", "Hamaran" },
            { "Hive", "Hiver" },
            { "Huma", "Human" }, // (Vilani/Solomani-mixed)
            { "Jaib", "Jaibok" },
            { "Jala", "Jala'lak" },
            { "Jend", "Jenda" },
            { "Jonk", "Jonkeereen" },
            { "Kafo", "Kafoe" },
            { "Kagg", "Kaggushus" },
            { "Karh", "Karhyri" },
            { "Kiak", "Kiakh'iee" },
            { "Lamu", "Lamura Gav/Teg" },
            { "Lanc", "Lancians" },
            { "Libe", "Liberts" },
            { "Luri", "Luriani" },
            { "Mal'", "Mal'Gnar" },
            { "Mask", "Maskai" },
            { "Mitz", "Mitzene" },
            { "Muri", "Murians" },
            { "Orca", "Orca" },
            { "Ormi", "Ormine" },
            { "S'mr", "S'mrii" },
            { "Scan", "Scanians" },
            { "Sele", "Selenites" },
            { "Sred", "Sred*Ni" },
            { "Stal", "Stalkers" },
            { "Suer", "Suerrat" },
            { "Sull", "Sulliji" },
            { "Swan", "Swanfei" },
            { "Sydi", "Sydites" },
            { "Syle", "Syleans" },
            { "Tapa", "Tapazmal" },
            { "Tent", "Tentrassi" },
            { "Tlye", "Tlyetrai" },
            { "UApe", "Uplifted Apes" },
            { "Ulan", "Ulane" },
            { "Ursa", "Ursa" },
            { "Urun", "Urunishani" },
            { "Varg", "Vargr" },
            { "Vega", "Vegans" },
            { "Zhod", "Zhodani" },
            { "Ziad", "Ziadd" },
            // Sophont Table End
        };
        public static string SophontCodeToName(string code)
        {
            if (s_sophontCodes.ContainsKey(code))
                return s_sophontCodes[code];
            return null;
        }
        public static IEnumerable<string> SophontCodes { get { return s_sophontCodes.Keys; } }
        #endregion // Sophonts
    }
}
