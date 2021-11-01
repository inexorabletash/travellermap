namespace TravellerCore.Features;
public class Sector : IWorldHolder
{
    public Metadata Metadata {  get; init; }
    public List<World> Worlds {  get; init; }
    public Position Position {  get; init; }
    public List<Quadrant> Quadrants { get; init; }
}
