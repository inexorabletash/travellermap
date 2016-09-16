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
    internal abstract class Selector
    {
        protected Selector()
        {
        }

        public bool Slop { get; set; }
        public float SlopFactor { get; set; }
        public bool UseMilieuFallbacks { get; set; }

        public abstract IEnumerable<Sector> Sectors { get; }
        public abstract IEnumerable<World> Worlds { get; }

        public IEnumerable<Border> Borders { get { return Sectors.SelectMany(sector => sector.Borders); } }

        public IEnumerable<Route> Routes { get { return Sectors.SelectMany(sector => sector.Routes); } }

        public IEnumerable<Label> Labels { get { return Sectors.SelectMany(sector => sector.Labels); } }

        public IEnumerable<Subsector> Subsectors
        {
            get
            {
                return Sectors.SelectMany(sector => Enumerable.Range(0, 16).Select(i => sector.Subsector(i)).OfType<Subsector>());
            }
        }
    }

    internal class SectorSelector : Selector
    {
        Sector sector;
        ResourceManager resourceManager;

        public SectorSelector(ResourceManager resourceManager, Sector sector)
        {
            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            if (sector == null)
                throw new ArgumentNullException("sector");

            this.sector = sector;
            this.resourceManager = resourceManager;
        }


        public override IEnumerable<Sector> Sectors { get { yield return sector; } }

        public override IEnumerable<World> Worlds { get { return sector.GetWorlds(resourceManager, cacheResults: true); } }
    }


    internal class SubsectorSelector : Selector
    {
        Sector sector;
        int index;
        ResourceManager resourceManager;

        public SubsectorSelector(ResourceManager resourceManager, Sector sector, int index)
        {
            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            if (sector == null)
                throw new ArgumentNullException("sector");

            if (index < 0 || index >= 16)
                throw new ArgumentOutOfRangeException("index", "index must be 0...15");

            this.sector = sector;
            this.index = index;
            this.resourceManager = resourceManager;
        }


        public override IEnumerable<Sector> Sectors { get { yield return sector; } }

        public override IEnumerable<World> Worlds
        {
            get
            {
                int ssx = index % 4;
                int ssy = index / 4;

                WorldCollection worlds = sector.GetWorlds(resourceManager);
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

    internal class QuadrantSelector : Selector
    {
        Sector sector;
        int index;
        ResourceManager resourceManager;

        public QuadrantSelector(ResourceManager resourceManager, Sector sector, int index)
        {
            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            if (sector == null)
                throw new ArgumentNullException("sector");

            if (index < 0 || index >= 4)
                throw new ArgumentOutOfRangeException("index", "index must be 0...3");

            this.sector = sector;
            this.index = index;
            this.resourceManager = resourceManager;
        }


        public override IEnumerable<Sector> Sectors { get { yield return sector; } }

        public override IEnumerable<World> Worlds
        {
            get
            {
                int qx = index % 2;
                int qy = index / 2;

                WorldCollection worlds = sector.GetWorlds(resourceManager);
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

    internal class RectSelector : Selector
    {
        SectorMap.Milieu map;
        ResourceManager resourceManager;
        private RectangleF rect = RectangleF.Empty;

        public RectSelector(SectorMap.Milieu map, ResourceManager resourceManager, RectangleF rect, bool slop = true)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            this.map = map;
            this.resourceManager = resourceManager;
            this.rect = rect;

            Slop = slop;
            SlopFactor = 0.25f;
        }

        public override IEnumerable<Sector> Sectors
        {
            get
            {
                RectangleF rect = this.rect;
                if (Slop)
                    rect.Inflate(rect.Width * SlopFactor, rect.Height * SlopFactor);

                int sx1 = (int)Math.Floor((rect.Left + Astrometrics.ReferenceHex.X) / Astrometrics.SectorWidth);
                int sx2 = (int)Math.Floor((rect.Right + Astrometrics.ReferenceHex.X) / Astrometrics.SectorWidth);

                int sy1 = (int)Math.Floor((rect.Top + Astrometrics.ReferenceHex.Y) / Astrometrics.SectorHeight);
                int sy2 = (int)Math.Floor((rect.Bottom + Astrometrics.ReferenceHex.Y) / Astrometrics.SectorHeight);

                for (int cx = sx1; cx <= sx2; cx++)
                {
                    for (int cy = sy1; cy <= sy2; cy++)
                    {
                        Sector sector = map.FromLocation(cx, cy, UseMilieuFallbacks);
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
                RectangleF rect = this.rect;
                if (Slop)
                    rect.Inflate(rect.Width * SlopFactor, rect.Height * SlopFactor);
                
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

                        if (cachedLoc != loc.Sector)
                        {
                            cachedSector = map.FromLocation(loc.Sector.X, loc.Sector.Y, UseMilieuFallbacks);
                            cachedLoc = loc.Sector;
                        }

                        if (cachedSector == null)
                            continue;

                        WorldCollection worlds = cachedSector.GetWorlds(resourceManager);
                        if (worlds == null)
                            continue;

                        World world = worlds[loc.Hex];
                        if (world == null)
                            continue;

                        yield return world;
                    }
                }
            }
        }
    }

    internal class HexSelector : Selector
    {
        SectorMap.Milieu map;
        ResourceManager resourceManager;
        private Location location;
        private int jump;

        public HexSelector(SectorMap.Milieu map, ResourceManager resourceManager, Location location, int jump)
        {
            if (map == null)
                throw new ArgumentNullException("map");

            if (resourceManager == null)            
                throw new ArgumentNullException("resourceManager");

            if (jump < 0 || jump > 36)            
                throw new ArgumentOutOfRangeException("jump", jump, "jump must be between 0 and 36 inclusive");

            this.map = map;
            this.resourceManager = resourceManager;
            this.location = location;
            this.jump = jump;
        }

        public override IEnumerable<Sector> Sectors
        {
            get
            {
                Point center = Astrometrics.LocationToCoordinates(location);
                Point topLeft = center;
                Point bottomRight = center;
                topLeft.Offset(-jump - 1, -jump - 1);
                bottomRight.Offset(jump + 1, jump + 1);

                Location locTL = Astrometrics.CoordinatesToLocation(topLeft);
                Location locBR = Astrometrics.CoordinatesToLocation(bottomRight);

                for (int y = locTL.Sector.Y; y <= locBR.Sector.Y; ++y)
                {
                    for (int x = locTL.Sector.X; x <= locBR.Sector.X; ++x)
                    {
                        Sector sector = map.FromLocation(x, y, UseMilieuFallbacks);
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
                Point center = Astrometrics.LocationToCoordinates(location);

                Point topLeft = center;
                topLeft.Offset(-jump - 1, -jump - 1);

                Point bottomRight = center;
                bottomRight.Offset(jump + 1, jump + 1);

                bool cached = false;
                Point cachedLoc = Point.Empty;
                Sector cachedSector = null;

                for (int y = topLeft.Y; y <= bottomRight.Y; ++y)
                {
                    for (int x = topLeft.X; x <= bottomRight.X; ++x)
                    {
                        Point coords = new Point(x, y);
                        if (Astrometrics.HexDistance(center, coords) <= jump)
                        {
                            Location loc = Astrometrics.CoordinatesToLocation(coords);

                            if (!cached || cachedLoc != loc.Sector)
                            {
                                cachedSector = map.FromLocation(loc.Sector.X, loc.Sector.Y, UseMilieuFallbacks);
                                cachedLoc = loc.Sector;
                                cached = true;
                            }

                            if (cachedSector != null)
                            {
                                WorldCollection worlds = cachedSector.GetWorlds(resourceManager);
                                if (worlds == null)
                                    continue;

                                World world = worlds[loc.Hex];
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

    internal class HexSectorSelector : Selector
    {
        Sector sector;
        ResourceManager resourceManager;
        Hex coords;
        int jump;

        public HexSectorSelector(ResourceManager resourceManager, Sector sector, Hex coords, int jump)
        {
            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            if (sector == null)
                throw new ArgumentNullException("sector");
 
            this.sector = sector;
            this.resourceManager = resourceManager;
            this.coords = coords;
            this.jump = jump;
        }


        public override IEnumerable<Sector> Sectors
        {
            get
            {
                yield return sector;
            }
        }

        public override IEnumerable<World> Worlds
        {
            get
            {
                Point center = Astrometrics.LocationToCoordinates(new Location(Point.Empty, coords));
                return from world in sector.GetWorlds(resourceManager, cacheResults: true)
                       where Astrometrics.HexDistance(center, world.Coordinates) <= jump
                       select world;
            }
        }
    }
}
