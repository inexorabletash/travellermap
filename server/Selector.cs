using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

//- To support multiple universes/era restriction/clipping AND simplify the code:
//  s = new Selector( coord rect, ... );
//  i = s.GetWorldEnumerator()
//  i = s.GetSubsectorEnumerator()
//  i = s.GetSectorEnumerator()
//  i = s.GetBorderEnumerator()
//  ...

namespace Maps
{
    public abstract class Selector
    {
        public Selector()
        {
            Slop = true;
        }

        public bool Slop { get; set; }
        protected const float SLOP_FACTOR = 0.25f;

        public abstract IEnumerable<Sector> Sectors { get; }
        public abstract IEnumerable<World> Worlds { get; }

        public IEnumerable<Border> Borders { get { return Sectors.SelectMany(sector => sector.Borders); } }

        public IEnumerable<Route> Routes { get { return Sectors.SelectMany(sector => sector.Routes); } }

        public IEnumerable<Label> Labels { get { return Sectors.SelectMany(sector => sector.Labels); } }

        public IEnumerable<Subsector> Subsectors
        {
            get
            {
                return Sectors.SelectMany(sector => Enumerable.Range(0, 16).Select(i => sector[i]).OfType<Subsector>());
            }
        }
    }

    public class SectorSelector : Selector
    {
        Sector m_sector;
        ResourceManager m_resourceManager;

        public SectorSelector(ResourceManager resourceManager, Sector sector)
        {
            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            if (sector == null)
                throw new ArgumentNullException("sector");

            m_sector = sector;
            m_resourceManager = resourceManager;
        }


        public override IEnumerable<Sector> Sectors { get { yield return m_sector; } }

        public override IEnumerable<World> Worlds { get { return m_sector.GetWorlds(m_resourceManager, cacheResults: true); } }
    }


    public class SubsectorSelector : Selector
    {
        Sector m_sector;
        int m_index;
        ResourceManager m_resourceManager;

        public SubsectorSelector(ResourceManager resourceManager, Sector sector, int index)
        {
            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            if (sector == null)
                throw new ArgumentNullException("sector");

            if (index < 0 || index >= 16)
                throw new ArgumentOutOfRangeException("index", "index must be 0...15");

            m_sector = sector;
            m_index = index;
            m_resourceManager = resourceManager;
        }


        public override IEnumerable<Sector> Sectors { get { yield return m_sector; } }

        public override IEnumerable<World> Worlds
        {
            get
            {
                int ssx = m_index % 4;
                int ssy = m_index / 4;

                WorldCollection worlds = m_sector.GetWorlds(m_resourceManager);
                for (int x = 0; x < Astrometrics.SubsectorWidth; ++x)
                {
                    for (int y = 0; y < Astrometrics.SubsectorHeight; ++y)
                    {
                        World world = worlds[1 + x + Astrometrics.SubsectorWidth * ssx, 1 + y + Astrometrics.SubsectorHeight * ssy];
                        if (world == null)
                            continue;
                        yield return world;
                    }
                }
            }
        }
    }

    public class QuadrantSelector : Selector
    {
        Sector m_sector;
        int m_index;
        ResourceManager m_resourceManager;

        public QuadrantSelector(ResourceManager resourceManager, Sector sector, int index)
        {
            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            if (sector == null)
                throw new ArgumentNullException("sector");

            if (index < 0 || index >= 4)
                throw new ArgumentOutOfRangeException("index", "index must be 0...3");

            m_sector = sector;
            m_index = index;
            m_resourceManager = resourceManager;
        }


        public override IEnumerable<Sector> Sectors { get { yield return m_sector; } }

        public override IEnumerable<World> Worlds
        {
            get
            {
                int qx = m_index % 2;
                int qy = m_index / 2;

                WorldCollection worlds = m_sector.GetWorlds(m_resourceManager);
                for (int x = 0; x < Astrometrics.SubsectorWidth * 2; ++x)
                {
                    for (int y = 0; y < Astrometrics.SubsectorHeight * 2; ++y)
                    {
                        World world = worlds[1 + x + Astrometrics.SubsectorWidth * 2 * qx, 1 + y + Astrometrics.SubsectorHeight * 2 * qy];
                        if (world == null)
                            continue;
                        yield return world;
                    }
                }
            }
        }
    }

    public class RectSelector : Selector
    {
        SectorMap m_map;
        ResourceManager m_resourceManager;
        private RectangleF m_rect = RectangleF.Empty;

        public RectSelector(SectorMap map, ResourceManager resourceManager, RectangleF rect)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            m_map = map;
            m_resourceManager = resourceManager;
            m_rect = rect;
        }

        public override IEnumerable<Sector> Sectors
        {
            get
            {
                RectangleF rect = m_rect;
                if (Slop)
                    rect.Inflate(rect.Width * SLOP_FACTOR, rect.Height * SLOP_FACTOR);

                int sx1 = (int)Math.Floor((rect.Left + Astrometrics.ReferenceHex.X) / Astrometrics.SectorWidth);
                int sx2 = (int)Math.Floor((rect.Right + Astrometrics.ReferenceHex.X) / Astrometrics.SectorWidth);

                int sy1 = (int)Math.Floor((rect.Top + Astrometrics.ReferenceHex.Y) / Astrometrics.SectorHeight);
                int sy2 = (int)Math.Floor((rect.Bottom + Astrometrics.ReferenceHex.Y) / Astrometrics.SectorHeight);

                for (int cx = sx1; cx <= sx2; cx++)
                {
                    for (int cy = sy1; cy <= sy2; cy++)
                    {
                        Sector sector = m_map.FromLocation(cx, cy);
                        if (sector == null)
                            continue;
                        yield return sector;
                    }
                }
            }
        }

        public override IEnumerable<World> Worlds
        {
            get
            {
                RectangleF rect = m_rect;
                if (Slop)
                    rect.Inflate(rect.Width * SLOP_FACTOR, rect.Height * SLOP_FACTOR);
                
                int hx1 = (int)Math.Floor(rect.Left);
                int hx2 = (int)Math.Ceiling(rect.Right);

                int hy1 = (int)Math.Floor(rect.Top);
                int hy2 = (int)Math.Ceiling(rect.Bottom);

                Point? cachedLoc = null;
                Sector cachedSector = null;

                Point coords = Point.Empty;
                for (coords.X = hx1; coords.X <= hx2; coords.X++)
                {
                    for (coords.Y = hy1; coords.Y <= hy2; coords.Y++)
                    {
                        Location loc = Astrometrics.CoordinatesToLocation(coords);

                        if (cachedLoc != loc.SectorLocation)
                        {
                            cachedSector = m_map.FromLocation(loc.SectorLocation.X, loc.SectorLocation.Y);
                            cachedLoc = loc.SectorLocation;
                        }

                        if (cachedSector == null)
                            continue;

                        WorldCollection worlds = cachedSector.GetWorlds(m_resourceManager);
                        if (worlds == null)
                            continue;

                        World world = worlds[loc.HexLocation];
                        if (world == null)
                            continue;

                        yield return world;
                    }
                }
            }
        }
    }

    public class HexSelector : Selector
    {
        SectorMap m_map;
        ResourceManager m_resourceManager;
        private Location m_location;
        private int m_jump;

        public HexSelector(SectorMap map, ResourceManager resourceManager, Location location, int jump)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            if (resourceManager == null)            
                throw new ArgumentNullException("resourceManager");

            if (jump < 0 || jump > 36)            
                throw new ArgumentOutOfRangeException("jump", jump, "jump must be between 0 and 36 inclusive");

            m_map = map;
            m_resourceManager = resourceManager;
            m_location = location;
            m_jump = jump;
        }

        public override IEnumerable<Sector> Sectors
        {
            get
            {
                Point center = Astrometrics.LocationToCoordinates(m_location);
                Point topLeft = center;
                Point bottomRight = center;
                topLeft.Offset(-m_jump - 1, -m_jump - 1);
                bottomRight.Offset(m_jump + 1, m_jump + 1);

                Location locTL = Astrometrics.CoordinatesToLocation(topLeft);
                Location locBR = Astrometrics.CoordinatesToLocation(bottomRight);

                for (int y = locTL.SectorLocation.Y; y <= locBR.SectorLocation.Y; ++y)
                {
                    for (int x = locTL.SectorLocation.X; x <= locBR.SectorLocation.X; ++x)
                    {
                        Sector sector = m_map.FromLocation(x, y);
                        if (sector == null)
                            continue;
                        yield return sector;
                    }
                }
            }
        }

        public override IEnumerable<World> Worlds
        {
            get
            {
                Point center = Astrometrics.LocationToCoordinates(m_location);

                Point topLeft = center;
                topLeft.Offset(-m_jump - 1, -m_jump - 1);

                Point bottomRight = center;
                bottomRight.Offset(m_jump + 1, m_jump + 1);

                bool cached = false;
                Point cachedLoc = Point.Empty;
                Sector cachedSector = null;

                for (int y = topLeft.Y; y <= bottomRight.Y; ++y)
                {
                    for (int x = topLeft.X; x <= bottomRight.X; ++x)
                    {
                        Point coords = new Point(x, y);
                        if (Astrometrics.HexDistance(center, coords) <= m_jump)
                        {
                            Location loc = Astrometrics.CoordinatesToLocation(coords);

                            if (!cached || cachedLoc != loc.SectorLocation)
                            {
                                cachedSector = m_map.FromLocation(loc.SectorLocation.X, loc.SectorLocation.Y);
                                cachedLoc = loc.SectorLocation;
                                cached = true;
                            }

                            if (cachedSector != null)
                            {
                                WorldCollection worlds = cachedSector.GetWorlds(m_resourceManager);
                                if (worlds == null)
                                    continue;

                                World world = worlds[loc.HexLocation];
                                if (world == null)
                                    continue;
                                yield return world;
                            }

                        }
                    }
                }
            }
        }
    }

    public class HexSectorSelector : Selector
    {
        Sector m_sector;
        ResourceManager m_resourceManager;
        Hex m_coords;
        int m_jump;

        public HexSectorSelector(ResourceManager resourceManager, Sector sector, Hex coords, int jump)
        {
            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            if (sector == null)
                throw new ArgumentNullException("sector");
 
            m_sector = sector;
            m_resourceManager = resourceManager;
            m_coords = coords;
            m_jump = jump;
        }


        public override IEnumerable<Sector> Sectors
        {
            get
            {
                yield return m_sector;
            }
        }

        public override IEnumerable<World> Worlds
        {
            get
            {
                Point center = Astrometrics.LocationToCoordinates(new Location(Point.Empty, m_coords));
                return from world in m_sector.GetWorlds(m_resourceManager, cacheResults: true)
                       where Astrometrics.HexDistance(center, world.Coordinates) <= m_jump
                       select world;
            }
        }
    }
}
