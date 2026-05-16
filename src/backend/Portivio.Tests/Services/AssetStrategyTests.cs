using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portivio.Application.Services;
using Portivio.Application.Results;
using Portivio.Application.Services.Authorization;
using Portivio.Application.Services.MarketData;
using Portivio.Application.Services.Strategies;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using System.Text.Json;
using Xunit;

namespace Portivio.Tests.Services
{
    public class AssetStrategyTests
    {
        private static PortivioDbContext CreateContext() =>
            new(new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        private sealed class NoopThrottle : IRefreshThrottle
        {
            public Task DelayAsync(TimeSpan delay, CancellationToken ct) => Task.CompletedTask;
        }

        private static async Task<(User user, Profile profile, AssetType assetType)> SeedBaseAsync(PortivioDbContext ctx)
        {
            var user = new User { Id = Guid.NewGuid(), Email = $"u-{Guid.NewGuid()}@t.com", Name = "U", PasswordHash = "h", IsVerified = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            var profile = new Profile { Id = Guid.NewGuid(), UserId = user.Id, Name = "P", BaseCurrency = "INR", Description = "", CreatedAt = DateTime.UtcNow };
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Test" };
            ctx.Users.Add(user);
            ctx.Profiles.Add(profile);
            ctx.AssetTypes.Add(assetType);
            await ctx.SaveChangesAsync();
            return (user, profile, assetType);
        }

        private static async Task<HoldingSnapshot> ComputeHoldingHelperAsync(IAssetStrategy strategy, PortivioDbContext ctx, Guid profileId, Guid instrumentId, DateTime asOf)
        {
            var holding = await ctx.Holdings.Include(h => h.Instrument)
                .FirstOrDefaultAsync(h => h.ProfileId == profileId && h.InstrumentId == instrumentId);
            
            if (holding == null)
            {
                var inst = await ctx.Instruments.FindAsync(instrumentId);
                holding = new Holding { ProfileId = profileId, InstrumentId = instrumentId, Instrument = inst! };
            }

            var txs = await ctx.Transactions
                .Where(t => t.ProfileId == profileId && t.InstrumentId == instrumentId)
                .ToListAsync();

            var latestPrice = await ctx.PriceHistories
                .Where(ph => ph.InstrumentId == instrumentId && ph.Date <= asOf)
                .OrderByDescending(ph => ph.Date)
                .Select(ph => (decimal?)ph.Price)
                .FirstOrDefaultAsync();

            return await strategy.ComputeHoldingAsync(holding, asOf, txs, latestPrice, default);
        }

        // ── MutualFundStrategy ──────────────────────────────────────────────────

        [Fact]
        public async Task MutualFund_Buy_ComputesHolding()
        {
            using var ctx = CreateContext();
            var (_, profile, assetType) = await SeedBaseAsync(ctx);
            var inst = new Instrument { Id = Guid.NewGuid(), AssetTypeId = assetType.Id, Category = AssetCategory.MutualFund, Name = "Axis MF", Symbol = "AXISMF", Currency = "INR" };
            ctx.Instruments.Add(inst);
            ctx.Transactions.Add(new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = inst.Id, Type = TransactionType.Buy, Quantity = 100m, Price = 50m, Amount = 5000m, TransactionDate = DateTime.UtcNow.AddDays(-10), Notes = "" });
            await ctx.SaveChangesAsync();

            var strategy = new MutualFundStrategy(ctx);
            var snapshot = await ComputeHoldingHelperAsync(strategy, ctx, profile.Id, inst.Id, DateTime.UtcNow);

            Assert.Equal(100m, snapshot.Quantity);
            Assert.Equal(50m, snapshot.AvgPrice);
            Assert.Equal(50m, snapshot.CurrentPrice); // fallback to last buy price
        }

        [Fact]
        public void MutualFund_ValidateTransaction_InvalidType_Fails()
        {
            var strategy = new MutualFundStrategy(null!);
            var tx = new Transaction { Type = TransactionType.Deposit, Quantity = 1m, Price = 10m, Amount = 10m };
            var result = strategy.ValidateTransaction(tx, null!);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void MutualFund_ValidateTransaction_BonusUnits_Passes()
        {
            var strategy = new MutualFundStrategy(null!);
            var tx = new Transaction { Type = TransactionType.BonusUnits, Quantity = 10m, Price = 0m, Amount = 0m };
            var result = strategy.ValidateTransaction(tx, null!);
            Assert.True(result.IsSuccess);
        }

        // ── FixedDepositStrategy ────────────────────────────────────────────────

        [Fact]
        public async Task FixedDeposit_Deposit_ComputesAccruedInterest()
        {
            using var ctx = CreateContext();
            var (_, profile, assetType) = await SeedBaseAsync(ctx);
            var meta = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                principal = 100000m,
                rate = 7.0m,
                compounding = "Quarterly",
                startDate = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd"),
                maturityDate = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd")
            }));
            var inst = new Instrument { Id = Guid.NewGuid(), AssetTypeId = assetType.Id, Category = AssetCategory.FixedDeposit, Name = "HDFC FD", Symbol = "FD:HDFC:001", Currency = "INR", Metadata = meta };
            ctx.Instruments.Add(inst);
            ctx.Transactions.Add(new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = inst.Id, Type = TransactionType.Deposit, Quantity = 1m, Price = 100000m, Amount = 100000m, TransactionDate = DateTime.UtcNow.AddYears(-1), Notes = "" });
            await ctx.SaveChangesAsync();

            var strategy = new FixedDepositStrategy(ctx);
            var snapshot = await ComputeHoldingHelperAsync(strategy, ctx, profile.Id, inst.Id, DateTime.UtcNow);

            Assert.Equal(1m, snapshot.Quantity);
            Assert.True(snapshot.AccruedInterest > 0m, "Should have accrued interest after 1 year");
            Assert.Equal(snapshot.AccruedInterest, snapshot.UnrealizedPnL);
        }

        [Fact]
        public void FixedDeposit_ValidateTransaction_Deposit_Passes()
        {
            var strategy = new FixedDepositStrategy(null!);
            var tx = new Transaction { Type = TransactionType.Deposit, Amount = 50000m, Quantity = 1m, Price = 50000m };
            Assert.True(strategy.ValidateTransaction(tx, null!).IsSuccess);
        }

        [Fact]
        public void FixedDeposit_ValidateTransaction_Buy_Fails()
        {
            var strategy = new FixedDepositStrategy(null!);
            var tx = new Transaction { Type = TransactionType.Buy, Quantity = 1m, Price = 50000m, Amount = 50000m };
            Assert.False(strategy.ValidateTransaction(tx, null!).IsSuccess);
        }

        // ── RecurringDepositStrategy ────────────────────────────────────────────

        [Fact]
        public async Task RecurringDeposit_Contribution_ComputesHolding()
        {
            using var ctx = CreateContext();
            var (_, profile, assetType) = await SeedBaseAsync(ctx);
            var meta = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                bank = "SBI",
                accountNo = "RD001",
                monthly = 5000m,
                rate = 6.5m,
                startDate = DateTime.UtcNow.AddMonths(-3).ToString("yyyy-MM-dd"),
                tenureMonths = 12
            }));
            var inst = new Instrument { Id = Guid.NewGuid(), AssetTypeId = assetType.Id, Category = AssetCategory.RecurringDeposit, Name = "SBI RD", Symbol = "RD:SBI:RD001", Currency = "INR", Metadata = meta };
            ctx.Instruments.Add(inst);
            for (int i = 0; i < 3; i++)
            {
                ctx.Transactions.Add(new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = inst.Id, Type = TransactionType.Contribution, Quantity = 1m, Price = 5000m, Amount = 5000m, TransactionDate = DateTime.UtcNow.AddMonths(-i), Notes = "" });
            }
            await ctx.SaveChangesAsync();

            var strategy = new RecurringDepositStrategy(ctx);
            var snapshot = await ComputeHoldingHelperAsync(strategy, ctx, profile.Id, inst.Id, DateTime.UtcNow);

            Assert.Equal(1m, snapshot.Quantity);
            Assert.Equal(15000m, snapshot.AvgPrice); // 3 × 5000
            Assert.True(snapshot.AccruedInterest >= 0m);
        }

        [Fact]
        public void RecurringDeposit_ValidateTransaction_Buy_Fails()
        {
            var strategy = new RecurringDepositStrategy(null!);
            var tx = new Transaction { Type = TransactionType.Buy, Quantity = 1m, Price = 5000m, Amount = 5000m };
            Assert.False(strategy.ValidateTransaction(tx, null!).IsSuccess);
        }

        // ── PpfStrategy ─────────────────────────────────────────────────────────

        [Fact]
        public async Task Ppf_Contribution_ComputesAccruedInterest()
        {
            using var ctx = CreateContext();
            var (_, profile, assetType) = await SeedBaseAsync(ctx);
            var meta = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                accountNo = "PPF001",
                openedOn = DateTime.UtcNow.AddYears(-2).ToString("yyyy-MM-dd"),
                lockInEndsOn = DateTime.UtcNow.AddYears(13).ToString("yyyy-MM-dd"),
                currentRate = 7.1m
            }));
            var inst = new Instrument { Id = Guid.NewGuid(), AssetTypeId = assetType.Id, Category = AssetCategory.Ppf, Name = "PPF - PPF001", Symbol = "PPF:PPF001", Currency = "INR", Metadata = meta };
            ctx.Instruments.Add(inst);
            ctx.Transactions.Add(new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = inst.Id, Type = TransactionType.Contribution, Quantity = 1m, Price = 150000m, Amount = 150000m, TransactionDate = DateTime.UtcNow.AddYears(-2), Notes = "" });
            await ctx.SaveChangesAsync();

            var strategy = new PpfStrategy(ctx);
            var snapshot = await ComputeHoldingHelperAsync(strategy, ctx, profile.Id, inst.Id, DateTime.UtcNow);

            Assert.Equal(1m, snapshot.Quantity);
            Assert.True(snapshot.AccruedInterest > 0m, "Should have accrued interest after 2 years at 7.1%");
        }

        [Fact]
        public void Ppf_ValidateTransaction_Buy_Fails()
        {
            var strategy = new PpfStrategy(null!);
            var tx = new Transaction { Type = TransactionType.Buy, Quantity = 1m, Price = 10000m, Amount = 10000m };
            Assert.False(strategy.ValidateTransaction(tx, null!).IsSuccess);
        }

        // ── GoldStrategy ────────────────────────────────────────────────────────

        [Fact]
        public async Task Gold_Buy_ComputesHolding()
        {
            using var ctx = CreateContext();
            var (_, profile, assetType) = await SeedBaseAsync(ctx);
            var inst = new Instrument { Id = Guid.NewGuid(), AssetTypeId = assetType.Id, Category = AssetCategory.Gold, Name = "Gold 24K Coin", Symbol = "GOLD:24K:COIN", Currency = "INR" };
            ctx.Instruments.Add(inst);
            ctx.Transactions.Add(new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = inst.Id, Type = TransactionType.Buy, Quantity = 10m, Price = 6000m, Amount = 60000m, TransactionDate = DateTime.UtcNow.AddMonths(-1), Notes = "" });
            await ctx.SaveChangesAsync();

            var strategy = new GoldStrategy(ctx);
            var snapshot = await ComputeHoldingHelperAsync(strategy, ctx, profile.Id, inst.Id, DateTime.UtcNow);

            Assert.Equal(10m, snapshot.Quantity);
            Assert.Equal(6000m, snapshot.AvgPrice);
            Assert.Equal(60000m, snapshot.MarketValue);
        }

        [Fact]
        public void Gold_ValidateTransaction_Deposit_Fails()
        {
            var strategy = new GoldStrategy(null!);
            var tx = new Transaction { Type = TransactionType.Deposit, Amount = 5000m, Quantity = 0m, Price = 0m };
            Assert.False(strategy.ValidateTransaction(tx, null!).IsSuccess);
        }

        // ── AssetStrategyResolver ───────────────────────────────────────────────

        [Fact]
        public void Resolver_UnknownCategory_Throws()
        {
            var resolver = new AssetStrategyResolver(new IAssetStrategy[] { new EquityStrategy(null!) });
            Assert.Throws<NotSupportedException>(() => resolver.For(AssetCategory.MutualFund));
        }

        [Fact]
        public void Resolver_AllCategories_Resolvable()
        {
            using var ctx = CreateContext();
            var strategies = new IAssetStrategy[]
            {
                new EquityStrategy(ctx),
                new MutualFundStrategy(ctx),
                new FixedDepositStrategy(ctx),
                new RecurringDepositStrategy(ctx),
                new PpfStrategy(ctx),
                new GoldStrategy(ctx)
            };
            var resolver = new AssetStrategyResolver(strategies);
            Assert.Equal(AssetCategory.Equity, resolver.For(AssetCategory.Equity).Category);
            Assert.Equal(AssetCategory.MutualFund, resolver.For(AssetCategory.MutualFund).Category);
            Assert.Equal(AssetCategory.FixedDeposit, resolver.For(AssetCategory.FixedDeposit).Category);
            Assert.Equal(AssetCategory.RecurringDeposit, resolver.For(AssetCategory.RecurringDeposit).Category);
            Assert.Equal(AssetCategory.Ppf, resolver.For(AssetCategory.Ppf).Category);
            Assert.Equal(AssetCategory.Gold, resolver.For(AssetCategory.Gold).Category);
        }

        // ── AssetInstrumentService convenience endpoints ─────────────────────────

        [Fact]
        public async Task AddMutualFund_ValidRequest_CreatesInstrumentAndTransaction()
        {
            using var ctx = CreateContext();
            var (user, profile, _) = await SeedBaseAsync(ctx);
            var guard = new ProfileAccessGuard(ctx);
            var equity = new EquityStrategy(ctx);
            var mf = new MutualFundStrategy(ctx);
            var fd = new FixedDepositStrategy(ctx);
            var rd = new RecurringDepositStrategy(ctx);
            var ppf = new PpfStrategy(ctx);
            var gold = new GoldStrategy(ctx);
            var resolver = new AssetStrategyResolver(new IAssetStrategy[] { equity, mf, fd, rd, ppf, gold });
            var ingest = new TransactionIngestService(ctx, guard, resolver);
            var recalc = new Mock<IHoldingRecalculationService>();
            recalc.Setup(r => r.RefreshProfileAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<List<Portivio.Application.DTOs.Holding.HoldingResponse>>.Success(new List<Portivio.Application.DTOs.Holding.HoldingResponse>()));
            var svc = new AssetInstrumentService(ctx, ingest, guard, recalc.Object);

            var req = new Application.DTOs.Asset.AddMutualFundRequest
            {
                SchemeName = "Axis Bluechip Fund",
                SchemeCode = "120503",
                Isin = "INF846K01DP8",
                Plan = "Direct",
                Option = "Growth",
                Units = 50m,
                NavPerUnit = 45.23m,
                Date = DateTime.UtcNow.AddDays(-5)
            };

            var result = await svc.AddMutualFundAsync(user.Id, profile.Id, req);

            Assert.True(result.IsSuccess);
            Assert.Equal(201, result.StatusCode);
            Assert.NotEqual(Guid.Empty, result.Data!.InstrumentId);
            Assert.Equal(1, await ctx.Transactions.CountAsync(t => t.ProfileId == profile.Id));
        }

        [Fact]
        public async Task AddFixedDeposit_ValidRequest_CreatesIdempotentOnReopen()
        {
            using var ctx = CreateContext();
            var (user, profile, _) = await SeedBaseAsync(ctx);
            var guard = new ProfileAccessGuard(ctx);
            var fd = new FixedDepositStrategy(ctx);
            var resolver = new AssetStrategyResolver(new IAssetStrategy[] { fd });
            var ingest = new TransactionIngestService(ctx, guard, resolver);
            var recalc = new Mock<IHoldingRecalculationService>();
            recalc.Setup(r => r.RefreshProfileAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<List<Portivio.Application.DTOs.Holding.HoldingResponse>>.Success(new List<Portivio.Application.DTOs.Holding.HoldingResponse>()));
            var svc = new AssetInstrumentService(ctx, ingest, guard, recalc.Object);

            var req = new Application.DTOs.Asset.AddFixedDepositRequest
            {
                Bank = "HDFC",
                AccountNo = "FD123",
                Principal = 200000m,
                RatePercent = 7.25m,
                Compounding = "Quarterly",
                PayoutFrequency = "OnMaturity",
                StartDate = DateTime.UtcNow.AddDays(-30),
                MaturityDate = DateTime.UtcNow.AddDays(335)
            };

            var first = await svc.AddFixedDepositAsync(user.Id, profile.Id, req);
            var second = await svc.AddFixedDepositAsync(user.Id, profile.Id, req);

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            // Same ClientTxnId → idempotent, same transaction returned
            Assert.Equal(first.Data!.TransactionId, second.Data!.TransactionId);
        }
    }
}
