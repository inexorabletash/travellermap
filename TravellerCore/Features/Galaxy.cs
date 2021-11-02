namespace TravellerCore.Features;
public class Galaxy : IWorldHolder
{
    public Metadata Metadata { get; init; }

    [JsonIgnore]
    public List<World> Worlds { get; init; }

    public Position Position { get; init; }

    public List<Sector> Sectors { get; init; }
}
