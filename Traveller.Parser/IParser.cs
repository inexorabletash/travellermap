namespace Traveller.Parser;
public interface IParser
{
    public bool CanParse(string extension);

    public bool TryParseSector(string inputSector, string? inputMetadata, out Sector result);
}
