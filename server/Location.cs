#nullable enable
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
        public string? SectorName { get => null; set { if (value != null) Sector = SectorMap.GetSectorCoordinatesByName(value); } }
        [XmlAttribute("Hex")]
        public string? HexName { get => null; set { if (value != null) Hex = new Hex(value); } }

        public bool IsEmpty => Sector.IsEmpty && Hex.IsEmpty;
        public bool IsValid => Hex.IsValid;
        public bool Equals(Location other) => other.Sector == Sector && other.Hex == Hex;
        public override bool Equals(object other) => other is Location location && Equals(location);
        public static bool operator ==(Location a, Location b) => a.Equals(b);
        public static bool operator !=(Location a, Location b) => !a.Equals(b);

        private static bool IsLessThan(Point a, Point b) => (a.X < b.X) || (a.X == b.X && a.Y < b.Y);
        private static bool IsGreaterThan(Point a, Point b) => (a.X > b.X) || (a.X == b.X && a.Y > b.Y);

        public static bool operator <(Location a, Location b) =>
            IsLessThan(a.Sector, b.Sector) || (a.Sector == b.Sector && a.Hex < b.Hex);
        public static bool operator >(Location a, Location b) =>
            IsGreaterThan(a.Sector, b.Sector) || (a.Sector == b.Sector && a.Hex > b.Hex);

        public override int GetHashCode() => Sector.GetHashCode() ^ Hex.GetHashCode();

        public string HexString => Hex.ToString();
        public string SubsectorHexString => Hex.ToSubsectorString();
        public static readonly Location Empty = new Location();
    }
}
