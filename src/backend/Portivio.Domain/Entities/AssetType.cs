namespace Portivio.Domain.Entities
{
    public class AssetType
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public ICollection<Instrument> Instruments { get; set; } = new List<Instrument>();
    }
}