namespace Portivio.Application.DTOs.Home
{
    public class HomeResponse
    {
        public UserInfoDto User { get; set; } = null!;
        public PortfolioSummaryDto Summary { get; set; } = null!;
        public List<ProfileDto> Profiles { get; set; } = new();
    }

    public class UserInfoDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool IsVerified { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class PortfolioSummaryDto
    {
        public int ProfileCount { get; set; }
        public int HoldingCount { get; set; }
        public int TransactionCount { get; set; }
        public int ActiveSIPCount { get; set; }
        public decimal TotalInvestment { get; set; }
        public decimal TotalMarketValue { get; set; }
        public decimal TotalUnrealizedPnL { get; set; }
    }

    public class ProfileDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string BaseCurrency { get; set; } = null!;
        public string Description { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public List<HoldingDto> Holdings { get; set; } = new();
        public List<TransactionDto> Transactions { get; set; } = new();
        public List<SIPPlanDto> SIPPlans { get; set; } = new();
        public PortfolioPerformanceDto? LatestPerformance { get; set; }
    }

    public class HoldingDto
    {
        public Guid Id { get; set; }
        public Guid InstrumentId { get; set; }
        public string InstrumentName { get; set; } = null!;
        public string InstrumentSymbol { get; set; } = null!;
        public string Currency { get; set; } = null!;
        public string AssetType { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal AvgPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal MarketValue { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class TransactionDto
    {
        public Guid Id { get; set; }
        public Guid InstrumentId { get; set; }
        public string InstrumentName { get; set; } = null!;
        public string InstrumentSymbol { get; set; } = null!;
        public string AssetType { get; set; } = null!;
        public string Type { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Notes { get; set; } = null!;
    }

    public class SIPPlanDto
    {
        public Guid Id { get; set; }
        public Guid InstrumentId { get; set; }
        public string InstrumentSymbol { get; set; } = null!;
        public decimal Amount { get; set; }
        public int SIPDay { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PortfolioPerformanceDto
    {
        public DateTime Date { get; set; }
        public decimal TotalInvestment { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal DayChange { get; set; }
        public decimal TotalReturn { get; set; }
        public decimal XIRR { get; set; }
    }
}
