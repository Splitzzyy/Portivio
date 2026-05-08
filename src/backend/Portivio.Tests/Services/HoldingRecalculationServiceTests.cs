using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portivio.Application.Services;
using Portivio.Application.Services.Authorization;
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

        private static (User user, Profile profile, Instrument instrument) SeedEquityHolding(
            PortivioDbContext context,
            decimal quantity,
            decimal avgPrice,
            decimal? latestPriceHistory,
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
                Name = "Test Corp",
                Symbol = "TEST",
                Currency = "USD"
            };
            context.Users.Add(user);
            context.Profiles.Add(profile);
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);

            // Seed a Buy transaction matching the requested cost basis.
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

            // Pre-existing holding row carrying stale derived fields the service should overwrite.
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
            // Real EquityStrategy keeps the test honest about price-history lookups.
            // Tests can pass extra mocked strategies for other categories.
            var list = new List<IAssetStrategy> { new EquityStrategy(context) };
            list.AddRange(extra);
            return new AssetStrategyResolver(list);
        }

        [Fact]
        public async Task RefreshProfileAsync_RecomputesHoldings_UsingLatestPriceHistory()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedEquityHolding(context, quantity: 10m, avgPrice: 100m, latestPriceHistory: 150m);
            var service = new HoldingRecalculationService(
                context,
                BuildResolver(context),
                new ProfileAccessGuard(context),
                CreateMockLogger());

            var result = await service.RefreshProfileAsync(user.Id, profile.Id);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Data!);
            var holding = result.Data![0];
            Assert.Equal(150m, holding.CurrentPrice);
            Assert.Equal(1500m, holding.MarketValue);          // 10 * 150
            Assert.Equal(500m, holding.UnrealizedPnL);         // (150 - 100) * 10
            Assert.True(holding.LastUpdated > DateTime.UtcNow.AddDays(-1));
        }

        [Fact]
        public async Task RefreshProfileAsync_PrunesClosedPositions()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedEquityHolding(context, quantity: 10m, avgPrice: 100m, latestPriceHistory: 150m);

            // Add an offsetting Sell so net qty == 0; strategy snapshot will return Quantity=0.
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

            var service = new HoldingRecalculationService(
                context,
                BuildResolver(context),
                new ProfileAccessGuard(context),
                CreateMockLogger());

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
            var otherUser = Guid.NewGuid();
            var service = new HoldingRecalculationService(
                context,
                BuildResolver(context),
                new ProfileAccessGuard(context),
                CreateMockLogger());

            var result = await service.RefreshProfileAsync(otherUser, profile.Id);

            Assert.True(result.IsFailure);
            Assert.Equal(403, result.StatusCode);
        }
    }
}
