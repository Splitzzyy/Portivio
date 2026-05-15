using Portivio.Infrastructure.Services;
using System.Globalization;
using Xunit;

namespace Portivio.Tests.Services;

public class EmailTemplatesTests
{
    [Fact]
    public void InvestmentSummaryEmail_IncludesTotalsLinksAndIdentity()
    {
        var culture = new CultureInfo("en-IN");
        var model = new InvestmentSummaryEmailModel
        {
            UserName = "Asha",
            RegisteredEmail = "asha@example.com",
            GeneratedAtUtc = new DateTime(2026, 05, 15, 10, 30, 0, DateTimeKind.Utc),
            ProfileCount = 2,
            HoldingCount = 3,
            TransactionCount = 4,
            ActiveSipCount = 1,
            TotalInvestment = 123456.78m,
            MarketValue = 130000m,
            UnrealizedPnL = 6543.22m,
            ReturnPercentage = 5.30m,
            DashboardLink = "https://app.portivio.app/home",
            ManagePreferencesLink = "https://app.portivio.app/home/my-profile"
        };

        var (subject, html, text) = EmailTemplates.InvestmentSummaryEmail(model, culture);

        Assert.Contains("Investment Summary", subject);

        Assert.Contains("Asha", html);
        Assert.Contains("asha@example.com", html);
        Assert.Contains(model.DashboardLink, html);
        Assert.Contains(model.ManagePreferencesLink, html);
        Assert.Contains(model.TotalInvestment.ToString("C", culture), html);

        Assert.Contains("Asha", text);
        Assert.Contains("asha@example.com", text);
        Assert.Contains(model.DashboardLink, text);
        Assert.Contains(model.ManagePreferencesLink, text);
        Assert.Contains(model.TotalInvestment.ToString("C", culture), text);
    }

    [Fact]
    public void InvestmentSummaryEmail_EmptyAccount_IncludesEmptyCopy()
    {
        var culture = new CultureInfo("en-IN");
        var model = new InvestmentSummaryEmailModel
        {
            UserName = "Test User",
            RegisteredEmail = "test@example.com",
            GeneratedAtUtc = new DateTime(2026, 05, 15, 10, 30, 0, DateTimeKind.Utc),
            ProfileCount = 0,
            HoldingCount = 0,
            TransactionCount = 0,
            ActiveSipCount = 0,
            TotalInvestment = 0m,
            MarketValue = 0m,
            UnrealizedPnL = 0m,
            ReturnPercentage = 0m,
            DashboardLink = "https://app.portivio.app/home",
            ManagePreferencesLink = "https://app.portivio.app/home/my-profile",
            IsEmptyAccount = true
        };

        var (_, html, text) = EmailTemplates.InvestmentSummaryEmail(model, culture);

        Assert.Contains("No investment data yet", html);
        Assert.Contains("No investment data yet", text);
    }
}

