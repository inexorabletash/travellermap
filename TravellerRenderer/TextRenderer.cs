namespace TravellerRenderer;
public class TextRenderer
{
    public static string Name = "Text Grid Renderer";
    public static string Description = "A renderer that outputs a simple text grid representing an area. Useful for command line interfaces and chat bots.";

    public static async Task<string> RenderAsync(Galaxy galaxy, Position min, Position max, DetailLevel detailLevel = DetailLevel.Two)
    {
        
        return "";
    }

    public static async Task<string> RenderWorldsFourLine(List<World> worlds, int X, int Y)
    {
        var result = string.Empty;

        // Convert worlds to lines of text.

        // Create grid of appropriate size.

        // Add world lines to grid.

        return result;
    }

    public static List<string> WorldToFourStrings(World world)
    {
        var strings = new List<string>(4);
        // Lines are 5 7 7 5 characters. For the lowest line, if they're used the underlines disappear.

        var gasGiant = world.GasGiants > 0 ? 'G' : ' ';

        var worldSymbol = (world.Uwp.Hydrology.GetRawValue(), world.Uwp.Size.GetRawValue()) switch
        {
            (_, 0) => "X",
            (0, _) => "O";
            var (w, _) when w >0 && w < 11 => "@",
            var (w, _) when w > 10 => "H",

            (_,_) => "?",
        };

        strings.Add($"  {world.Uwp.Starport} {gasGiant}");
        strings.Add($"{world.TravelCode}  {worldSymbol}   ");
        strings.Add(world.Name.Substring(0, Math.Min(7, world.Name.Length)).PadLeft(7));
        strings.Add($" {world.Position.ToSubSector()}");

        return strings;
    }
}

public enum DetailLevel
{
    Two,
    Four,
}
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