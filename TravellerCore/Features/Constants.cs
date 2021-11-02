namespace TravellerCore.Features;
public class Constants
{
    public class Dimensions
    {
        /// <summary>Spinward-Trailing / Left-Right</summary>
        public const int SubSectorWidth = 8;
        /// <summary>Coreward-Rimward /Up-Down</summary>
        public const int SubSectorHeight = 10;

        /// <summary>Spinward-Trailing / Left-Right</summary>
        public const int QuadrantWidth = SubSectorWidth * 2;
        /// <summary>Coreward-Rimward /Up-Down</summary>
        public const int QuadrantHeight = SubSectorHeight * 2;

        /// <summary>Spinward-Trailing / Left-Right</summary>
        public const int SectorWidth = QuadrantWidth * 2;
        /// <summary>Coreward-Rimward /Up-Down</summary>
        public const int SectorHeight = QuadrantHeight * 2;

        /// <summary>Spinward-Trailing / Left-Right</summary>
        public const int GalacticCenterX = 1;
        /// <summary>Coreward-Rimward /Up-Down</summary>
        public const int GalacticCenterY = 1;

        /// <summary>Center of the Galaxy</summary>
        public static readonly Position Center = new(GalacticCenterX, GalacticCenterY);
    }

    public class Names
    {
        public const string QuadrantA = "Quadrant Alpha";
        public const string QuadrantB = "Quadrant Beta";
        public const string QuadrantC = "Quadrant Gamma";
        public const string QuadrantD = "Quadrant Delta";

        public static readonly string[] Quadrants = { QuadrantA, QuadrantB, QuadrantC, QuadrantD };

        public const string SubSectorA = "Subsector A";
        public const string SubSectorB = "Subsector B";
        public const string SubSectorC = "Subsector C";
        public const string SubSectorD = "Subsector D";
        public const string SubSectorE = "Subsector E";
        public const string SubSectorF = "Subsector F";
        public const string SubSectorG = "Subsector G";
        public const string SubSectorH = "Subsector H";
        public const string SubSectorI = "Subsector I";
        public const string SubSectorJ = "Subsector J";
        public const string SubSectorK = "Subsector K";
        public const string SubSectorL = "Subsector L";
        public const string SubSectorM = "Subsector M";
        public const string SubSectorN = "Subsector N";
        public const string SubSectorO = "Subsector O";
        public const string SubSectorP = "Subsector P";

        public static readonly string[] SubSectors = 
        { 
            SubSectorA, SubSectorB, SubSectorC, SubSectorD,
            SubSectorE, SubSectorF, SubSectorG, SubSectorH,
            SubSectorI, SubSectorJ, SubSectorK, SubSectorL,
            SubSectorM, SubSectorN, SubSectorO, SubSectorP,
        };
    }
}
