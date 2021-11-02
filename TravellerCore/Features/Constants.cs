namespace TravellerCore.Features;
public class Constants
{
    public class Dimensions
    {
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
}
