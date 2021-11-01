namespace TravellerCore.Features;
public class SubSector : IWorldHolder
{
    public Metadata Metadata { get; init; }
    public List<World> Worlds { get; init; }
    public Position Position { get; init; }
}
