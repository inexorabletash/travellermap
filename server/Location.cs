using System;
using System.Drawing;
using System.Xml.Serialization;

namespace Maps
{
    public struct Location : IEquatable<Location>
    {
        internal Location(Point sectorLocation, int hex)
        {
            Sector = sectorLocation;
            Hex = new Hex(hex);
        }

        internal Location(Point sectorLocation, Hex hexLocation)
        {
            Sector = sectorLocation;
            Hex = hexLocation;
        }

        internal Point Sector { get; set; }
        internal Hex Hex { get; set; }

        // For XML Deserialization:
        [XmlAttribute("Sector")]
        public string SectorName { get => null; set => Sector = SectorMap.GetSectorCoordinatesByName(value); }
        [XmlAttribute("Hex")]
        public string HexName { get => null; set => Hex = new Hex(value); }

        public bool IsEmpty => Sector.IsEmpty && Hex.IsEmpty;
        public bool IsValid => Hex.IsValid;
        public bool Equals(Location other)
        {
            return other.Sector == Sector && other.Hex == Hex;
        }
        public override bool Equals(object other)
        {
            return other is Location location && Equals(location);
        }
        public static bool operator ==(Location a, Location b) { return a.Equals(b); }
        public static bool operator !=(Location a, Location b) { return !a.Equals(b); }

        private static bool IsLessThan(Point a, Point b) { return (a.X < b.X) || (a.X == b.X && a.Y < b.Y); }
        private static bool IsGreaterThan(Point a, Point b) { return (a.X > b.X) || (a.X == b.X && a.Y > b.Y); }

        public static bool operator <(Location a, Location b)
        {
            return IsLessThan(a.Sector, b.Sector) || (a.Sector == b.Sector && a.Hex < b.Hex);
        }
        public static bool operator >(Location a, Location b)
        {
            return IsGreaterThan(a.Sector, b.Sector) || (a.Sector == b.Sector && a.Hex > b.Hex);
        }

        public override int GetHashCode()
        {
            return Sector.GetHashCode() ^ Hex.GetHashCode();
        }

        public string HexString => Hex.ToString();
        public string SubsectorHexString => Hex.ToSubsectorString();
        public static readonly Location Empty = new Location();
    }
}
