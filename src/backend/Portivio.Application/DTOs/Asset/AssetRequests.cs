namespace Portivio.Application.DTOs.Asset
{
    public class AddMutualFundRequest
    {
        public string SchemeName { get; set; } = string.Empty;
        public string SchemeCode { get; set; } = string.Empty;
        public string? Isin { get; set; }
        public string? Plan { get; set; }
        public string? Option { get; set; }
        public decimal Units { get; set; }
        public decimal NavPerUnit { get; set; }
        public DateTime Date { get; set; }
        public string? Notes { get; set; }
    }

    public class AddFixedDepositRequest
    {
        public string Bank { get; set; } = string.Empty;
        public string AccountNo { get; set; } = string.Empty;
        public decimal Principal { get; set; }
        public decimal RatePercent { get; set; }
        public string Compounding { get; set; } = "Quarterly";
        public string PayoutFrequency { get; set; } = "OnMaturity";
        public DateTime StartDate { get; set; }
        public DateTime MaturityDate { get; set; }
        public decimal PrematurePenaltyPct { get; set; }
        public string? Notes { get; set; }
    }

    public class AddRecurringDepositRequest
    {
        public string Bank { get; set; } = string.Empty;
        public string AccountNo { get; set; } = string.Empty;
        public decimal MonthlyAmount { get; set; }
        public decimal RatePercent { get; set; }
        public DateTime StartDate { get; set; }
        public int TenureMonths { get; set; }
        public string? Notes { get; set; }
    }

    public class AddPpfRequest
    {
        public string AccountNo { get; set; } = string.Empty;
        public DateTime OpenedOn { get; set; }
        public decimal CurrentRatePercent { get; set; }
        public decimal InitialContribution { get; set; }
        public DateTime ContributionDate { get; set; }
        public string? Notes { get; set; }
    }

    public class AddPpfContributionRequest
    {
        public Guid InstrumentId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string? Notes { get; set; }
    }

    public class AddGoldRequest
    {
        public string Form { get; set; } = "Coin";
        public string Purity { get; set; } = "24K";
        public decimal WeightGrams { get; set; }
        public decimal RatePerGram { get; set; }
        public decimal MakingChargesInr { get; set; }
        public DateTime Date { get; set; }
        public string? Notes { get; set; }
    }

    public class AssetIngestResponse
    {
        public Guid InstrumentId { get; set; }
        public string InstrumentName { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public Guid TransactionId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
