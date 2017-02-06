using Json;
using System;
using System.Drawing;
using System.Globalization;
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
        public string SectorName { get { return null; } set { Sector = SectorMap.GetSectorCoordinatesByName(value); } }
        [XmlAttribute("Hex")]
        public string HexName { get { return null; } set { Hex = new Hex(value); } }

        public bool IsEmpty { get { return Sector.IsEmpty && Hex.IsEmpty; } }
        public bool IsValid { get { return Hex.IsValid; } }

        public bool Equals(Location other)
        {
            return other.Sector == Sector && other.Hex == Hex;
        }
        public override bool Equals(object other)
        {
            return other is Location && Equals((Location)other);
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

        public string HexString
        {
            get { return Hex.ToString(); }
        }

        public string SubsectorHexString
        {
            get { return Hex.ToSubsectorString(); }
        }

        public static readonly Location Empty = new Location();
    }

    [XmlInclude(typeof(WorldLocation)),
     XmlInclude(typeof(SubsectorLocation)),
     XmlInclude(typeof(SectorLocation)),
     XmlInclude(typeof(LabelLocation))]
    internal abstract class ItemLocation
    {
    }

    internal class WorldLocation : ItemLocation
    {
        public WorldLocation() { }

        public WorldLocation(Sector sector, World world)
        {
            if (sector == null)
                throw new ArgumentNullException(nameof(sector));
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            Sector = sector.Location;
            Hex = new Hex(world.X, world.Y);
        }

        public WorldLocation(int sector_x, int sector_y, byte hex_x, byte hex_y)
        {
            Sector = new Point(sector_x, sector_y);
            Hex = new Hex(hex_x, hex_y);
        }

        public Point Sector { get; set; }
        public Hex Hex { get; set; }

        public void Resolve(SectorMap.Milieu sectorMap, ResourceManager resourceManager, out Sector sector, out World world)
        {
            if (sectorMap == null)
                throw new ArgumentNullException(nameof(sectorMap));

            sector = null;
            world = null;

            sector = sectorMap.FromLocation(Sector.X, Sector.Y);
            if (sector == null)
                return;

            WorldCollection worlds = sector.GetWorlds(resourceManager, cacheResults: true);
            if (worlds != null)
                world = worlds[Hex];
        }
    }

    internal class SubsectorLocation : ItemLocation
    {
        public SubsectorLocation() { }

        public SubsectorLocation(Sector sector, Subsector subsector)
        {
            if (sector == null)
                throw new ArgumentNullException(nameof(sector));
            if (subsector == null)
                throw new ArgumentNullException(nameof(subsector));

            SectorLocation = sector.Location;
            Index = subsector.Index[0];
        }

        public SubsectorLocation(int x, int y, char index)
        {
            SectorLocation = new Point(x, y);
            Index = index;
        }

        public Point SectorLocation { get; set; }
        public char Index { get; set; }

        public void Resolve(SectorMap.Milieu sectorMap, out Sector sector, out Subsector subsector)
        {
            if (sectorMap == null)
                throw new ArgumentNullException(nameof(sectorMap));

            sector = null;
            subsector = null;

            sector = sectorMap.FromLocation(SectorLocation.X, SectorLocation.Y);
            if (sector != null)
                subsector = sector.Subsector(Index);
        }
    }

    internal class SectorLocation : ItemLocation
    {
        public SectorLocation() { }

        public SectorLocation(Sector sector)
        {
            if (sector == null)
                throw new ArgumentNullException(nameof(sector));

            SectorCoords = sector.Location;
        }
        public SectorLocation(int x, int y)
        {
            SectorCoords = new Point(x, y);
        }

        public Point SectorCoords { get; set; }

        public Sector Resolve(SectorMap.Milieu sectorMap)
        {
            if (sectorMap == null)
                throw new ArgumentNullException(nameof(sectorMap));

            return sectorMap.FromLocation(SectorCoords.X, SectorCoords.Y);
        }
    }

    internal class LabelLocation : ItemLocation
    {
        public LabelLocation() { }

        public LabelLocation(string label, Point coords, int radius)
        {
            Label = label;
            Coords = coords;
            Radius = radius;
        }

        public string Label { get; set; }
        public Point Coords { get; set; }
        public int Radius { get; set; }

        public Sector Resolve(SectorMap.Milieu sectorMap)
        {
            if (sectorMap == null)
                throw new ArgumentNullException(nameof(sectorMap));

            return sectorMap.FromLocation(Astrometrics.CoordinatesToLocation(Coords).Sector);
        }

    }
}
