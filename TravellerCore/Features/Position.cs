namespace TravellerCore.Features;
public class Position
{
    /// <summary>Trailing / Right</summary>
    public int X { get; init; }
    /// <summary>Rimwards / Down</summary>
    public int Y { get; init; }

    public Position(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString()
    {
        return X.ToString().PadLeft(2) + Y.ToString().PadLeft(2);
    }

    public Position Tosector() => new(
            X % Constants.Dimensions.SectorWidth,
            Y % Constants.Dimensions.SectorHeight);

    public Position ToQuadrant() => new(
            X % Constants.Dimensions.QuadrantWidth,
            Y % Constants.Dimensions.QuadrantHeight);
    
    public Position ToSubsector() => new(
            X % Constants.Dimensions.SubSectorWidth,
            Y % Constants.Dimensions.SubSectorHeight);
}

