//#define EXTENDED_SYSTEM_PARSING

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace Maps
{
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
        //                | dwarf
        //                | browndwarf
        //                | blackhole
        //                | unknown
        //   type       ::= "O" | "B" | "A" | "F" | "G" | "K" | "M"
        //   tenths     ::= "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9"
        //   size       ::= "D" | "Ia" | "Ib" | "II" | "III" | "IV" | "V" | "VI" | "VII"
        //   dwarf      ::= "DB" | "DA" | "DF" | "DG" | "DK" | "DM" | "D"
        //   browndwarf ::= "BD"
        //   blackhole  ::= "BH"
        //   unknown    ::= "Un"
        //
        //   main       ::= "*"
        //
        //   w          ::= " "
        //
        // Notes:
        //   * "Un" is from Mendan 0221 (verified in Challenge #46)
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
            public static bool Parse(TextReader r, out Unit unit)
            {
#if EXTENDED_SYSTEM_PARSING
                Pair p;
                if( Pair.Parse( r, out p ) )
                {
                    unit = p;
                    return true;
                }
#endif
                Star s;
                if (Star.Parse(r, out s))
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

            public static bool Parse( TextReader r, out Pair pair )
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
            public static bool Parse(TextReader r, out Companion companion)
            {
                NearCompanion nc;
                if (NearCompanion.Parse(r, out nc))
                {
                    companion = nc;
                    return true;
                }
#if EXTENDED_SYSTEM_PARSING
                FarCompanion fc;
                if( FarCompanion.Parse( r, out fc ) )
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
            public Unit Companion;
            public override string ToString(OutputFormat format)
            {
                return Companion.ToString(format);
            }
            public static bool Parse(TextReader r, out NearCompanion near)
            {
                Unit u;
                if (Unit.Parse(r, out u))
                {
                    near = new NearCompanion();
                    near.Companion = u;
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
            public static bool Parse( TextReader r, out FarCompanion far )
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
            public Unit Core;
            public List<Companion> Companions = new List<Companion>();
            public string ToString(OutputFormat format)
            {
                string s = Core.ToString(format);
                foreach (Companion c in Companions)                
                    s += " " + c.ToString(format);
                return s;
            }

            public static bool Parse(TextReader r, out System system)
            {

                Unit u;
                if (!Unit.Parse(r, out u))
                    throw new InvalidSystemException("No core star");

                system = new System();
                system.Core = u;

                while (r.Peek() == ' ')
                {
                    while (r.Peek() == ' ') // w+
                        r.Read();

                    Companion companion;
                    if (!Companion.Parse(r, out companion))
                        throw new InvalidSystemException("Expected companion");
                    system.Companions.Add(companion);
                }

                return true;
            }
        }

        private class Star : Unit
        {
            public string Type;
            public int Tenths;
            public string Size;
            public bool Main = false;

            public override string ToString(OutputFormat format)
            {
                string res;
                if (Type.Length > 1)
                    res = Type;
                else
                    res = Type + Tenths.ToString() + (format == OutputFormat.Compact ? "" : " ") + Size;

                if (Main && format == OutputFormat.Extended)
                    res += "*";

                return res;
            }

            private static string[] STAR_TYPES = { "O", "B", "A", "F", "G", "K", "M" };
            private static string[] STAR_TENTHS = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
            private static string[] STAR_SIZES = { "D", "Ia", "Ib", "II", "III", "IV", "V", "VI", "VII" };
            private static string[] DWARF_SIZE = { "D" };
            private static string[] DWARF_TYPES = { "DB", "DA", "DF", "DG", "DK", "DM", "D" };
            private static string[] OTHER_TYPES = { "BD", "BH", "Un" };

            public static bool Parse(TextReader r, out Star star)
            {
                string m;

                m = Match(r, OTHER_TYPES);
                if (m != null)
                {
                    // Brown Dwarf, Black Hole, Unknown
                    star = new Star();
                    star.Type = m;
                    return true;
                }

                m = Match(r, STAR_TYPES);
                if (m != null)
                {
                    // Regular
                    star = new Star();
                    star.Type = m;

                    if (r.Peek() == ' ')
                    {
                        while (r.Peek() == ' ') // w*
                            r.Read();
                        if (Match(r, DWARF_SIZE) == null)
                            throw new InvalidSystemException("Invalid stellar type");
                        star.Tenths = 0;
                        star.Size = "D";
                    }
                    else if (Match(r, DWARF_SIZE) != null)
                    {
                        star.Tenths = 0;
                        star.Size = "D";
                    }
                    else
                    {
                        m = Match(r, STAR_TENTHS);
                        if (m == null)
                            throw new InvalidSystemException("Invalid stellar type");
                        star.Tenths = (int)m[0] - (int)'0';

                        while (r.Peek() == ' ') // w*
                            r.Read();

                        m = Match(r, STAR_SIZES);
                        if (m == null)
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

                m = Match(r, DWARF_TYPES);
                if (m != null)
                {
                    // Dwarf
                    star = new Star();
                    star.Type = m;
                    return true;
                }

                star = null;
                return false;
            }
        }

        private static bool IsPrefixIn(string prefix, string[] options)
        {
            return options.Any(s => s.StartsWith(prefix));
        }

        /// <summary>
        /// Match one of a set of string options. Will return one of the options or null
        /// if there is no match. If the stream produces a partial match (e.g. options
        /// are [ "aa", "ab" ] but the stream is "ac" then an InvalidSystemException
        /// will be thrown.
        /// </summary>
        /// <param name="r">Text to parse</param>
        /// <param name="options">List of accepted options</param>
        /// <returns>Matched string, or null</returns>
        private static string Match(TextReader r, string[] options)
        {
            string found = "";

            while (r.Peek() != -1 && IsPrefixIn(found + (char)r.Peek(), options))
            {
                found += (char)r.Read();
            }

            if (options.Any(o => found == o))
                return found;

            if (found.Length > 0)
                throw new InvalidSystemException("Invalid character in match sequence");

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
            TextReader reader = new StringReader(rest);
            System system;
            bool success = System.Parse(reader, out system);

            if (!success)
                throw new InvalidSystemException("Could not parse as a system");

            if (reader.Peek() != -1)
                throw new InvalidSystemException(string.Format("Saw unexpected character: {0}", (char)reader.Read()));

            return system.ToString(format);
        }
    }
}
