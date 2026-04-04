namespace Portivio.Domain.Entities
{
    public class Instrument
    {
        public Guid Id { get; set; }

        public Guid AssetTypeId { get; set; }

        public string Name { get; set; } = null!;

        public string Symbol { get; set; } = null!;

        public string Currency { get; set; } = null!;

        public AssetType AssetType { get; set; } = null!;

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

        public ICollection<Holding> Holdings { get; set; } = new List<Holding>();

        public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

        public ICollection<SIPPlan> SIPPlans { get; set; } = new List<SIPPlan>();
    }
}
