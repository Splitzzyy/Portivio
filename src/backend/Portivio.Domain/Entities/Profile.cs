namespace Portivio.Domain.Entities
{
    public class Profile
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = null!;
        public string BaseCurrency { get; set; } = null!;
        public string Description { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public User User { get; set; } = null!;
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<Holding> Holdings { get; set; } = new List<Holding>();
        public ICollection<SIPPlan> SIPPlans { get; set; } = new List<SIPPlan>();
        public ICollection<PortfolioPerformance> PortfolioPerformances { get; set; } = new List<PortfolioPerformance>();
    }
}
