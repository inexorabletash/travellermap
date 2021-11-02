using System.Text.Json;

namespace TravellerParser;
public class JsonParser: IParser
{
    public const string JsonExtension = "json";
    public bool CanParse(string extension) => JsonExtension.Equals(extension.Trim().TrimStart('.'));

    public bool TryParseSector(string inputSector, string? inputMetadata, out Sector result)
    {
        result = JsonSerializer.Deserialize<Sector>(inputSector);
        return false;
    }
}
