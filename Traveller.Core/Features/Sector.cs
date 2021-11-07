namespace Traveller.Core.Features;
public class Sector : IWorldHolder
{
    public Metadata Metadata {  get; init; }
    public List<World> Worlds {  get; init; }
    public Position Position {  get; init; }
    public List<Quadrant> Quadrants { get; init; }
    public List<SubSector> SubSectors
    {
        get
        {
            var subSectors = new List<SubSector>();
            foreach (var quadrant in Quadrants) subSectors.AddRange(quadrant.SubSectors);
            return subSectors;
        }
    }
}
