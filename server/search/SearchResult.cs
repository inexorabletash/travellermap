#nullable enable
using System;
using System.Drawing;

namespace Maps.Search
{
    internal abstract class SearchResult
    {
    }

    internal class WorldResult : SearchResult
    {
        public WorldResult() { }

        public WorldResult(Sector sector, World world)
        {
            if (sector == null)
                throw new ArgumentNullException(nameof(sector));
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            Sector = sector.Location;
            Hex = new Hex(world.X, world.Y);
        }

        public WorldResult(int sector_x, int sector_y, byte hex_x, byte hex_y)
        {
            Sector = new Point(sector_x, sector_y);
            Hex = new Hex(hex_x, hex_y);
        }

        public Point Sector { get; set; }
        public Hex Hex { get; set; }

        public void Resolve(SectorMap.Milieu sectorMap, ResourceManager resourceManager, out Sector? sector, out World? world)
        {
            if (sectorMap == null)
                throw new ArgumentNullException(nameof(sectorMap));

            sector = null;
            world = null;

            sector = sectorMap.FromLocation(Sector.X, Sector.Y);
            if (sector == null)
                return;

            WorldCollection? worlds = sector.GetWorlds(resourceManager, cacheResults: true);
            if (worlds != null)
                world = worlds[Hex];
        }
    }

    internal class SubsectorResult : SearchResult
    {
        public SubsectorResult() { }

        public SubsectorResult(Sector sector, Subsector subsector)
        {
            if (sector == null)
                throw new ArgumentNullException(nameof(sector));
            if (subsector == null)
                throw new ArgumentNullException(nameof(subsector));

            SectorLocation = sector.Location;
            Index = subsector.Index[0];
        }

        public SubsectorResult(int x, int y, char index)
        {
            SectorLocation = new Point(x, y);
            Index = index;
        }

        public Point SectorLocation { get; set; }
        public char Index { get; set; }

        public void Resolve(SectorMap.Milieu sectorMap, out Sector? sector, out Subsector? subsector)
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

    internal class SectorResult : SearchResult
    {
        public SectorResult() { }

        public SectorResult(Sector sector)
        {
            if (sector == null)
                throw new ArgumentNullException(nameof(sector));

            SectorCoords = sector.Location;
        }
        public SectorResult(int x, int y)
        {
            SectorCoords = new Point(x, y);
        }

        public Point SectorCoords { get; set; }

        public Sector? Resolve(SectorMap.Milieu sectorMap)
        {
            if (sectorMap == null)
                throw new ArgumentNullException(nameof(sectorMap));

            return sectorMap.FromLocation(SectorCoords.X, SectorCoords.Y);
        }
    }

    internal class LabelResult : SearchResult
    {
        public LabelResult() { }

        public LabelResult(string label, Point coords, int radius)
        {
            Label = label;
            Coords = coords;
            Radius = radius;
        }

        public string Label { get; set; } = "";
        public Point Coords { get; set; }
        public int Radius { get; set; }

        public Sector? Resolve(SectorMap.Milieu sectorMap)
        {
            if (sectorMap == null)
                throw new ArgumentNullException(nameof(sectorMap));

            return sectorMap.FromLocation(Astrometrics.CoordinatesToLocation(Coords).Sector);
        }

    }
}
