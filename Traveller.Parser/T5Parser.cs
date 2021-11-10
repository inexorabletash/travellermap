namespace Traveller.Parser;
public class T5Parser : IParser
{
    public const string SupportedExtension = "tab";
    public bool CanParse(string extension) => SupportedExtension.Equals(extension.Trim().TrimStart('.'));

    public bool TryParseSector(string inputSector, string? inputMetadata, out Sector result)
    {
        throw new System.NotImplementedException();
        /* Example header and first line, NOT AUTHORATIVE!
         * Fields may appear in ANY ORDER, though consistent on a file to file basis.
         * Sector	SS	Hex     Name	UWP	        Bases	Remarks	    Zone	PBG	    Allegiance	Stars	    {Ix}	(Ex)	[Cx]	Nobility	W	RU
         * Troj	    A	0103	Taltern	E530240-6	N	    De Lo Po	A	    202	    NaHu	    M2 V M2 V	{ -3 }	(410-5)	[1111]	            7   0
        */
        var parts = inputSector.Split('\n'); // Note both LF and CR+LF are valid line endings. This might not catch both types.
        var isTabDelimited = IsTabDelimited(parts[0]);

        result = new Sector();
        var worldPartList = new List<Dictionary<Field, string>>();

        if (isTabDelimited)
        {
            var headers = ParseTabHeader(parts[0]);


            for (var i = 1; i < parts.Length; i++)
            {
                var lineParts = parts[i].Split('\t');
                var worldParts = new Dictionary<Field, string>();
                for (var j = 0; j < headers.Count; j++)
                {
                    worldParts[headers[j]] = lineParts[j];
                }
                worldPartList.Add(worldParts);
            }
        }
        else
        {
            // Handle column delimited worlds.
        }

        foreach (var world in worldPartList)
        {
            // Add worlds to sector.
            result.AddWorld(ParseLine(world));
        }

        // Todo: Parse Metadata.
    }

    private static bool IsTabDelimited(string header)
    {
        if ((header.Contains(' ') && header.Contains('\t')) ||
            (!header.Contains(' ') && !header.Contains('\t'))) throw new ArgumentException("A T5 format must be either tab or column delimited.");
        return header.Contains('\t');
    }

    private static List<Field> ParseTabHeader(string header)
    {
        var parts = header.Split('\t');
        var fields = new List<Field>();
        foreach (var part in parts)
        {
            if (Enum.TryParse<Field>(part, out var field)) fields.Add(field);
            else throw new ArgumentException($"Unrecognized Field: {part}");
        }

        return fields;
    }
    private static List<(Field, int)> ParseColumnHeader(string header, string headerDashes)
    {
        /* Example
            Hex  Name                 UWP       Remarks                   {Ix}   (Ex)    [Cx]   N    B  Z PBG W  A    Stellar       
            ---- -------------------- --------- ------------------------- ------ ------- ------ ---- -- - --- -- ---- --------------
            0101 Tikal                E767213-A Ga                        { 0 }  (000-0) [0000]         - 000 1                     
         */

        var nameParts = header.Split();
        var sizeParts = headerDashes.Split();
        var fields = new List<(Field, int)>();
        
        for (int i = 0; i < nameParts.Length; i++)
        {
            if (Enum.TryParse<Field>(nameParts[i], out var field)) fields.Add((field, sizeParts[i].Length));
            else throw new ArgumentException($"Unrecognized Field: {nameParts[i]}");
        }

        return fields;
    }

    private static World ParseLine(Dictionary<Field, string> parts)
    {
        var world = new World()
        {
            Name = parts[Field.Name],
            Uwp = new UWP(parts[Field.UWP]),
            Position = new Position(parts[Field.Hex]),
            GasGiants = 0,
            TravelCode = parts[Field.Zone] == " " ? TravelCode.G : Enum.Parse<TravelCode>(parts[Field.Zone]),
        };

        return world;
    }

    private enum Field
    {
        Sector,
        SS,
        Hex,
        Name,
        UWP,
        Bases,
        Remarks,
        Zone,
        PBG,
        Allegiance,
        Stars,
        Ix,
        Ex,
        Cx,
        Nobility,
        W,
        RU,
    }
}
