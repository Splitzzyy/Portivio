namespace Portivio.Domain.Entities
{
    public class PortfolioPerformance
    {
        public Guid Id { get; set; }
        public Guid ProfileId { get; set; }
        public DateTime Date { get; set; }
        public decimal TotalInvestment { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal DayChange { get; set; }
        public decimal TotalReturn { get; set; }
        public decimal XIRR { get; set; }
        public DateTime CreatedAt { get; set; }
        public Profile Profile { get; set; } = null!;
    }
}