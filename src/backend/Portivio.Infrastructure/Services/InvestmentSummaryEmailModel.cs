namespace Portivio.Infrastructure.Services;

public class InvestmentSummaryEmailModel
{
    public string UserName { get; set; } = string.Empty;
    public string RegisteredEmail { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }

    public int ProfileCount { get; set; }
    public int HoldingCount { get; set; }
    public int TransactionCount { get; set; }
    public int ActiveSipCount { get; set; }

    public decimal TotalInvestment { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal ReturnPercentage { get; set; }

    public string DashboardLink { get; set; } = string.Empty;
    public string ManagePreferencesLink { get; set; } = string.Empty;

    public bool IsEmptyAccount { get; set; }
}

