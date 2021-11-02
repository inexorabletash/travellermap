namespace TravellerCore.Features;
public class SubSector : IWorldHolder
{
    public Metadata Metadata { get; init; }

    [JsonIgnore]
    public List<World> Worlds { get; init; }

    public Position Position { get; init; }
}
