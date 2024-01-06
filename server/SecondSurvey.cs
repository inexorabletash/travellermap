﻿#nullable enable
using Maps.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Hosting;

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
            return c switch
            {
                'O' => 0,// Typo found in some data files
                'I' => 1,// Typo found in some data files
                _ => throw new ParseException($"Invalid eHex digit: '{c}'"),
            };
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

        private static ThreadLocal<RegexMap<string>> s_legacyBaseDecodeTable = new ThreadLocal<RegexMap<string>>(() =>
            new GlobMap<string> {
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
        });

        public static string DecodeLegacyBases(string allegiance, string code)
        {
            allegiance = AllegianceCodeToBaseAllegianceCode(allegiance);
            string match = s_legacyBaseDecodeTable.Value.Match(allegiance + "." + code);
            return (match != default) ? match : code;
        }

        private static ThreadLocal<RegexMap<string>> s_legacyBaseEncodeTable = new ThreadLocal<RegexMap<string>>(() =>
            new GlobMap<string> {
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
        });

        public static string EncodeLegacyBases(string allegiance, string bases)
        {
            allegiance = AllegianceCodeToBaseAllegianceCode(allegiance);
            string match = s_legacyBaseEncodeTable.Value.Match(allegiance + "." + bases);
            return (match != default) ? match : bases;
        }
        #endregion // Bases

        #region Allegiance

        private class AllegianceDictionary : Dictionary<string, Allegiance>
        {
            public AllegianceDictionary() { }
            public AllegianceDictionary(IEnumerable<KeyValuePair<string, Allegiance>> collection) {
                foreach (var pair in collection)
                    this.Add(pair);
            }

            public void Add(KeyValuePair<string, Allegiance> pair)
            {
                Add(pair.Key, pair.Value);
            }

            public void Add(string code, string name)
            {
                Add(code, new Allegiance(code, name));
            }

            public void Add(string code, string legacy, string? baseCode, string name, string? location = null)
            {
                Add(code, new Allegiance(code, name, legacy, baseCode, location));
            }

            public AllegianceDictionary Merge(AllegianceDictionary other)
            {
                foreach (var pair in other)
                    this.TryAdd(pair.Key, pair.Value);
                return this;
            }

            public static AllegianceDictionary FromFile(string path)
            {
                using var reader = Util.SharedFileReader(path);
                return Parse(reader);
            }

            private static AllegianceDictionary Parse(StreamReader reader)
            {
                static string? nullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
                var dict = new AllegianceDictionary();
                var parser = new Serialization.TSVParser(reader);
                foreach (var row in parser.Data)
                {
                    dict.Add(
                        row.dict["Code"], row.dict["Legacy"], nullIfEmpty(row.dict["BaseCode"]),
                        row.dict["Name"], nullIfEmpty(row.dict["Location"]));
                }
                return dict;
            }
        }

        // Overrides or additions where Legacy -> T5SS code mapping is ambiguous.
        private static ThreadLocal<IReadOnlyDictionary<string, string>> s_legacyAllegianceToT5Overrides = new ThreadLocal<IReadOnlyDictionary<string, string>>(() =>
            new Dictionary<string, string> {
            { "J-", "JuPr" },
            { "Jp", "JuPr" },
            { "Ju", "JuPr" },
            { "Na", "NaHu" },
            { "So", "SoCf" },
            { "Va", "NaVa" },
            { "Zh", "ZhCo" },
            { "??", "XXXX" },
            { "--", "XXXX" }
        });

        // Cases where T5SS codes don't apply: e.g. the Hierate or Imperium, or where no codes exist yet
        private static ThreadLocal<AllegianceDictionary> s_legacyAllegiances = new ThreadLocal<AllegianceDictionary>(() =>
            new AllegianceDictionary {
            { "As", "Aslan Hierate" }, // T5SS: Clan, client state, or unknown; no generic code
            { "Dr", "Droyne" }, // T5SS: Polity name or unaligned w/ Droyne population
            { "Im", "Third Imperium" }, // T5SS: Domain or cultural region; no generic code
            { "Kk", "The Two Thousand Worlds" }, // T5SS: (Not yet assigned)
        });

        // In priority order:
        // * T5 Allegiance code (T5SS)
        // * Legacy -> T5 overrides
        // * Legacy stock codes
        // * Legacy -> T5 (T5SS)
        public static Allegiance? GetStockAllegianceFromCode(string code)
        {
            if (code == null)
                return null;

            if (s_t5Allegiances.Value.ContainsKey(code))
                return s_t5Allegiances.Value[code];
            if (s_legacyAllegianceToT5Overrides.Value.ContainsKey(code))
                return s_t5Allegiances.Value[s_legacyAllegianceToT5Overrides.Value[code]];
            if (s_legacyAllegiances.Value.ContainsKey(code))
                return s_legacyAllegiances.Value[code];
            if (s_legacyToT5Allegiance.Value.ContainsKey(code))
                return s_legacyToT5Allegiance.Value[code];

            return null;
        }

        // TODO: This discounts the Sector's allegiance/base definitions, if any.
        public static string AllegianceCodeToBaseAllegianceCode(string code)
        {
            Allegiance? alleg = GetStockAllegianceFromCode(code);
            if (alleg == null)
                return code;
            if (string.IsNullOrEmpty(alleg.Base))
                return code;
            return alleg.Base!;
        }

        public static string T5AllegianceCodeToLegacyCode(string t5code)
        {
            if (!s_t5Allegiances.Value.ContainsKey(t5code))
                return t5code;
            return s_t5Allegiances.Value[t5code].LegacyCode;
        }

        private static ThreadLocal<AllegianceDictionary> s_t5Allegiances = new ThreadLocal<AllegianceDictionary>(() =>             
            AllegianceDictionary
            .FromFile(HostingEnvironment.MapPath("~/res/t5ss/allegiance_codes.tab"))
            .Merge(new AllegianceDictionary {
            // T5Code, LegacyCode, BaseCode, Name

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
        }));

        public static IEnumerable<string> AllegianceCodes => s_t5Allegiances.Value.Keys;
        // May need GroupBy to handle duplicates
        private static ThreadLocal<IReadOnlyDictionary<string, Allegiance>> s_legacyToT5Allegiance = new ThreadLocal<IReadOnlyDictionary<string, Allegiance>>(() =>
            new AllegianceDictionary(
                s_t5Allegiances.Value.Values
                .Where(a => a.LegacyCode != null)
                .GroupBy(a => a.LegacyCode)
                .Select(g => g.First())
                .Select(a => new KeyValuePair<string, Allegiance>(a.LegacyCode!, a))));

        private static ThreadLocal<HashSet<string>> s_defaultAllegiances = new ThreadLocal<HashSet<string>>(() =>
            new HashSet<string> {
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
        });

        public static bool IsDefaultAllegiance(string code) => s_defaultAllegiances.Value.Contains(code);
        public static bool IsKnownT5Allegiance(string code) => s_t5Allegiances.Value.ContainsKey(code);

        #endregion Allegiance

        #region Sophonts
        internal class Sophont
        {
            public Sophont(string code, string name, string location)
            {
                Code = code; Name = name; Location = location;
            }
            public string Code { get; }
            public string Name { get; }
            public string Location { get; }
        }

        private class SophontDictionary : Dictionary<string, Sophont>
        {
            public void Add(string code, string name, string location)
            {
                Add(code, new Sophont(code, name, location));
            }
            public static SophontDictionary FromFile(string path)
            {
                using var reader = Util.SharedFileReader(path);
                return Parse(reader);
            }

            private static SophontDictionary Parse(StreamReader reader)
            {
                var dict = new SophontDictionary();
                var parser = new Serialization.TSVParser(reader);
                foreach (var row in parser.Data)
                    dict.Add(row.dict["Code"], row.dict["Name"], row.dict["Location"]);
                return dict;
            }
        }

        private static ThreadLocal<SophontDictionary> s_sophontCodes = new ThreadLocal<SophontDictionary>(() =>
            SophontDictionary
            .FromFile(System.Web.Hosting.HostingEnvironment.MapPath("~/res/t5ss/sophont_codes.tab")));

        public static string? SophontCodeToName(string code)
        {
            if (s_sophontCodes.Value.ContainsKey(code))
                return s_sophontCodes.Value[code].Name;
            return null;
        }
        public static Sophont? SophontForCode(string code)
        {
            if (s_sophontCodes.Value.ContainsKey(code))
                return s_sophontCodes.Value[code];
            return null;
        }
        public static IEnumerable<string> SophontCodes => s_sophontCodes.Value.Keys;
        #endregion // Sophonts

    }
}
