using Maps.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

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

            throw new ArgumentOutOfRangeException(nameof(c), c, "Expected value in eHex range");
        }

        public static int FromHex(char c, int? valueIfUnknown = null)
        {
            c = Char.ToUpperInvariant(c);

            if ((c == 'X' || c == '?' || c == '_') && valueIfUnknown.HasValue)
                return valueIfUnknown.Value;

            int value = HEX.IndexOf(c);
            if (value != -1)
                return value;
            switch (c)
            {
                case 'O': return 0; // Typo found in some data files
                case 'I': return 1; // Typo found in some data files
            }
            throw new ArgumentOutOfRangeException(nameof(c), c, "Expected eHex value");
        }
        #endregion // eHex

        #region Bases
        // Bases should be string containing zero or more of: CDEKMNRSTWXZ (plus nonstandard O)

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

        private static readonly RegexMap<string> s_legacyBaseDecodeTable = new GlobMap<string> {
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
            { "Sc.H", "H" },  // Hiver Supply Base
            { "*.I", "I" },  // Interface
            { "*.T", "T" },  // Terminus
        };

        public static string DecodeLegacyBases(string allegiance, string code)
        {
            allegiance = AllegianceCodeToBaseAllegianceCode(allegiance);
            string match = s_legacyBaseDecodeTable.Match(allegiance + "." + code);
            return (match != default(string)) ? match : code;
        }

        private static RegexMap<string> s_legacyBaseEncodeTable = new GlobMap<string> {
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
            // TNE
            { "Sc.H", "H" },  // Hiver Supply Base
            { "*.I", "I" },  // Interface
            { "*.T", "T" },  // Terminus
        };

        public static string EncodeLegacyBases(string allegiance, string bases)
        {
            allegiance = AllegianceCodeToBaseAllegianceCode(allegiance);
            string match = s_legacyBaseEncodeTable.Match(allegiance + "." + bases);
            return (match != default(string)) ? match : bases;
        }
        #endregion // Bases

        #region Allegiance

        private class AllegianceDictionary : EasyInitConcurrentDictionary<string, Allegiance>
        {
            public AllegianceDictionary() { }
            public AllegianceDictionary(IEnumerable<KeyValuePair<string, Allegiance>> e) : base(e) { }

            public void Add(string code, string name)
            {
                Add(code, new Allegiance(code, name));
            }

            public void Add(string code, string legacy, string baseCode, string name, string location = null)
            {
                Add(code, new Allegiance(code, legacy, baseCode, name, location));
            }
        }

        // Overrides or additions where Legacy -> T5SS code mapping is ambiguous.
        private static readonly IReadOnlyDictionary<string, string> s_legacyAllegianceToT5Overrides = new EasyInitConcurrentDictionary<string, string> {
            { "J-", "JuPr" },
            { "Jp", "JuPr" },
            { "Ju", "JuPr" },
            { "Na", "NaHu" },
            { "So", "SoCf" },
            { "Va", "NaVa" },
            { "Zh", "ZhCo" },
            { "??", "XXXX" },
            { "--", "XXXX" }
        };

        // Cases where T5SS codes don't apply: e.g. the Hierate or Imperium, or where no codes exist yet
        private static readonly AllegianceDictionary s_legacyAllegiances = new AllegianceDictionary {
            { "As", "Aslan Hierate" }, // T5SS: Clan, client state, or unknown; no generic code
            { "Dr", "Droyne" }, // T5SS: Polity name or unaligned w/ Droyne population
            { "Im", "Third Imperium" }, // T5SS: Domain or cultural region; no generic code
            { "Kk", "The Two Thousand Worlds" }, // T5SS: (Not yet assigned)
        };

        // In priority order:
        // * T5 Allegiance code (T5SS)
        // * Legacy -> T5 overrides
        // * Legacy stock codes
        // * Legacy -> T5 (T5SS)
        public static Allegiance GetStockAllegianceFromCode(string code)
        {
            if (code == null)
                return null;

            if (s_t5Allegiances.ContainsKey(code))
                return s_t5Allegiances[code];
            if (s_legacyAllegianceToT5Overrides.ContainsKey(code))
                return s_t5Allegiances[s_legacyAllegianceToT5Overrides[code]];
            if (s_legacyAllegiances.ContainsKey(code))
                return s_legacyAllegiances[code];
            if (s_legacyToT5Allegiance.ContainsKey(code))
                return s_legacyToT5Allegiance[code];

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

        // TODO: Parse this from data file
        private static readonly AllegianceDictionary s_t5Allegiances = new AllegianceDictionary {
            // T5Code, LegacyCode, BaseCode, Name
            #region T5SS Allegiances
            // Allegiance Table Begin
            { "3EoG", "Ga", null, "Third Empire of Gashikan", "Mend/Gash/Tren" },
            { "4Wor", "Fw", null, "Four Worlds", "Farf" },
            { "AkUn", "Ak", null, "Akeena Union", "Gate" },
            { "AlCo", "Al", null, "Altarean Confederation", "Vang" },
            { "AnTC", "Ac", null, "Anubian Trade Coalition", "Hint" },
            { "AsIf", "As", "As", "Iyeaao'fte", "Ustr" }, // (Tlaukhu client state)
            { "AsMw", "As", "As", "Aslan Hierate, single multiple-world clan dominates", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr/Verg" },
            { "AsOf", "As", "As", "Oleaiy'fte", "Ustr" }, // (Tlaukhu client state)
            { "AsSc", "As", "As", "Aslan Hierate, multiple clans split control", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsSF", "As", "As", "Aslan Hierate, small facility", "" }, // (temporary)
            { "AsT0", "A0", "As", "Aslan Hierate, Tlaukhu control, Yerlyaruiwo (1), Hrawoao (13), Eisohiyw (14), Ferekhearl (19)", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsT1", "A1", "As", "Aslan Hierate, Tlaukhu control, Khauleairl (2), Estoieie' (16), Toaseilwi (22)", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsT2", "A2", "As", "Aslan Hierate, Tlaukhu control, Syoisuis (3)", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsT3", "A3", "As", "Aslan Hierate, Tlaukhu control, Tralyeaeawi (4), Yulraleh (12), Aiheilar (25), Riyhalaei (28)", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsT4", "A4", "As", "Aslan Hierate, Tlaukhu control, Eakhtiyho (5), Eteawyolei' (11), Fteweyeakh (23)", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsT5", "A5", "As", "Aslan Hierate, Tlaukhu control, Hlyueawi (6), Isoitiyro (15)", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsT6", "A6", "As", "Aslan Hierate, Tlaukhu control, Uiktawa (7), Iykyasea (17), Faowaou (27)", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsT7", "A7", "As", "Aslan Hierate, Tlaukhu control, Ikhtealyo (8), Tlerfearlyo (20), Yehtahikh (24)", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsT8", "A8", "As", "Aslan Hierate, Tlaukhu control, Seieakh (9), Akatoiloh (18), We'okunir (29)", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsT9", "A9", "As", "Aslan Hierate, Tlaukhu control, Aokhalte (10), Sahao' (21), Ouokhoi (26)", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsTA", "Ta", "As", "Tealou Arlaoh", "Uist/Ustr" }, // (Aslan independent clan, non-outcast)
            { "AsTv", "As", "As", "Aslan Hierate, Tlaukhu vassal clan dominates", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsTz", "As", "As", "Aslan Hierate, Zodia clan", "Iwah" }, // (Tralyeaeawi vassal)
            { "AsVc", "As", "As", "Aslan Hierate, vassal clan dominates", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsWc", "As", "As", "Aslan Hierate, single one-world clan dominates", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "AsXX", "As", "As", "Aslan Hierate, unknown", "Akti/Dark/Eali/Hlak/Iwah/Reav/Rift/Stai/Troj/Uist/Ustr" },
            { "Bium", "Bi", null, "The Biumvirate", "Farf" },
            { "BlSo", "Bs", null, "Belgardian Sojurnate", "Troj" },
            { "CaAs", "Cb", null, "Carrillian Assembly", "Reav" },
            { "CAEM", "Es", null, "Comsentient Alliance, Eslyat Magistracy", "Beyo/Vang" },
            { "CAin", "Co", null, "Comsentient Alliance", "Beyo/Vang" }, // independent
            { "CAKT", "Kt", null, "Comsentient Alliance, Kajaani Triumverate", "Vang" },
            { "CaPr", "Ca", null, "Principality of Caledon", "Reav" },
            { "CaTe", "Ct", null, "Carter Technocracy", "Reav" },
            { "CoBa", "Ba", null, "Confederation of Bammesuka", "Mend" },
            { "CoLp", "Lp", null, "Council of Leh Perash", "Hint" },
            { "CsCa", "Ca", null, "Client state, Principality of Caledon", "Reav" },
            { "CsHv", "Hc", null, "Client state, Hive Federation", "Cruc/Spic" },
            { "CsIm", "Cs", null, "Client state, Third Imperium", "various" },
            { "CsMP", "Ms", null, "Client state, Mal'Gnar Primarchic", "Beyo" },
            { "CsTw", "KC", null, "Client state, Two Thousand Worlds", "various" },
            { "CsZh", "Cz", null, "Client state, Zhodani Consulate", "Spin/Troj" },
            { "CyUn", "Cu", null, "Cytralin Unity", "Hint" },
            { "DaCf", "Da", null, "Darrian Confederation", "Spin" },
            { "DeHg", "Dh", null, "Descarothe Hegemony", "Farf" },
            { "DeNo", "Dn", null, "Demos of Nobles", "Newo" },
            { "DiGr", "Dg", null, "Dienbach Grüpen", "Newo" },
            { "DiWb", "Dw", null, "Die Weltbund", "Beyo" },
            { "DoAl", "Az", null, "Domain of Alntzar", "Farf" },
            { "DuCf", "Cd", null, "Confederation of Duncinae", "Reav" },
            { "FCSA", "Fc", null, "Four Corners Sovereign Array", "Vang" },
            { "FeAl", "Fa", null, "Federation of Alsas", "Farf" },
            { "FeAm", "FA", null, "Federation of Amil", "Cruc" },
            { "FeHe", "Fh", null, "Federation of Heron", "Glim" },
            { "FlLe", "Fl", null, "Florian League", "Troj" },
            { "GaFd", "Ga", null, "Galian Federation", "Gate" },
            { "GaRp", "Gr", null, "Gamma Republic", "Glim" },
            { "GdKa", "Rm", null, "Grand Duchy of Kalradin", "Cruc" },
            { "GdMh", "Ma", null, "Grand Duchy of Marlheim", "Reav" },
            { "GdSt", "Gs", null, "Grand Duchy of Stoner", "Glim" },
            { "GeOr", "Go", null, "Gerontocracy of Ormine", "Dark" },
            { "GlEm", "Gl", "As", "Glorious Empire", "Troj" }, // (Aslan independent clan, outcast)
            { "GlFe", "Gf", null, "Glimmerdrift Federation", "Cruc/Glim" },
            { "GnCl", "Gi", null, "Gniivi Collective", "Hint" },
            { "GrCo", "Gr", null, "Grossdeutchland Confederation", "Vang" },
            { "HaCo", "Hc", null, "Haladon Cooperative", "Farf" },
            { "HoPA", "Ho", null, "Hochiken People's Assembly", "Gate" },
            { "HvFd", "Hv", "Hv", "Hive Federation", "Spic" },
            { "HyLe", "Hy", null, "Hyperion League", "Vang" },
            { "IHPr", "IS", null, "I'Sred*Ni Protectorate", "Beyo" },
            { "ImAp", "Im", "Im", "Third Imperium, Amec Protectorate", "Dagu" },
            { "ImDa", "Im", "Im", "Third Imperium, Domain of Antares", "Anta/Empt/Lish" },
            { "ImDc", "Im", "Im", "Third Imperium, Domain of Sylea", "Core/Delp/Forn/Mass" },
            { "ImDd", "Im", "Im", "Third Imperium, Domain of Deneb", "Dene/Reft/Spin/Troj" },
            { "ImDg", "Im", "Im", "Third Imperium, Domain of Gateway", "Glim/Hint/Ley" },
            { "ImDi", "Im", "Im", "Third Imperium, Domain of Ilelish", "Daib/Ilel/Reav/Verg/Zaru" },
            { "ImDs", "Im", "Im", "Third Imperium, Domain of Sol", "Alph/Dias/Magy/Olde/Solo" },
            { "ImDv", "Im", "Im", "Third Imperium, Domain of Vland", "Corr/Dagu/Gush/Reft/Vlan" },
            { "ImLa", "Im", "Im", "Third Imperium, League of Antares", "Anta" },
            { "ImLc", "Im", "Im", "Third Imperium, Lancian Cultural Region", "Corr/Dagu/Gush" },
            { "ImLu", "Im", "Im", "Third Imperium, Luriani Cultural Association", "Ley/Forn" },
            { "ImSy", "Im", "Im", "Third Imperium, Sylean Worlds", "Core" },
            { "ImVd", "Ve", "Im", "Third Imperium, Vegan Autonomous District", "Solo" },
            { "IsDo", "Id", null, "Islaiat Dominate", "Eali" },
            { "JAOz", "Jo", "Jp", "Julian Protectorate, Alliance of Ozuvon", "Mend" },
            { "JaPa", "Ja", null, "Jarnac Pashalic", "Beyo/Vang" },
            { "JAsi", "Ja", "Jp", "Julian Protectorate, Asimikigir Confederation", "Amdu/Mend" },
            { "JCoK", "Jc", "Jp", "Julian Protectorate, Constitution of Koekhon", "Amdu/Mend" },
            { "JHhk", "Jh", "Jp", "Julian Protectorate, Hhkar Sphere", "Amdu/Mend" },
            { "JLum", "Jd", "Jp", "Julian Protectorate, Lumda Dower", "Mend" },
            { "JMen", "Jm", "Jp", "Julian Protectorate, Commonwealth of Mendan", "Mend/Gash" },
            { "JPSt", "Jp", "Jp", "Julian Protectorate, Pirbarish Starlane", "Mend" },
            { "JRar", "Vw", "Jp", "Julian Protectorate, Rar Errall/Wolves Warren", "Mend" },
            { "JuHl", "Hl", "Jp", "Julian Protectorate, Hegemony of Lorean", "Amdu/Empt/Mend" },
            { "JUkh", "Ju", "Jp", "Julian Protectorate, Ukhanzi Coordinate", "Mend" },
            { "JuNa", "Jn", null, "Jurisdiction of Nadon", "Cano" },
            { "JuPr", "Jp", "Jp", "Julian Protectorate", "Amdu/Empt/Mend" }, // independent
            { "JuRu", "Jr", "Jp", "Julian Protectorate, Rukadukaz Republic", "Empt/Mend" },
            { "JVug", "Jv", "Jp", "Julian Protectorate, Vugurar Dominion", "Mend" },
            { "KaCo", "KC", null, "Katowice Conquest", "Cruc" },
            { "KaWo", "KW", null, "Karhyri Worlds", "Cruc" },
            { "KhLe", "Kl", null, "Khuur League", "Ley" },
            { "KkTw", "Kk", "Kk", "Two Thousand Worlds", "various" }, // (K'kree)
            { "KoEm", "Ko", null, "Korsumug Empire", "Thet" },
            { "KoPm", "Pm", null, "Percavid Marches", "Thet" },
            { "KPel", "Pe", null, "Kingdom of Peladon", "Thet" },
            { "KrBu", "Kr", null, "Kranzbund", "Vang" },
            { "LaCo", "Lc", null, "Langemarck Coalition", "Vang" },
            { "LeSu", "Ls", null, "League of Suns", "Farf" },
            { "LnRp", "Ln", null, "Loyal Nineworlds Republic", "Glim" },
            { "LyCo", "Ly", null, "Lanyard Colonies", "Reav" },
            { "MaCl", "Ma", null, "Mapepire Cluster", "Beyo" },
            { "MaEm", "Mk", null, "Maskai Empire", "Glim" },
            { "MaPr", "MF", null, "Mal'Gnar Primarchic", "Beyo" },
            { "MaUn", "Mu", null, "Malorn Union", "Cano/Alde" },
            { "MeCo", "Me", null, "Megusard Corporate", "Gate" },
            { "MiCo", "Mi", null, "Mische Conglomerate", "Cruc" },
            { "MnPr", "Mn", null, "Mnemosyne Principality", "Farf" },
            { "MrCo", "MC", null, "Mercantile Concord", "Cruc" },
            { "NaAs", "As", "As", "Non-Aligned, Aslan-dominated", "Akti/Dark/Eali/Rift/Uist/Ustr" }, // (outside Hierate)
            { "NaHu", "Na", "Na", "Non-Aligned, Human-dominated", "various" },
            { "NaVa", "Va", "Va", "Non-Aligned, Vargr-dominated", "various" },
            { "NaXX", "Na", "Na", "Non-Aligned, unclaimed", "various" },
            { "OcWs", "Ow", null, "Outcasts of the Whispering Sky", "Hint" },
            { "OlWo", "Ow", null, "Old Worlds", "Cruc" },
            { "PiFe", "Pi", null, "Pionier Fellowship", "Vang" },
            { "PlLe", "Pl", null, "Plavian League", "Gate" },
            { "Prot", "Pt", null, "The Protectorate", "Farf" },
            { "RaRa", "Ra", null, "Ral Ranta", "Hint" },
            { "Reac", "Rh", null, "The Reach", "Cruc" },
            { "ReUn", "Re", null, "Renkard Union", "Gate" },
            { "SaCo", "Sc", null, "Salinaikin Concordance", "Farf" },
            { "Sark", "Sc", null, "Sarkan Constellation", "Mend" },
            { "SeFo", "Sf", null, "Senlis Foederate", "Troj" },
            { "SlLg", "Sl", null, "Shukikikar League", "Glim" },
            { "SoBF", "So", "So", "Solomani Confederation, Bootean Federation", "Solo" },
            { "SoCf", "So", "So", "Solomani Confederation", "Alph/Diab/Dark/Hint/Magy/Olde/Reav/Solo/Spic/Ustr" },
            { "SoCT", "So", "So", "Solomani Confederation, Consolidation of Turin", "Alph" },
            { "SoFr", "Fr", "So", "Solomani Confederation, Third Reformed French Confederate Rebublic", "Alde" },
            { "SoHn", "Hn", "So", "Solomani Confederation, Hanuman Systems", "Lang" },
            { "SoKv", "Kv", "So", "Solomani Confederation, Kostov Confederate Republic", "Newo" },
            { "SoNS", "So", "So", "Solomani Confederation, New Slavic Solidarity", "Magy" },
            { "SoQu", "Qu", "So", "Solomani Confederation, Grand United States of Quesada", "Alde" },
            { "SoRD", "So", "So", "Solomani Confederation, Reformed Dootchen Estates", "Magy" },
            { "SoRz", "So", "So", "Solomani Confederation, Restricted Zone", "Alde/Newo" },
            { "SoWu", "So", "So", "Solomani Confederation, Wuan Technology Association", "Diab/Magy" },
            { "StCl", "Sc", null, "Strend Cluster", "Troj" },
            { "SwCf", "Sw", null, "Sword Worlds Confederation", "Spin" },
            { "SwFW", "Sw", null, "Swanfei Free Worlds", "Gate" },
            { "SyRe", "Sy", null, "Syzlin Republic", "Cruc" },
            { "TeCl", "Tc", null, "Tellerian Cluster", "Vang" },
            { "TrBr", "Tb", null, "Trita Brotherhood", "Cano" },
            { "TrCo", "Tr", null, "Trindel Confederacy", "Gate" },
            { "TrDo", "Td", null, "Trelyn Domain", "Vang/Farf" },
            { "TroC", "Tr", null, "Trooles Confederation", "Thet" },
            { "UnGa", "Ug", null, "Union of Garth", "Farf" },
            { "UnHa", "Uh", null, "Union of Harmony", "Dark/Reav" },
            { "V17D", "V7", "Va", "17th Disjucture", "Mesh/Wind" },
            { "V40S", "Ve", "Va", "40th Squadron", "Gvur" }, // (Ekhelle Ksafi)
            { "VAkh", "VA", "Va", "Akhstuti", "Tugl" },
            { "VAnP", "Vx", "Va", "Antares Pact", "Mesh/Mend" },
            { "VARC", "Vr", "Va", "Anti-Rukh Coalition", "Gvur" }, // (Gnoerrgh Rukh Lloell)
            { "VAsP", "Vx", "Va", "Ascendancy Pact", "Knoe" },
            { "VAug", "Vu", "Va", "United Followers of Augurgh", "Dene/Tugl" },
            { "VBkA", "Vb", "Va", "Bakne Alliance", "Tugl" },
            { "VCKd", "Vk", "Va", "Commonality of Kedzudh", "Gvur" }, // (Kedzudh Aeng)
            { "VDeG", "Vd", "Va", "Democracy of Greats", "Knoe" },
            { "VDrN", "VN", "Va", "Drr'lana Network", "Gash" },
            { "VDzF", "Vf", "Va", "Dzarrgh Federate", "Dene/Prov/Tugl" },
            { "VFFD", "V1", "Va", "First Fleet of Dzo", "Mesh" },
            { "VGoT", "Vg", "Va", "Glory of Taarskoerzn", "Prov" },
            { "ViCo", "Vi", "Va", "Viyard Concourse", "Gate" },
            { "VInL", "V9", "Va", "Infinity League", "Knoe" },
            { "VIrM", "Vh", "Va", "Irrgh Manifest", "Prov" },
            { "VJoF", "Vj", "Va", "Jihad of Faarzgaen", "Prov" },
            { "VKfu", "Vk", "Va", "Kfue", "Tugl" },
            { "VLIn", "Vi", "Va", "Llaeghskath Interacterate", "Prov/Tugl" },
            { "VLPr", "Vl", "Va", "Lair Protectorate", "Prov" },
            { "VNgC", "Vn", "Va", "Ngath Confederation", "Wind" },
            { "VNoe", "VN", "Va", "Noefa", "Tugl" },
            { "VOpA", "Vo", "Va", "Opposition Alliance", "Knoe" },
            { "VOpp", "Vo", "Va", "Opposition Alliance", "Mesh" },
            { "VOuz", "VO", "Va", "Ouzvothon", "Tugl" },
            { "VPGa", "Vg", "Va", "Pact of Gaerr", "Gvur" }, // (Gaerr Thue)
            { "VRo5", "V5", "Va", "Ruler of Five", "Mesh" },
            { "VRrS", "VW", "Va", "Rranglloez Stronghold", "Tugl" },
            { "VRuk", "Vn", "Va", "Worlds of Leader Rukh", "Gvur" }, // (Rukh Aegz)
            { "VSDp", "Vs", "Va", "Saeknouth Dependency", "Gvur" }, // (Saeknouth Igz)
            { "VSEq", "Vd", "Va", "Society of Equals", "Gvur/Tugl" }, // (Dzen Aeng Kho)
            { "VThE", "Vt", "Va", "Thoengling Empire", "Gvur/Tugl" }, // (Thoengling Raghz)
            { "VTrA", "VT", "Va", "Trae Aggregation", "Tren" },
            { "VTzE", "Vp", "Va", "Thirz Empire", "Gvur/Ziaf" }, // (Thirz Uerra)
            { "VUru", "Vu", "Va", "Urukhu", "Gvur" },
            { "VVar", "Ve", "Va", "Empire of Varroerth", "Prov/Tugl/Wind" },
            { "VVoS", "Vv", "Va", "Voekhaeb Society", "Mesh" },
            { "VWan", "Vw", "Va", "People of Wanz", "Tugl" },
            { "VWP2", "V2", "Va", "Windhorn Pact of Two", "Tugl" },
            { "VYoe", "VQ", "Va", "Union of Yoetyqq", "Gash" },
            { "WiDe", "Wd", null, "Winston Democracy", "Alde/Newo" },
            { "XXXX", "Xx", null, "Unknown", "various" },
            { "ZePr", "Zp", "Z", "Zelphic Primacy", "Farf" },
            { "ZhAx", "Ax", "Zh", "Zhodani Consulate, Addaxur Reserve", "Tien" },
            { "ZhCa", "Ca", "Zh", "Zhodani Consulate, Colonnade Province", "Vang/Farf" },
            { "ZhCh", "Zh", "Zh", "Zhodani Consulate, Chtierabl Province", "Chti" },
            { "ZhCo", "Zh", "Zh", "Zhodani Consulate", "various" }, // undetermined
            { "ZhIa", "Zh", "Zh", "Zhodani Consulate, Iabrensh Province", "Stia/Zdie" },
            { "ZhIN", "Zh", "Zh", "Zhodani Consulate, Iadr Nsobl Province", "Farf/Fore/Gvur/Spin/Yikl/Ziaf" },
            { "ZhJp", "Zh", "Zh", "Zhodani Consulate, Jadlapriants Province", "Tien/Zhda" },
            { "ZhMe", "Zh", "Zh", "Zhodani Consulate, Meqlemianz Province", "Eiap/Sidi/Eiap" },
            { "ZhOb", "Zh", "Zh", "Zhodani Consulate, Obrefripl Province", "various" },
            { "ZhSh", "Zh", "Zh", "Zhodani Consulate, Shtochiadr Province", "Itvi/Tlab" },
            { "ZhVQ", "Zh", "Zh", "Zhodani Consulate, Vlanchiets Qlom Province", "various" },
            { "Zuug", "Zu", "Zu", "Zuugabish Tripartite", "Mend" },
            { "ZyCo", "Zc", null, "Zydarian Codominium", "Beyo" },
            // Allegiance Table End
            #endregion

            // -----------------------
            // Unofficial/Unreviewed
            // -----------------------

            // M1120
            { "FdAr", "Fa", null, "Federation of Arden" },
            { "BoWo", "Bw", null, "Border Worlds" },
            { "LuIm", "Li", "Im", "Lucan's Imperium" },
            { "MaSt", "Ma", "Im", "Maragaret's Domain" },
            { "BaCl", "Bc", null, "Backman Cluster" },
            { "FdDa", "Fd", "Im", "Federation of Daibei" },
            { "FdIl", "Fi", "Im", "Federation of Ilelish" },
            { "AvCn", "Ac", null, "Avalar Consulate" },
            { "CoAl", "Ca", null, "Corsair Alliance" },
            { "StIm", "St", "Im", "Strephon's Worlds" },
            { "ZiSi", "Rv", "Im", "Restored Vilani Imperium" }, // Ziru Sirka
            { "VA16", "V6", null, "Assemblage of 1116" },
            { "CRVi", "CV", null, "Vilani Cultural Region" },
            { "CRGe", "CG", null, "Geonee Cultural Region" },
            { "CRSu", "CS", null, "Suerrat Cultural Region" },
            { "CRAk", "CA", null, "Anakudnu Cultural Region" },
        };
        public static IEnumerable<string> AllegianceCodes => s_t5Allegiances.Keys;
        // May need GroupBy to handle duplicates
        private static readonly IReadOnlyDictionary<string, Allegiance> s_legacyToT5Allegiance =
            new AllegianceDictionary(
                s_t5Allegiances.Values
                .GroupBy(a => a.LegacyCode)
                .Select(g => g.First())
                .Select(a => new KeyValuePair<string, Allegiance>(a.LegacyCode, a)));

        private static readonly ConcurrentSet<string> s_defaultAllegiances = new ConcurrentSet<string> {
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
            "??", // Placeholder - show as blank
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
        internal class Sophont
        {
            public Sophont(string code, string name, string location)
            {
                Code = code; Name = name; Location = location;
            }
            public string Code { get; set; }
            public string Name { get; set; }
            public string Location { get; set; }
        }

        private class SophontDictionary : Dictionary<string, Sophont>
        {
            public void Add(string code, string name, string location)
            {
                Add(code, new Sophont(code, name, location));
            }
        }

        private static readonly SophontDictionary s_sophontCodes = new SophontDictionary {
            #region T5SS Sophont Codes
            // Sophont Table Begin
            { "Adda", "Addaxur", "Zhodani space" },
            { "Akee", "Akeed", "Gate" },
            { "Aqua", "Aquans (Daga)/Aquamorphs (Alph)", "Daga (Aquans)/Alph (Aquamorphs)" },
            { "Asla", "Aslan", "major" },
            { "Bhun", "Brunj", "Forn" },
            { "Brin", "Brinn", "Corr" },
            { "Bruh", "Bruhre", "Daib/Reav" },
            { "Buru", "Burugdi", "Dagu/Thet" },
            { "Bwap", "Bwaps", "Imperial/Vilani space" },
            { "Chir", "Chirpers", "major" },
            { "Darm", "Darmine", "Zaru" },
            { "Dary", "Daryen", "Spin" },
            { "Dolp", "Dolphins", "Imperial/Solomani space" },
            { "Droy", "Droyne", "major" },
            { "Esly", "Eslyat", "Beyo/Vang" },
            { "Flor", "Floriani", "Beyo/Troj" },
            { "Geon", "Geonee", "Mass" },
            { "Gnii", "Gniivi", "Hint" },
            { "Gray", "Graytch", "Dagu/Gush/Ilel" },
            { "Guru", "Gurungan", "Solo" },
            { "Gurv", "Gurvin", "Hiver space" },
            { "Hama", "Hamaran", "Dagu" },
            { "Hive", "Hiver", "Hiver space" },
            { "Huma", "Human", "Imperial/Solomani space" }, // (Vilani/Solomani-mixed)
            { "Ithk", "Ithklur", "Hiver space" },
            { "Jaib", "Jaibok", "Thet" },
            { "Jala", "Jala'lak", "Dagu" },
            { "Jend", "Jenda", "Hint" },
            { "Jonk", "Jonkeereen", "Dene/Spin" },
            { "K'kr", "K'kree", "K'kree space" },
            { "Kafo", "Kafoe", "Cruc" },
            { "Kagg", "Kaggushus", "Mass" },
            { "Karh", "Karhyri", "Cruc" },
            { "Kiak", "Kiakh'iee", "Dagu" },
            { "Lamu", "Lamura Gav/Teg", "Hint" },
            { "Lanc", "Lancians", "Dagu/Gush" },
            { "Libe", "Liberts", "Daib/Dias" },
            { "Llel", "Llellewyloly", "Spin" }, // (Dandies)
            { "Luri", "Luriani", "Forn/Ley" },
            { "Mal'", "Mal'Gnar", "Beyo" },
            { "Mask", "Maskai", "Glim" },
            { "Mitz", "Mitzene", "Thet" },
            { "Muri", "Murians", "Vang" },
            { "Orca", "Orca", "Imperial/Solomani space" },
            { "Ormi", "Ormine", "Dark" },
            { "S'mr", "S'mrii", "Dagu" },
            { "Scan", "Scanians", "Dagu" },
            { "Sele", "Selenites", "Alph" },
            { "Sred", "Sred*Ni", "Beyo" },
            { "Stal", "Stalkers", "Hint" },
            { "Suer", "Suerrat", "Ilel" },
            { "Sull", "Sulliji", "Dene" },
            { "Swan", "Swanfei", "Gate" },
            { "Sydi", "Sydites", "Ley" },
            { "Syle", "Syleans", "Core" },
            { "Tapa", "Tapazmal", "Reft" },
            { "Taur", "Taureans", "Alde" },
            { "Tent", "Tentrassi", "Zaru" },
            { "Tlye", "Tlyetrai", "Reav" },
            { "UApe", "Uplifted Apes", "Imperial/Solomani space" },
            { "Ulan", "Ulane", "Dark" },
            { "Ursa", "Ursa", "Forn/Ley" },
            { "Urun", "Urunishani", "Anta" },
            { "Varg", "Vargr", "Anta/Corr/Dagu/Dene/Empt/Ley/Lish/Spin/Vargr space" },
            { "Vega", "Vegans", "Solo" },
            { "Za't", "Za'tachk", "Wren" },
            { "Zhod", "Zhodani", "Zhodani space" },
            { "Ziad", "Ziadd", "Dagu" },
            // Sophont Table End
            #endregion
        };
        public static string SophontCodeToName(string code)
        {
            if (s_sophontCodes.ContainsKey(code))
                return s_sophontCodes[code].Name;
            return null;
        }
        public static Sophont SophontForCode(string code)
        {
            if (s_sophontCodes.ContainsKey(code))
                return s_sophontCodes[code];
            return null;
        }
        public static IEnumerable<string> SophontCodes => s_sophontCodes.Keys;
        #endregion // Sophonts

        #region World Details
        public static int Importance(World world)
        {
            if (world.ImportanceValue.HasValue)
                return world.ImportanceValue.Value;

            int i = 0;
            if (world.Starport == 'A' || world.Starport == 'B')
                i += 1;
            if (world.Starport >= 'D')
                i -= 1;
            if (world.TechLevel >= 16)
                i += 1;
            if (world.TechLevel >= 10)
                i += 1;
            if (world.TechLevel <= 8)
                i -= 1;
            if (world.IsAg)
                i += 1;
            if (world.IsHi)
                i += 1;
            if (world.IsIn)
                i += 1;
            if (world.IsRi)
                i += 1;
            if (world.PopulationExponent <= 6)
                i -= 1;
            if ((world.Bases.Contains('N') || world.Bases.Contains('K')) &&
                (world.Bases.Contains('S') || world.Bases.Contains('V')))
                i += 1;
            if (world.Bases.Contains('W'))
                i += 1;
            return i;
        }
        #endregion
    }
}
