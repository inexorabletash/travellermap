namespace Traveller.Core.Features;
public interface IWorldHolder
{
    public Metadata Metadata { get; init; }
    public List<World> Worlds { get; init; }
    public Position Position { get; init; }
}
