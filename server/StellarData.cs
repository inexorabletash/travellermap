//#define EXTENDED_SYSTEM_PARSING
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace Maps
{
    // TODO: Expand to handle non-T5SS data
    internal static class StellarData
    {
        internal struct Star
        {
            public string classification; // Full string for star, e.g. "G2 V", "D", "BD", "BH", etc

            public char type; // OBAFGKM 
            public int fraction; // 0-9
            public string? luminosity; // Ia, Ib, II, III, IV, V, VI, VII
        }

        private static readonly Regex STELLAR_REGEX = new Regex(@"([OBAFGKM][0-9] ?(?:Ia|Ib|II|III|IV|V|VI|VII|D)|D|NS|PSR|BH|BD)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static Regex STAR_REGEX = new Regex(@"^([OBAFGKM])([0-9]) ?(Ia|Ib|II|III|IV|V|VI)$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static IEnumerable<Star> Parse(string stellar)
        {
            foreach (Match m in STELLAR_REGEX.Matches(stellar))
            {
                if (m.Value == "D" || m.Value == "NS" || m.Value == "PSR" || m.Value == "BH" || m.Value == "BD" )
                {
                    yield return new Star() { classification = m.Value };
                }
                else
                {
                    Match sm = STAR_REGEX.Match(m.Value);
                    if (sm.Success)
                    {
                        yield return new Star()
                        {
                            classification = m.Value,
                            type = sm.Groups[1].Value[0],
                            fraction = sm.Groups[2].Value[0] - '0',
                            luminosity = sm?.Groups[3].Value
                        };
                    }
                    else
                    {
                        // Assume anything else is a white dwarf. 
                        yield return new Star() { classification = "D" };
                    }
                }
            }
        }
    }

    // TODO: Remove this
    internal static class StellarDataParser
    {
        // Grammar:
        //
        // Basic: 
        //   system     ::= star ( w+ star )*
        // Realized as:
        //   system     ::= unit ( w+ companion )*
        //   companion  ::= near
        //   near       ::= unit
        //   unit       ::= star

        // Extended: (Malenfant's Revised Stellar Generation Rules)
        //
        //   system     ::= unit ( w+ companion )*
        //   companion  ::= near | far
        //   near       ::= unit
        //   far        ::= "[" system "]"
        //   unit       ::= star | pair
        //   pair       ::= "(" star w+ star ")"

        // Common:
        //   star       ::= type ( tenths w* size | w* "D" ) main?
        //                | whitedwarf
        //                | neutronstar
        //                | pulsar
        //                | blackhole
        //                | browndwarf
        //                | unknown
        //   type       ::= "O" | "B" | "A" | "F" | "G" | "K" | "M"
        //   tenths     ::= "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9"
        //   size       ::= "D" | "Ia" | "Ib" | "II" | "III" | "IV" | "V" | "VI" | "VII"
        //   whitedwarf ::= "DB" | "DA" | "DF" | "DG" | "DK" | "DM" | "D"
        //   neutronstar::= "NS"
        //   pulsar     ::= "PSR"
        //   blackhole  ::= "BH"
        //   browndwarf ::= "BD"
        //   unknown    ::= "Un"
        //
        //   main       ::= "*"
        //
        //   w          ::= " "
        //
        // Notes:
        //   * "Un" is from Mendan 0221 (verified in Challenge #46) [Replaced in T5SS]
        //   * "BH" is for Far Frontiers 2526 Shadowsand (per "Rescue on Galatea")
        //
        // Future: 
        //   * support "L" | "T" | "Y" brown dwarf types

        public enum OutputFormat
        {
            Basic,        // F7 V M8 D M6 V
            Compact,      // F7V M8D M6V
            Extended      // F7 V [(M8 D M6 V)]
        };

        [Serializable]
        public class InvalidSystemException : ApplicationException
        {
            public InvalidSystemException() : base("System data is not valid") { }
            public InvalidSystemException(string message) : base(message) { }
            public InvalidSystemException(string message, Exception innerException) : base(message, innerException) { }
            protected InvalidSystemException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }

        private abstract class Unit
        {
            public static bool Parse(SeekableReader r, out Unit? unit)
            {
#if EXTENDED_SYSTEM_PARSING
                if( Pair.Parse( r, out Pair p ) )
                {
                    unit = p;
                    return true;
                }
#endif
                if (Star.Parse(r, out Star? s))
                {
                    unit = s;
                    return true;
                }

                unit = null;
                return false;
            }

            public abstract string ToString(OutputFormat format);
        }

#if EXTENDED_SYSTEM_PARSING
        private class Pair : Unit
        {
            public Star Star1;
            public Star Star2;

            public override string ToString( OutputFormat format )
            {
                switch( format )
                {
                    default:
                    case OutputFormat.Compact:
                    case OutputFormat.Basic:
                        return Star1.ToString( format ) + " " + Star2.ToString( format );
                    case OutputFormat.Extended:
                        return "(" + Star1.ToString( format ) + " " + Star2.ToString( format ) + ")";
                }
            }

            public static bool Parse( SeekableReader r, out Pair pair )
            {
                if( r.Peek() != '(' )
                {
                    pair = null;
                    return false;
                }

                pair = new Pair();
                r.Read(); // "("

                if( !Star.Parse( r, out pair.Star1 ) )
                    throw new InvalidSystemException( "Invalid star within pair" );

                if( r.Peek() != ' ' ) // w+
                    throw new InvalidSystemException( "Missing whitespace within pair" );
                while( r.Peek() == ' ' )
                    r.Read();

                if( !Star.Parse( r, out pair.Star2 ) )
                    throw new InvalidSystemException( "Invalid star within pair" );

                if( r.Read() != ')' )
                    throw new InvalidSystemException( "Unclosed pair" );

                return true;
            }
        }
#endif

        private abstract class Companion
        {
            public static bool Parse(SeekableReader r, out Companion? companion)
            {
                if (NearCompanion.Parse(r, out NearCompanion? nc))
                {
                    companion = nc;
                    return true;
                }
#if EXTENDED_SYSTEM_PARSING
                if( FarCompanion.Parse( r, out FarCompanion fc ) )
                {
                    companion = fc;
                    return true;
                }
#endif
                companion = null;
                return false;
            }

            public abstract string ToString(OutputFormat format);
        }

        private class NearCompanion : Companion
        {
            public Unit? Companion;

            public override string ToString(OutputFormat format) => Companion?.ToString(format) ?? "";

            public static bool Parse(SeekableReader r, out NearCompanion? near)
            {
                if (Unit.Parse(r, out Unit? u))
                {
                    near = new NearCompanion()
                    {
                        Companion = u
                    };
                    return true;
                }

                near = null;
                return false;
            }
        }

#if EXTENDED_SYSTEM_PARSING
        private class FarCompanion : Companion
        {
            public System Companion;
            public override string ToString( OutputFormat format )
            {
                switch( format )
                {
                    default:
                    case OutputFormat.Compact:
                    case OutputFormat.Basic:
                        return Companion.ToString( format );

                    case OutputFormat.Extended:
                        return "[" + Companion.ToString( format ) + "]";
                }
            }
            public static bool Parse( SeekableReader r, out FarCompanion far )
            {
                if( r.Peek() != '[' )
                {
                    far = null;
                    return false;
                }

                far = new FarCompanion();
                r.Read(); // '['

                if( !System.Parse(r, out far.Companion) )
                    throw new InvalidSystemException( "Invalid far companion" );

                if( r.Read() != ']' )
                    throw new InvalidSystemException( "Unclosed far companion" );

                return true;
            }
        }
#endif

        private class System
        {
            public Unit? Core;
            public List<Companion> Companions = new List<Companion>();
            public string ToString(OutputFormat format)
            {
                string s = Core?.ToString(format) ?? "";
                foreach (Companion c in Companions)                
                    s += " " + c.ToString(format);
                return s;
            }

            public static bool Parse(SeekableReader r, out System system)
            {
                if (!Unit.Parse(r, out Unit? u))
                    throw new InvalidSystemException("No core star");

                system = new System()
                {
                    Core = u
                };
                while (r.Peek() == ' ')
                {
                    while (r.Peek() == ' ') // w+
                        r.Read();

                    if (!Companion.Parse(r, out Companion? companion) || companion == null)
                        throw new InvalidSystemException("Expected companion");
                    system.Companions.Add(companion);
                }

                return true;
            }
        }

        private class Star : Unit
        {
            public string? Type;
            public int? Tenths;
            public string? Size;
            public bool Main = false;

            public override string ToString(OutputFormat format)
            {
                if (Type == null)
                    return "";

                string res;
                if (Type.Length > 1 || Type == "D")
                    res = Type;
                else
                    res = Type + Tenths.ToString() + (format == OutputFormat.Compact ? "" : " ") + Size;

                if (Main && format == OutputFormat.Extended)
                    res += "*";

                return res;
            }

            private static readonly string[] STAR_TYPES = { "O", "B", "A", "F", "G", "K", "M" };
            private static readonly string[] STAR_TENTHS = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
            private static readonly string[] STAR_SIZES = { "D", "Ia", "Ib", "II", "III", "IV", "V", "VI", "VII" };
            private static readonly string[] WHITEDWARF_SIZE = { "D" };
            private static readonly string[] WHITEDWARF_TYPES = { "DB", "DA", "DF", "DG", "DK", "DM", "D" };
            private static readonly string[] OTHER_TYPES = { "NS", "PSR", "BH", "BD", "Un" };
            
            public static bool Parse(SeekableReader r, out Star? star)
            {
                string? m;

                m = Match(r, OTHER_TYPES);
                if (m != null)
                {
                    // Brown Dwarf, Neutron Star, Pulsar, Black Hole, Unknown
                    star = new Star()
                    {
                        Type = m
                    };
                    return true;
                }

                m = Match(r, STAR_TYPES);
                if (m != null)
                {
                    // Regular
                    star = new Star()
                    {
                        Type = m
                    };
                    if (r.Peek() == ' ')
                    {
                        while (r.Peek() == ' ') // w*
                            r.Read();
                        if (Match(r, WHITEDWARF_SIZE) == null)
                            throw new InvalidSystemException("Invalid stellar type");
                        star.Tenths = 0;
                        star.Size = "D";
                    }
                    else if (Match(r, WHITEDWARF_SIZE) != null)
                    {
                        star.Tenths = 0;
                        star.Size = "D";
                    }
                    else
                    {
                        m = Match(r, STAR_TENTHS) ??
                            throw new InvalidSystemException("Invalid stellar type");
                        star.Tenths = (int)m[0] - (int)'0';

                        while (r.Peek() == ' ') // w*
                            r.Read();

                        m = Match(r, STAR_SIZES) ??
                            throw new InvalidSystemException("Invalid stellar size");
                        star.Size = m;
                    }

#if EXTENDED_SYSTEM_PARSING
                    if (r.Peek() == '*')
                    {
                        r.Read();
                        star.Main = true;
                    }
#endif
                    return true;
                }

                m = Match(r, WHITEDWARF_TYPES);
                if (m != null)
                {
                    // White Dwarf
                    star = new Star()
                    {
                        Type = m
                    };
                    return true;
                }

                star = null;
                return false;
            }
        }

        private static bool IsPrefixIn(string prefix, string[] options) => options.Any(s => s.StartsWith(prefix));

        /// <summary>
        /// Match one of a set of string options. Will return one of the options or null
        /// if there is no match.
        /// </summary>
        /// <param name="r">Text to parse</param>
        /// <param name="options">List of accepted options</param>
        /// <returns>Matched string, or null</returns>
        private static string? Match(SeekableReader r, string[] options)
        {
            string found = "";

            int pos = r.Position;

            while (r.Peek() != -1 && IsPrefixIn(found + (char)r.Peek(), options))
            {
                found += (char)r.Read();
            }

            if (options.Any(o => found == o))
                return found;

            r.Position = pos;
            return null;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="rest"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        /// <exception cref="">InvalidSystemException</exception>
        public static string Parse(string rest, OutputFormat format)
        {
            SeekableReader reader = new SeekableStringReader(rest);
            bool success = System.Parse(reader, out System system);

            if (!success)
                throw new InvalidSystemException("Could not parse as a system");

            if (reader.Peek() != -1)
                throw new InvalidSystemException($"Saw unexpected character: {(char)reader.Read()}");

            return system.ToString(format);
        }

        private abstract class SeekableReader
        {
            public abstract int Peek();
            public abstract int Read();
            public abstract int Position { get; set; }
        }

        private class SeekableStringReader : SeekableReader
        {
            private string s;

            public SeekableStringReader(string s)
            {
                this.s = s;
            }

            public override int Peek() => (0 <= Position && Position < s.Length) ? s[Position] : -1;

            public override int Read() => (0 <= Position && Position < s.Length) ? s[Position++] : -1;

            public override int Position { get; set; } = 0;
        }
    }

    internal class T5StellarData
    {
        public static bool IsValid(string stellar)
        {
            return new T5StellarData(stellar).IsValid();
        }

        private static readonly string[] SIZES = { "Ia", "Ib", "II", "III", "IV", "V", "VI", "D", "NS", "PSR", "BH", "BD" };
        private static readonly char[] SPECTRALS = { 'O', 'B', 'A', 'F', 'G', 'K', 'M' };
        private struct Star
        {
            public string size;
            public char? spectral;
            public int? digit;

            internal bool IsBiggerThanOrSame(Star rhs)
            {
                if (size != rhs.size)
                    return Array.IndexOf(SIZES, size) < Array.IndexOf(SIZES, rhs.size);

                if (!spectral.HasValue && !rhs.spectral.HasValue)
                    return true;

                if (spectral != rhs.spectral)
                    return Array.IndexOf(SPECTRALS, spectral) < Array.IndexOf(SPECTRALS, rhs.spectral);

                if (!digit.HasValue && !rhs.digit.HasValue)
                    return true;

                if (digit != rhs.digit)
                    return digit < rhs.digit;

                return true;
            }
        }

        private static readonly Regex STAR_REGEX = new Regex(@"\b(D|NS|PSR|BH|BD|[OBAFGKM][0-9]\x20(?:Ia|Ib|II|III|IV|V|VI))\b");
        public T5StellarData(string stellar)
        {
            string orig = stellar;
            foreach (Match match in STAR_REGEX.Matches(stellar))
            {
                string s = match.Value;

                if (s == "D" || s == "NS" || s == "PSR" || s == "BH" || s == "BD")
                {
                    stars.Add(new Star { size = s });
                }
                else
                {
                    stars.Add(new Star { spectral = s[0], digit = s[1] - '0', size = s.Substring(3) });
                }
            }
        }

        private List<Star> stars = new List<Star>();

        public bool IsValid()
        {
            for (int i = 1; i < stars.Count(); ++i)
            {
                if (!stars[0].IsBiggerThanOrSame(stars[i])) {
                    return false;
                }
            }
            return true;

        }

    }
}
