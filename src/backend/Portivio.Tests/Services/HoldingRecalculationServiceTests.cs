using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portivio.Application.DTOs.MarketData;
using Portivio.Application.Results;
using Portivio.Application.Services;
using Portivio.Application.Services.Authorization;
using Portivio.Application.Services.MarketData;
using Portivio.Application.Services.Strategies;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class HoldingRecalculationServiceTests
    {
        private static PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private static ILogger<HoldingRecalculationService> CreateMockLogger()
            => new Mock<ILogger<HoldingRecalculationService>>().Object;

        private sealed class RecordingThrottle : IRefreshThrottle
        {
            public List<TimeSpan> Calls { get; } = new();
            public Task DelayAsync(TimeSpan delay, CancellationToken ct)
            {
                Calls.Add(delay);
                return Task.CompletedTask;
            }
        }

        private static IMarketDataService NoopMarketData()
        {
            var mock = new Mock<IMarketDataService>();
            mock.Setup(m => m.SyncAllNavsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SyncSummaryResponse>.Success(new SyncSummaryResponse()));
            mock.Setup(m => m.SyncStockPriceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<StockPriceResponse>.Success(new StockPriceResponse()));
            return mock.Object;
        }

        private static IGoldRateProvider NoopGoldRate()
        {
            var mock = new Mock<IGoldRateProvider>();
            mock.Setup(m => m.GetRatePerGramAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);
            return mock.Object;
        }

        private static (User user, Profile profile, Instrument instrument) SeedEquityHolding(
            PortivioDbContext context,
            decimal quantity,
            decimal avgPrice,
            decimal? latestPriceHistory,
            string symbol = "TEST",
            DateTime? priceDate = null)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"u-{Guid.NewGuid()}@t.com",
                Name = "U",
                PasswordHash = "h",
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var profile = new Profile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = "P",
                BaseCurrency = "USD",
                Description = "",
                CreatedAt = DateTime.UtcNow
            };
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Equity" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Category = AssetCategory.Equity,
                Name = $"Inst-{symbol}",
                Symbol = symbol,
                Currency = "USD",
                PriceSource = PriceSource.AlphaVantage
            };
            context.Users.Add(user);
            context.Profiles.Add(profile);
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);

            context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = TransactionType.Buy,
                Quantity = quantity,
                Price = avgPrice,
                Amount = quantity * avgPrice,
                TransactionDate = DateTime.UtcNow.AddDays(-10),
                Notes = "",
                Source = TransactionSource.Manual,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-10)
            });

            if (latestPriceHistory.HasValue)
            {
                context.PriceHistories.Add(new PriceHistory
                {
                    Id = Guid.NewGuid(),
                    InstrumentId = instrument.Id,
                    Price = latestPriceHistory.Value,
                    Date = priceDate ?? DateTime.UtcNow,
                    Source = "test",
                    CreatedAt = DateTime.UtcNow
                });
            }

            context.Holdings.Add(new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = quantity,
                AvgPrice = avgPrice,
                CurrentPrice = avgPrice,
                MarketValue = quantity * avgPrice,
                UnrealizedPnL = 0m,
                LastUpdated = DateTime.UtcNow.AddDays(-5)
            });

            context.SaveChanges();
            return (user, profile, instrument);
        }

        private static AssetStrategyResolver BuildResolver(PortivioDbContext context, params IAssetStrategy[] extra)
        {
            var list = new List<IAssetStrategy> { new EquityStrategy(context) };
            list.AddRange(extra);
            return new AssetStrategyResolver(list);
        }

        private static HoldingRecalculationService BuildService(
            PortivioDbContext context,
            IMarketDataService? marketData = null,
            IRefreshThrottle? throttle = null,
            IGoldRateProvider? goldRate = null,
            ILivePriceApiStockProvider? livePrice = null,
            params IAssetStrategy[] extraStrategies) =>
            new(
                context,
                BuildResolver(context, extraStrategies),
                new ProfileAccessGuard(context),
                marketData ?? NoopMarketData(),
                goldRate ?? NoopGoldRate(),
                livePrice ?? Mock.Of<ILivePriceApiStockProvider>(),
                throttle ?? new RecordingThrottle(),
                CreateMockLogger());

        // ---------- RefreshProfileAsync (slice #28) ----------

        [Fact]
        public async Task RefreshProfileAsync_RecomputesHoldings_UsingLatestPriceHistory()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _) = SeedEquityHolding(context, quantity: 10m, avgPrice: 100m, latestPriceHistory: 150m);
            var market = new Mock<IMarketDataService>();
            var service = BuildService(context, market.Object);

            var result = await service.RefreshProfileAsync(user.Id, profile.Id);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Data!);
            var holding = result.Data![0];
            Assert.Equal(150m, holding.CurrentPrice);
            Assert.Equal(1500m, holding.MarketValue);          // 10 * 150
            Assert.Equal(500m, holding.UnrealizedPnL);         // (150 - 100) * 10
            Assert.True(holding.LastUpdated > DateTime.UtcNow.AddDays(-1));
            market.Verify(m => m.SyncStockPriceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            market.Verify(m => m.SyncAllNavsAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RefreshProfileAsync_PrunesClosedPositions()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedEquityHolding(context, quantity: 10m, avgPrice: 100m, latestPriceHistory: 150m);

            context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = TransactionType.Sell,
                Quantity = 10m,
                Price = 150m,
                Amount = 1500m,
                TransactionDate = DateTime.UtcNow.AddDays(-1),
                Notes = "",
                Source = TransactionSource.Manual,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();

            var service = BuildService(context);

            var result = await service.RefreshProfileAsync(user.Id, profile.Id);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Data!);
            Assert.False(await context.Holdings.AnyAsync(h => h.ProfileId == profile.Id));
        }

        [Fact]
        public async Task RefreshProfileAsync_RejectsNonOwnerProfile()
        {
            using var context = CreateInMemoryDbContext();
            var (_, profile, _) = SeedEquityHolding(context, quantity: 10m, avgPrice: 100m, latestPriceHistory: 150m);
            var service = BuildService(context);

            var result = await service.RefreshProfileAsync(Guid.NewGuid(), profile.Id);

            Assert.True(result.IsFailure);
            Assert.Equal(403, result.StatusCode);
        }

        private static (User user, Profile profile, Instrument instrument) SeedGoldHolding(
            PortivioDbContext context, string purity = "24K")
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"u-{Guid.NewGuid()}@t.com",
                Name = "U",
                PasswordHash = "h",
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var profile = new Profile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = "P",
                BaseCurrency = "INR",
                Description = "",
                CreatedAt = DateTime.UtcNow
            };
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Gold" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Category = AssetCategory.Gold,
                Name = $"Gold {purity} Digital",
                Symbol = $"GOLD:{purity}:DIGITAL",
                Currency = "INR",
                PriceSource = PriceSource.Manual,
                Metadata = System.Text.Json.JsonDocument.Parse($"{{\"purity\":\"{purity}\",\"form\":\"DIGITAL\"}}")
            };
            context.Users.Add(user);
            context.Profiles.Add(profile);
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = TransactionType.Buy,
                Quantity = 10m,
                Price = 7000m,
                Amount = 70_000m,
                TransactionDate = DateTime.UtcNow.AddDays(-30),
                Notes = "",
                Source = TransactionSource.Manual,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-30)
            });
            context.Holdings.Add(new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 10m,
                AvgPrice = 7000m,
                CurrentPrice = 7000m,
                MarketValue = 70_000m,
                UnrealizedPnL = 0m,
                LastUpdated = DateTime.UtcNow.AddDays(-30)
            });
            context.SaveChanges();
            return (user, profile, instrument);
        }

        [Fact]
        public async Task RefreshProfileAsync_PullsGoldRateFromConfig_BeforeRecompute()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedGoldHolding(context, purity: "24K");

            var goldRate = new Mock<IGoldRateProvider>();
            goldRate.Setup(g => g.GetRatePerGramAsync("24K", It.IsAny<CancellationToken>()))
                .ReturnsAsync(7480m);

            var service = BuildService(
                context,
                marketData: NoopMarketData(),
                throttle: new RecordingThrottle(),
                goldRate: goldRate.Object,
                extraStrategies: new GoldStrategy(context));

            var result = await service.RefreshProfileAsync(user.Id, profile.Id);

            Assert.True(result.IsSuccess);
            Assert.True(await context.PriceHistories.AnyAsync(p => p.InstrumentId == instrument.Id && p.Source == "config"));
            var holding = await context.Holdings.FirstAsync(h => h.InstrumentId == instrument.Id);
            Assert.Equal(7480m, holding.CurrentPrice);
            Assert.Equal(74_800m, holding.MarketValue);   // 10 grams × 7480
            Assert.Equal(7000m, holding.AvgPrice);        // cost basis preserved
            goldRate.Verify(g => g.GetRatePerGramAsync("24K", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RefreshProfileAsync_DoesNotDuplicate_GoldPriceHistory_OnSecondCallSameDay()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedGoldHolding(context, purity: "24K");

            var goldRate = new Mock<IGoldRateProvider>();
            goldRate.Setup(g => g.GetRatePerGramAsync("24K", It.IsAny<CancellationToken>()))
                .ReturnsAsync(7480m);

            var service = BuildService(
                context,
                marketData: NoopMarketData(),
                throttle: new RecordingThrottle(),
                goldRate: goldRate.Object,
                extraStrategies: new GoldStrategy(context));

            await service.RefreshProfileAsync(user.Id, profile.Id);
            await service.RefreshProfileAsync(user.Id, profile.Id);

            var todayCount = await context.PriceHistories.CountAsync(p =>
                p.InstrumentId == instrument.Id &&
                p.Date.Date == DateTime.UtcNow.Date);
            Assert.Equal(1, todayCount);
        }

        // ---------- RunDailyRefreshAsync (slice #29) ----------

        [Fact]
        public async Task RunDailyRefreshAsync_ContinuesOnPerInstrumentFailure()
        {
            using var context = CreateInMemoryDbContext();
            var (_, profileGood, instGood) = SeedEquityHolding(context, 10m, 100m, latestPriceHistory: 150m, symbol: "GOOD");
            var (_, _, instBad) = SeedEquityHolding(context, 5m, 50m, latestPriceHistory: 55m, symbol: "BAD");

            var market = new Mock<IMarketDataService>();
            market.Setup(m => m.SyncAllNavsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SyncSummaryResponse>.Success(new SyncSummaryResponse()));
            market.Setup(m => m.SyncStockPriceAsync("GOOD", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<StockPriceResponse>.Success(new StockPriceResponse { Symbol = "GOOD", Price = 150m }));
            market.Setup(m => m.SyncStockPriceAsync("BAD", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("provider exploded"));

            var service = BuildService(context, market.Object);

            var result = await service.RunDailyRefreshAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.Errors);
            Assert.Contains(result.Data.ErrorMessages, m => m.Contains("BAD"));
            Assert.Equal(2, result.Data.HoldingsRecomputed);

            // Both holdings still recomputed via strategy snapshot regardless of provider failure.
            var goodHolding = await context.Holdings.FirstAsync(h => h.InstrumentId == instGood.Id);
            Assert.Equal(150m, goodHolding.CurrentPrice);
            var badHolding = await context.Holdings.FirstAsync(h => h.InstrumentId == instBad.Id);
            Assert.Equal(55m, badHolding.CurrentPrice);
        }

        [Fact]
        public async Task RunDailyRefreshAsync_ThrottlesAlphaVantageCalls()
        {
            using var context = CreateInMemoryDbContext();
            SeedEquityHolding(context, 10m, 100m, latestPriceHistory: 110m, symbol: "AAA");
            SeedEquityHolding(context, 5m, 200m, latestPriceHistory: 210m, symbol: "BBB");
            SeedEquityHolding(context, 1m, 300m, latestPriceHistory: 310m, symbol: "CCC");

            var throttle = new RecordingThrottle();
            var service = BuildService(context, NoopMarketData(), throttle);

            var result = await service.RunDailyRefreshAsync();

            Assert.True(result.IsSuccess);
            // 3 AlphaVantage calls → throttle invoked twice (between 1↔2 and 2↔3).
            Assert.Equal(2, throttle.Calls.Count);
            Assert.All(throttle.Calls, c => Assert.Equal(TimeSpan.FromSeconds(12), c));
        }

        [Fact]
        public async Task RunDailyRefreshAsync_AppliesGoldRateFromOptions()
        {
            using var context = CreateInMemoryDbContext();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"u-{Guid.NewGuid()}@t.com",
                Name = "U",
                PasswordHash = "h",
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var profile = new Profile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = "P",
                BaseCurrency = "INR",
                Description = "",
                CreatedAt = DateTime.UtcNow
            };
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Gold" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Category = AssetCategory.Gold,
                Name = "Gold 24K Digital",
                Symbol = "GOLD:24K:DIGITAL",
                Currency = "INR",
                PriceSource = PriceSource.Manual,
                Metadata = System.Text.Json.JsonDocument.Parse("{\"purity\":\"24K\",\"form\":\"DIGITAL\"}")
            };
            context.Users.Add(user);
            context.Profiles.Add(profile);
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = TransactionType.Buy,
                Quantity = 10m,
                Price = 7000m,
                Amount = 70000m,
                TransactionDate = DateTime.UtcNow.AddDays(-30),
                Notes = "",
                Source = TransactionSource.Manual,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-30)
            });
            context.Holdings.Add(new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 10m,
                AvgPrice = 7000m,
                CurrentPrice = 7000m,
                MarketValue = 70000m,
                UnrealizedPnL = 0m,
                LastUpdated = DateTime.UtcNow.AddDays(-30)
            });
            await context.SaveChangesAsync();

            var goldRate = new Mock<IGoldRateProvider>();
            goldRate.Setup(g => g.GetRatePerGramAsync("24K", It.IsAny<CancellationToken>()))
                .ReturnsAsync(7480m);

            var service = BuildService(
                context,
                marketData: NoopMarketData(),
                throttle: new RecordingThrottle(),
                goldRate: goldRate.Object,
                extraStrategies: new GoldStrategy(context));

            var result = await service.RunDailyRefreshAsync();

            Assert.True(result.IsSuccess);
            Assert.True(result.Data!.PricesUpdated >= 1);
            Assert.True(await context.PriceHistories.AnyAsync(p => p.InstrumentId == instrument.Id && p.Source == "config"));
            var holding = await context.Holdings.FirstAsync(h => h.InstrumentId == instrument.Id);
            Assert.Equal(7480m, holding.CurrentPrice);
            Assert.Equal(74800m, holding.MarketValue);     // 10 * 7480
        }

        [Fact]
        public async Task RunDailyRefreshAsync_IsIdempotent_OnSameDay()
        {
            using var context = CreateInMemoryDbContext();
            var (_, _, instrument) = SeedEquityHolding(context, 10m, 100m, latestPriceHistory: 150m);
            var service = BuildService(context);

            var first = await service.RunDailyRefreshAsync();
            var firstUpdated = await context.Holdings
                .Where(h => h.InstrumentId == instrument.Id)
                .Select(h => h.LastUpdated).FirstAsync();

            var second = await service.RunDailyRefreshAsync();
            var secondUpdated = await context.Holdings
                .Where(h => h.InstrumentId == instrument.Id)
                .Select(h => h.LastUpdated).FirstAsync();

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            // Holding row count is stable across runs (no duplicates).
            Assert.Equal(1, await context.Holdings.CountAsync(h => h.InstrumentId == instrument.Id));
            // Second run advances LastUpdated (or matches it within the same tick).
            Assert.True(secondUpdated >= firstUpdated);
        }
    }
}
