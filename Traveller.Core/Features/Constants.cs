namespace Traveller.Core.Features;
public class Constants
{
    public static class Dimensions
    {
        /* Axes
         *        Coreward / -Y
         *             |
         * Spinward    |
         * -X  --------+--------   +X
         *             |     Trailing
         *             |
         *         Rimward / +Y
         */

        /// <summary>Spinward-Trailing / Left-Right / X</summary>
        public const int SubSectorWidth = 8;
        /// <summary>Coreward-Rimward /Up-Down / Y</summary>
        public const int SubSectorHeight = 10;

        /// <summary>Spinward-Trailing / Left-Right / X</summary>
        public const int QuadrantWidth = SubSectorWidth * 2;
        /// <summary>Coreward-Rimward /Up-Down / Y</summary>
        public const int QuadrantHeight = SubSectorHeight * 2;

        /// <summary>Spinward-Trailing / Left-Right / X</summary>
        public const int SectorWidth = QuadrantWidth * 2;
        /// <summary>Coreward-Rimward /Up-Down / Y</summary>
        public const int SectorHeight = QuadrantHeight * 2;

        /// <summary>Spinward-Trailing / Left-Right / X</summary>
        public const int GalacticCenterX = 1;
        /// <summary>Coreward-Rimward /Up-Down / Y</summary>
        public const int GalacticCenterY = 1;

        /// <summary>Center of the Galaxy</summary>
        public static readonly Position Center = new(GalacticCenterX, GalacticCenterY);
    }

    public static class Names
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
    public static class Positions
    {
        /* Quadrants:
         * A B
         * C D
         */

        public static readonly (int, int) QuadrantAOffset = (0, 0);
        public static readonly (int, int) QuadrantBOffset = (0, 0);
        public static readonly (int, int) QuadrantCOffset = (0, 0);
        public static readonly (int, int) QuadrantDOffset = (0, 0);

        public static readonly (int, int)[] QuadrantOffsets = { QuadrantAOffset, QuadrantBOffset, QuadrantCOffset, QuadrantDOffset };

        public static readonly Position QuadrantA = new(QuadrantAOffset);
        public static readonly Position QuadrantB = new(QuadrantBOffset);
        public static readonly Position QuadrantC = new(QuadrantCOffset);
        public static readonly Position QuadrantD = new(QuadrantDOffset);

        public static readonly Position[] Quadrants = { QuadrantA, QuadrantB, QuadrantC, QuadrantD };

        /* Subsectors:
         * A B C D
         * E F G H
         * I J K L
         * M N O P
         */
        public static readonly (int, int) SubSectorAOffset = (0, 0);
        public static readonly (int, int) SubSectorBOffset = (1, 0);
        public static readonly (int, int) SubSectorCOffset = (2, 0);
        public static readonly (int, int) SubSectorDOffset = (3, 0);
        public static readonly (int, int) SubSectorEOffset = (0, 1);
        public static readonly (int, int) SubSectorFOffset = (1, 1);
        public static readonly (int, int) SubSectorGOffset = (2, 1);
        public static readonly (int, int) SubSectorHOffset = (3, 1);
        public static readonly (int, int) SubSectorIOffset = (0, 2);
        public static readonly (int, int) SubSectorJOffset = (1, 2);
        public static readonly (int, int) SubSectorKOffset = (2, 2);
        public static readonly (int, int) SubSectorLOffset = (3, 2);
        public static readonly (int, int) SubSectorMOffset = (0, 3);
        public static readonly (int, int) SubSectorNOffset = (1, 3);
        public static readonly (int, int) SubSectorOOffset = (2, 3);
        public static readonly (int, int) SubSectorPOffset = (3, 3);

        public static readonly (int, int)[] SubSectorOffsets =
        {
            SubSectorAOffset, SubSectorBOffset, SubSectorCOffset, SubSectorDOffset,
            SubSectorEOffset, SubSectorFOffset, SubSectorGOffset, SubSectorHOffset,
            SubSectorIOffset, SubSectorJOffset, SubSectorKOffset, SubSectorLOffset,
            SubSectorMOffset, SubSectorNOffset, SubSectorOOffset, SubSectorPOffset,
        };

        public static readonly Position SubSectorA = new(SubSectorAOffset);
        public static readonly Position SubSectorB = new(SubSectorBOffset);
        public static readonly Position SubSectorC = new(SubSectorCOffset);
        public static readonly Position SubSectorD = new(SubSectorDOffset);
        public static readonly Position SubSectorE = new(SubSectorEOffset);
        public static readonly Position SubSectorF = new(SubSectorFOffset);
        public static readonly Position SubSectorG = new(SubSectorGOffset);
        public static readonly Position SubSectorH = new(SubSectorHOffset);
        public static readonly Position SubSectorI = new(SubSectorIOffset);
        public static readonly Position SubSectorJ = new(SubSectorJOffset);
        public static readonly Position SubSectorK = new(SubSectorKOffset);
        public static readonly Position SubSectorL = new(SubSectorLOffset);
        public static readonly Position SubSectorM = new(SubSectorMOffset);
        public static readonly Position SubSectorN = new(SubSectorNOffset);
        public static readonly Position SubSectorO = new(SubSectorOOffset);
        public static readonly Position SubSectorP = new(SubSectorPOffset);

        public static readonly Position[] SubSectors =
        {
            SubSectorA, SubSectorB, SubSectorC, SubSectorD,
            SubSectorE, SubSectorF, SubSectorG, SubSectorH,
            SubSectorI, SubSectorJ, SubSectorK, SubSectorL,
            SubSectorM, SubSectorN, SubSectorO, SubSectorP,
        };
    }
}
