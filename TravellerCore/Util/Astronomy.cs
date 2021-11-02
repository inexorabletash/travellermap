using TravellerCore.Features;

namespace TravellerCore.Util;


    /* Traveller uses a “odd-q” vertical layout, which shoves odd columns down
         
        A sector map looks much like this:

      _____         _____         _____         _____       
     /  X G\       /  D G\       /  X G\       /     \       / 
    /   @   \_____/   @   \_____/   @   \_____/       \_____/ 
    \ NAME  /  B G\ NAME  /  B G\ NAME  /     \       /     \ 
     \ 0101/*  @   \ 0301/   @   \ 0501/       \_____/       \ 
     /  D G\ NAME  /  D G\ NAME  /  X G\       /  C G\       / 
    /   @   \ ____/   O   \^____/   @   \_____/   @   \_____/ 
    \ NAME  /  D G\ NAME  /  A G\ NAME  /  X G\ NAME  /  E G\ 
     \ 0102/   @   \ 0302/*  @   \ 0502/   @   \^0702/   @   \ 
     /  B G\ NAME  /     \ NAME  /     \ NAME  /     \ NAME  / 
    /   @   \^0202/       \ 0402/       \ 0602/       \ 0802/ 
    \ NAME  /     \       /     \       /  X G\       /  D G\ 
     \ 0103/       \_____/       \_____/   @   \_____/   O   \ 
     /  E G\       /     \       /  C G\ NAME  /  C G\ NAME  / 
    /   @   \_____/       \_____/   @   \ 0603/   @   \ 0803/ 
    \ NAME  /     \       /  X G\ NAME  /  D G\ NAME  /  X G\ 
     \ 0104/       \_____/   @   \^0504/   @   \^0704/   @   \ 
     /     \       /  X G\ NAME  /  A G\ NAME  /  A G\ NAME  / 
    /       \_____/   O   \ 0404/   @   \^0604/   @   \ 0804/ 
    \       /  C G\ NAME  /     \ NAME  /  X G\ NAME  /     \ 
     \_0105/   @   \ 0305/       \ 0505/   @   \ 0705/       \ 
     /  B G\ NAME  /  X G\       /     \ NAME  /     \       / 
    /   @   \^0205/   @   \_____/       \ 0605/       \_____/ 
    \ NAME  /     \ NAME  /  C G\       /  X G\       /     \ 
     \ 0106/       \ 0306/   @   \_____/   @   \_____/       \ 
     /     \       /  D G\ NAME  /     \ NAME  /     \       / 
    /       \_____/   O   \ 0406/       \ 0606/       \_____/ 
    \       /  X G\ NAME  /     \       /  D G\       /     \ 
     \_____/   @   \^0307/       \_____/   @   \_____/       \ 
     /     \ NAME  /  X G\       /  E G\ NAME  /     \       / 
    /       \ 0207/   @   \_____/   @   \^0607/       \_____/ 
    \       /     \ NAME  /  X G\ NAME  /  D G\       /     \ 
     \_____/       \ 0308/   @   \ 0508/   @   \_____/       \ 
     /     \       /     \ NAME  /     \ NAME  /  E G\       / 
    /       \_____/       \ 0408/       \ 0608/   @   \_____/ 
    \       /  B G\       /  D G\       /     \ NAME  /     \ 
     \_____/   @   \_____/   @   \_____/       \ 0709/       \ 
     /  X G\ NAME  /  B G\ NAME  /  B G\       /     \       / 
    /   O   \ 0209/   @   \^0409/   @   \_____/       \_____/ 
    \ NAME  /  A G\ NAME  /  E G\ NAME  /  X G\       /  C G\ 
     \ 0110/   @   \ 0310/   @   \ 0510/   @   \_____/   @   \ 
           \ NAME  /     \ NAME  /     \ NAME  /     \ NAME  /
            \ 0210/       \ 0410/       \ 0610/       \ 0810/   
    */
public static class Astronomy
{
    /* We need the following functions:
     * 
     * Find shortest path
     * Find shortest path with limitations
     * Find distance
     * 
     * And probably more.
     */
    private const double YOffset = 0.5;

    public static double CalculateDistance(Position a, Position b)
    {
        var (x, y) = CalculateOffset(a, b);

        // If A and B are both on either even or odd columns we can do the simple calculation.
        if (x%2 == 0) return Math.Sqrt(x * x + y * y);

        // If not we need to add an offset of 0.5 in one direction.
        double y2 = y;
        if (a.X % 2 == 0) y2 -= YOffset;
        else y2 += YOffset;

        return Math.Sqrt(x * x + y2 * y2);
    }

    public static int CalculateHexDistance(Position a, Position b)
    {
        var (x, y) = CalculateOffset(a, b);

        // If x is 0 this is simple, just go straight down.
        if (x == 0) return y;

        // If y < x/2 distance is x
        if (y < (x / 2)) return x;

        var distance = (x / 2) + y;

        if (x % 2 != 0) distance++;

        return distance;
    }

    public static (int, int) CalculateOffset(Position a, Position b)
    {
        var x = Math.Abs(a.X - b.X);
        var y = Math.Abs(a.Y - b.Y);
        return (x, y);
    }
}
