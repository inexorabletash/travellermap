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
        public string SectorName { get { return null; } set { Sector = SectorMap.GetInstance().FromName(value).Location; } }
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

    [XmlInclude(typeof(WorldLocation)), XmlInclude(typeof(SubsectorLocation)), XmlInclude(typeof(SectorLocation))]
    internal abstract class ItemLocation
    {
    }

    internal class WorldLocation : ItemLocation
    {
        public WorldLocation() { }

        public WorldLocation(Sector sector, World world)
        {
            if (sector == null)
                throw new ArgumentNullException("sector");
            if (world == null)
                throw new ArgumentNullException("world");

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

        public void Resolve(SectorMap sectorMap, ResourceManager resourceManager, out Sector sector, out World world)
        {
            if (sectorMap == null)
                throw new ArgumentNullException("sectorMap");

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
                throw new ArgumentNullException("sector");
            if (subsector == null)
                throw new ArgumentNullException("subsector");

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

        public void Resolve(SectorMap sectorMap, out Sector sector, out Subsector subsector)
        {
            if (sectorMap == null)
                throw new ArgumentNullException("sectorMap");

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
                throw new ArgumentNullException("sector");

            SectorCoords = sector.Location;
        }
        public SectorLocation(int x, int y)
        {
            SectorCoords = new Point(x, y);
        }

        public Point SectorCoords { get; set; }

        public Sector Resolve(SectorMap sectorMap)
        {
            if (sectorMap == null)
                throw new ArgumentNullException("sectorMap");

            return sectorMap.FromLocation(SectorCoords.X, SectorCoords.Y);
        }
    }
}
