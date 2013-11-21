using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml.Serialization;
using Json;

namespace Maps
{
    public struct Location
    {
        public Location(string sectorName, int hexX, int hexY)
            : this()
        {
            SettingName = SectorMap.DefaultSetting;

            SectorName = sectorName;

            SectorLocation = Point.Empty;
            HexLocation = new Point(hexX, hexY);
        }

        public Location(string sectorName, int hex)
            : this()
        {
            SettingName = SectorMap.DefaultSetting;

            SectorName = sectorName;

            Hex = hex;
        }

        public Location(Point sectorLocation, int hex)
            : this()
        {
            SettingName = SectorMap.DefaultSetting;

            SectorLocation = sectorLocation;
            m_sectorName = null;

            Hex = hex;
        }

        public Location(Point sectorLocation, Point hexLocation)
            : this()
        {
            SettingName = SectorMap.DefaultSetting;

            SectorLocation = sectorLocation;
            m_sectorName = null;

            HexLocation = hexLocation;
        }

        private string m_sectorName;

        public string SettingName { get; set; }
        public string SectorName { get { return m_sectorName; } set { m_sectorName = value; SectorLocation = SectorMap.FromName(SettingName, value).Location; } }
        public int Hex { get { return HexLocation.X * 100 + HexLocation.Y; } set { HexLocation = new Point(value / 100, value % 100); } }

        [XmlIgnore, JsonIgnore]
        public Point SectorLocation { get; set; }

        [XmlIgnore, JsonIgnore]
        public Point HexLocation { get; set; }

        public override bool Equals(object obj)
        {
            Location loc = (Location)obj;

            return
                (this.SectorLocation == loc.SectorLocation) &&
                (this.HexLocation == loc.HexLocation);
        }

        public static bool operator ==(Location location1, Location location2) { return location1.Equals(location2); }
        public static bool operator !=(Location location1, Location location2) { return !location1.Equals(location2); }

        public override int GetHashCode()
        {
            return SectorLocation.GetHashCode() ^ HexLocation.GetHashCode();
        }
    }

    [XmlInclude(typeof(WorldLocation)), XmlInclude(typeof(SubsectorLocation)), XmlInclude(typeof(SectorLocation))]
    public abstract class ItemLocation
    {
    }

    public class WorldLocation : ItemLocation
    {
        public WorldLocation() { }

        public WorldLocation(Sector sector, World world)
        {
            if (sector == null)
                throw new ArgumentNullException("sector");
            if (world == null)
                throw new ArgumentNullException("world");

            Sector = sector.Location;
            World = world.Location;
        }

        public WorldLocation(int sector_x, int sector_y, int hex_x, int hex_y)
        {
            Sector = new Point(sector_x, sector_y);
            World = new Point(hex_x, hex_y);
        }

        public Point Sector { get; set; }
        public Point World { get; set; }

        public void Resolve(SectorMap sectorMap, ResourceManager resourceManager, out Sector sector, out World world)
        {
            if (sectorMap == null)
            {
                throw new ArgumentNullException("sectorMap");
            }

            sector = null;
            world = null;

            sector = sectorMap.FromLocation(Sector.X, Sector.Y);
            if (sector != null)
            {
                WorldCollection worlds = sector.GetWorlds(resourceManager, cacheResults: true);
                if (worlds != null)
                {
                    world = worlds[World.X, World.Y];
                }
            }
        }
    }

    public class SubsectorLocation : ItemLocation
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
            {
                throw new ArgumentNullException("sectorMap");
            }

            sector = null;
            subsector = null;

            sector = sectorMap.FromLocation(SectorLocation.X, SectorLocation.Y);
            if (sector != null)
            {
                subsector = sector[Index];
            }
        }
    }

    public class SectorLocation : ItemLocation
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
