namespace TravellerCore.Features;
public class Quadrant : IWorldHolder
{
    public Metadata Metadata { get; init; }

    [JsonIgnore]
    public List<World> Worlds { get; init; }

    public Position Position { get; init; }

    public List<SubSector> SubSectors {  get; init; }
}
