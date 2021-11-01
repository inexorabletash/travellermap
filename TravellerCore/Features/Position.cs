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

    // Todo: There's probably a general solution that would accept a range parameter.
    public bool IsNeighbour(Position position)
    {
        // If X is offset by >1 then it can't be a neighbour.
        if (Math.Abs(position.X - X) > 1) return false;

        /* Since this is a Hex grid one of the axes behaves slightly differently,
         * in the case of traveller then it's the Y axis.
         * 
         * This means that:
         * 0504 borders 0603 and 0604, while
         * 0604 borders 0504 and 0505
         * 
         * Thus if X is even it borders the tiles at: Y and Y-1, (Minus)
         * and  if X is odd, it borders the tiles at: Y and Y+1. (Plus)
         */
        if (position.Y == Y) return true;
        if (position.X % 2 == 0) // Is X even?
        {
            if (position.Y == (Y - 1)) return true;
        }
        else
        {
            if (position.Y == (Y + 1)) return true;
        }

        return false;
    }
}

