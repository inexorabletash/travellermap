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
    }
}
