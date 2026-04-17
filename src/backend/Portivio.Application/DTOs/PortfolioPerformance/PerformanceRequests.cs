namespace Portivio.Application.DTOs.PortfolioPerformance
{
    public class RecordSnapshotRequest
    {
        public DateTime? Date { get; set; }
    }

    public class PerformanceResponse
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
    }

    public class PerformanceHistoryResponse
    {
        public List<PerformanceResponse> History { get; set; } = new();
        public PerformanceResponse? Latest { get; set; }
    }
}
