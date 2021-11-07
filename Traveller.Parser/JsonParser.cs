using System.Text.Json;

namespace Traveller.Parser;
public class JsonParser: IParser
{
    public const string JsonExtension = "json";
    public bool CanParse(string extension) => JsonExtension.Equals(extension.Trim().TrimStart('.'));

    public bool TryParseSector(string inputSector, string? inputMetadata, out Sector result)
    {
        result = JsonSerializer.Deserialize<Sector>(inputSector);

        foreach  (var world in result.Worlds)
        {
            foreach (var quadrant in result.Quadrants)
                if (world.Position.IsInQuadrant(quadrant.Position))
                    quadrant.Worlds.Add(world);

            foreach (var subSector in result.Quadrants)
                if (world.Position.IsInQuadrant(subSector.Position))
                    subSector.Worlds.Add(world);
        }

        return false;
    }
}
