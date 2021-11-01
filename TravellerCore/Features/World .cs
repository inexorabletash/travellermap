using System.Text.RegularExpressions;

namespace TravellerCore.Features;
public class World
{
    public string Name { get; init; }
    public UWP Uwp { get; init; }
    public Position Position {  get; init; }
    public TravelCode TravelCode { get; init; }

    public World(string name, UWP uwp, Position position, TravelCode travelCode)
    {
        Name = name;
        Uwp = uwp;
        Position = position;
        TravelCode = travelCode;
    }
}

public enum TravelCode
{
    G = 0, // Green/None
    A = 1, // Amber - Caution advised
    R = 2, // Red - Interdicted
    B = 3, // Blue - TNE Technologically Elevated Dictatorship
    F = 4, // Forbidden (Zhodani)
    U = 5, // Unabsorbed (Zhodani)
}

public class UWP
{
    public HexValue Starport { get; init; }
    public HexValue Size { get; init; }
    public HexValue Atmosphere { get; init; }
    public HexValue Hydrology { get; init; }
    public HexValue Population { get; init; }
    public HexValue Government { get; init; }
    public HexValue Law { get; init; }
    public HexValue Tech { get; init; }

    public UWP(string raw)
    {
        if (!Regex.IsMatch(raw, "[ABCDEX][0-9A-Z]{6}-[0-9A-Z]")) throw new ArgumentException("Invalid UWP");
        var parts = raw.Replace("-", string.Empty).Split(string.Empty);
        var parsedParts = parts.Select(p => new HexValue(p)).ToArray();
        Starport    = parsedParts[0];
        Size        = parsedParts[1];
        Atmosphere  = parsedParts[2];
        Hydrology   = parsedParts[3];
        Population  = parsedParts[4];
        Government  = parsedParts[5];
        Law         = parsedParts[6];
        Tech        = parsedParts[7];
    }

    public override string ToString()
    {
        return $"{Starport}{Size}{Atmosphere}{Hydrology}{Population}{Government}{Law}-{Tech}";
    }
}

public struct HexValue
{
    private int value { get; init; }

    public HexValue(int v) => value = v;
    public HexValue(char v) => this = new HexValue(v.ToString());
    public HexValue(string v) 
    {
        if (int.TryParse(v, out var number) &&
            number >= 0 &&
            number <= 9) value = number;
        else if (Enum.TryParse<HexCodes>(v, out var code)) value = (int)code;
        else value = 0;
    }

    public string GetValue()
    {
        if (value <= 9) return value.ToString();
        else return ((HexCodes)value).ToString();
    }

    private enum HexCodes
    {
        A = 10,
        B = 11,
        C = 12,
        D = 13,
        E = 14,
        F = 15,
        G = 16,
        H = 17,
        J = 18, // Skipping I
        K = 19,
        L = 20,
        M = 21,
        N = 22,
        P = 23, // Skipping O
        Q = 24,
        R = 25,
        S = 26,
        T = 27,
        U = 28,
        V = 29,
        W = 30,
        X = 31,
        Y = 32,
        Z = 33,
    }
}
