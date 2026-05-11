using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Portivio.API.Testing;
using Portivio.Application.DTOs.Asset;
using Portivio.Application.Results;
using Portivio.Application.Services;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Portivio.Tests.Controllers;

public sealed class AssetControllerTests : IClassFixture<AssetControllerTests.AssetApiFactory>
{
    private readonly AssetApiFactory _factory;

    public AssetControllerTests(AssetApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Put_stock_route_binds_and_returns_ok()
    {
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();

        var req = new UpdateStockRequest
        {
            Name = "TCS",
            Symbol = "TCS",
            Exchange = "NSE",
            Quantity = 1m,
            Price = 100m,
            Date = DateTime.UtcNow
        };

        var res = new AssetIngestResponse
        {
            InstrumentId = instrumentId,
            InstrumentName = "TCS",
            Symbol = "NSE:TCS",
            TransactionId = Guid.NewGuid(),
            Message = "updated"
        };

        _factory.Assets.Reset();
        _factory.Assets
            .Setup(s => s.UpdateStockAsync(
                userId,
                profileId,
                instrumentId,
                It.Is<UpdateStockRequest>(r => r.Name == req.Name && r.Symbol == req.Symbol && r.Exchange == req.Exchange),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AssetIngestResponse>.Success(res));

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());

        var http = await client.PutAsJsonAsync($"/api/profiles/{profileId}/assets/stock/{instrumentId}", req);
        Assert.Equal(HttpStatusCode.OK, http.StatusCode);

        var body = await http.Content.ReadFromJsonAsync<AssetIngestResponse>();
        Assert.NotNull(body);
        Assert.Equal(res.InstrumentId, body!.InstrumentId);
        Assert.Equal(res.TransactionId, body.TransactionId);

        _factory.Assets.VerifyAll();
    }

    [Fact]
    public async Task Put_mutual_fund_route_binds_and_returns_ok()
    {
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();

        var req = new UpdateMutualFundRequest
        {
            SchemeName = "PPFAS",
            SchemeCode = "120503",
            Units = 1m,
            NavPerUnit = 10m,
            Date = DateTime.UtcNow
        };

        var res = new AssetIngestResponse
        {
            InstrumentId = instrumentId,
            InstrumentName = "PPFAS",
            Symbol = "120503",
            TransactionId = Guid.NewGuid(),
            Message = "updated"
        };

        _factory.Assets.Reset();
        _factory.Assets
            .Setup(s => s.UpdateMutualFundAsync(
                userId,
                profileId,
                instrumentId,
                It.Is<UpdateMutualFundRequest>(r => r.SchemeName == req.SchemeName && r.SchemeCode == req.SchemeCode),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AssetIngestResponse>.Success(res));

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());

        var http = await client.PutAsJsonAsync($"/api/profiles/{profileId}/assets/mutual-fund/{instrumentId}", req);
        Assert.Equal(HttpStatusCode.OK, http.StatusCode);

        var body = await http.Content.ReadFromJsonAsync<AssetIngestResponse>();
        Assert.NotNull(body);
        Assert.Equal(res.InstrumentId, body!.InstrumentId);

        _factory.Assets.VerifyAll();
    }

    [Fact]
    public async Task Put_gold_route_binds_and_returns_ok()
    {
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();

        var req = new UpdateGoldRequest
        {
            Form = "Digital",
            Purity = "24K",
            WeightGrams = 1m,
            RatePerGram = 7000m,
            Date = DateTime.UtcNow
        };

        var res = new AssetIngestResponse
        {
            InstrumentId = instrumentId,
            InstrumentName = "Gold",
            Symbol = "24K",
            TransactionId = Guid.NewGuid(),
            Message = "updated"
        };

        _factory.Assets.Reset();
        _factory.Assets
            .Setup(s => s.UpdateGoldAsync(
                userId,
                profileId,
                instrumentId,
                It.Is<UpdateGoldRequest>(r => r.Form == req.Form && r.Purity == req.Purity),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AssetIngestResponse>.Success(res));

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());

        var http = await client.PutAsJsonAsync($"/api/profiles/{profileId}/assets/gold/{instrumentId}", req);
        Assert.Equal(HttpStatusCode.OK, http.StatusCode);

        var body = await http.Content.ReadFromJsonAsync<AssetIngestResponse>();
        Assert.NotNull(body);
        Assert.Equal(res.InstrumentId, body!.InstrumentId);

        _factory.Assets.VerifyAll();
    }

    [Fact]
    public async Task Put_ppf_route_binds_and_returns_ok()
    {
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();

        var req = new UpdatePpfRequest
        {
            AccountNo = "PPF-123",
            OpenedOn = DateTime.UtcNow.Date.AddYears(-1),
            CurrentRatePercent = 7.1m,
            InitialContribution = 1000m,
            ContributionDate = DateTime.UtcNow
        };

        var res = new AssetIngestResponse
        {
            InstrumentId = instrumentId,
            InstrumentName = "PPF",
            Symbol = "PPF",
            TransactionId = Guid.NewGuid(),
            Message = "updated"
        };

        _factory.Assets.Reset();
        _factory.Assets
            .Setup(s => s.UpdatePpfAsync(
                userId,
                profileId,
                instrumentId,
                It.Is<UpdatePpfRequest>(r => r.AccountNo == req.AccountNo && r.InitialContribution == req.InitialContribution),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AssetIngestResponse>.Success(res));

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());

        var http = await client.PutAsJsonAsync($"/api/profiles/{profileId}/assets/ppf/{instrumentId}", req);
        Assert.Equal(HttpStatusCode.OK, http.StatusCode);

        var body = await http.Content.ReadFromJsonAsync<AssetIngestResponse>();
        Assert.NotNull(body);
        Assert.Equal(res.InstrumentId, body!.InstrumentId);

        _factory.Assets.VerifyAll();
    }

    [Fact]
    public async Task Put_fixed_deposit_route_binds_and_returns_ok()
    {
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();

        var req = new UpdateFixedDepositRequest
        {
            Bank = "HDFC",
            Principal = 10000m,
            RatePercent = 7.5m,
            StartDate = DateTime.UtcNow.Date,
            MaturityDate = DateTime.UtcNow.Date.AddYears(1),
            PrematurePenaltyPct = 0m
        };

        var res = new AssetIngestResponse
        {
            InstrumentId = instrumentId,
            InstrumentName = "FD",
            Symbol = "FD",
            TransactionId = Guid.NewGuid(),
            Message = "updated"
        };

        _factory.Assets.Reset();
        _factory.Assets
            .Setup(s => s.UpdateFixedDepositAsync(
                userId,
                profileId,
                instrumentId,
                It.Is<UpdateFixedDepositRequest>(r => r.Bank == req.Bank && r.Principal == req.Principal),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AssetIngestResponse>.Success(res));

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());

        var http = await client.PutAsJsonAsync($"/api/profiles/{profileId}/assets/fixed-deposit/{instrumentId}", req);
        Assert.Equal(HttpStatusCode.OK, http.StatusCode);

        var body = await http.Content.ReadFromJsonAsync<AssetIngestResponse>();
        Assert.NotNull(body);
        Assert.Equal(res.InstrumentId, body!.InstrumentId);

        _factory.Assets.VerifyAll();
    }

    [Fact]
    public async Task Put_recurring_deposit_route_binds_and_returns_ok()
    {
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();

        var req = new UpdateRecurringDepositRequest
        {
            Bank = "ICICI",
            MonthlyAmount = 2000m,
            RatePercent = 7.2m,
            StartDate = DateTime.UtcNow.Date,
            TenureMonths = 12
        };

        var res = new AssetIngestResponse
        {
            InstrumentId = instrumentId,
            InstrumentName = "RD",
            Symbol = "RD",
            TransactionId = Guid.NewGuid(),
            Message = "updated"
        };

        _factory.Assets.Reset();
        _factory.Assets
            .Setup(s => s.UpdateRecurringDepositAsync(
                userId,
                profileId,
                instrumentId,
                It.Is<UpdateRecurringDepositRequest>(r => r.Bank == req.Bank && r.MonthlyAmount == req.MonthlyAmount),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AssetIngestResponse>.Success(res));

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());

        var http = await client.PutAsJsonAsync($"/api/profiles/{profileId}/assets/recurring-deposit/{instrumentId}", req);
        Assert.Equal(HttpStatusCode.OK, http.StatusCode);

        var body = await http.Content.ReadFromJsonAsync<AssetIngestResponse>();
        Assert.NotNull(body);
        Assert.Equal(res.InstrumentId, body!.InstrumentId);

        _factory.Assets.VerifyAll();
    }

    public sealed class AssetApiFactory : WebApplicationFactory<Program>
    {
        public Mock<IAssetInstrumentService> Assets { get; } = new(MockBehavior.Strict);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAssetInstrumentService>();
                services.AddSingleton<IAssetInstrumentService>(_ => Assets.Object);
            });
        }
    }
}
