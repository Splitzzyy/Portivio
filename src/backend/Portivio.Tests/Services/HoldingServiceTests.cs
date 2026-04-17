using Microsoft.EntityFrameworkCore;
using Portivio.Application.DTOs.Holding;
using Portivio.Application.Services;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class HoldingServiceTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private static (User user, Profile profile, AssetType assetType, Instrument instrument) SeedBasicData(PortivioDbContext context)
        {
            var user = new User { Id = Guid.NewGuid(), Email = $"u-{Guid.NewGuid()}@t.com", Name = "U", PasswordHash = "h", IsVerified = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            var profile = new Profile { Id = Guid.NewGuid(), UserId = user.Id, Name = "P", BaseCurrency = "USD", Description = "", CreatedAt = DateTime.UtcNow };
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Equity" };
            var instrument = new Instrument { Id = Guid.NewGuid(), AssetTypeId = assetType.Id, Name = "Test Corp", Symbol = "TEST", Currency = "USD" };
            context.Users.Add(user);
            context.Profiles.Add(profile);
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.SaveChanges();
            return (user, profile, assetType, instrument);
        }

        [Fact]
        public async Task UpsertHolding_NewHolding_CreatesRecord()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _, instrument) = SeedBasicData(context);
            var service = new HoldingService(context);

            var result = await service.UpsertHoldingAsync(user.Id, profile.Id, new UpsertHoldingRequest
            {
                InstrumentId = instrument.Id,
                Quantity = 10m,
                AvgPrice = 100m,
                CurrentPrice = 110m
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(10m, result.Data!.Quantity);
            Assert.Equal(100m, result.Data.AvgPrice);
            Assert.True(await context.Holdings.AnyAsync(h => h.ProfileId == profile.Id));
        }

        [Fact]
        public async Task UpsertHolding_ExistingHolding_UpdatesRecord()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _, instrument) = SeedBasicData(context);
            var holding = new Holding { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Quantity = 5m, AvgPrice = 90m, CurrentPrice = 95m, MarketValue = 475m, UnrealizedPnL = 25m, LastUpdated = DateTime.UtcNow };
            context.Holdings.Add(holding);
            await context.SaveChangesAsync();

            var service = new HoldingService(context);
            var result = await service.UpsertHoldingAsync(user.Id, profile.Id, new UpsertHoldingRequest
            {
                InstrumentId = instrument.Id,
                Quantity = 15m,
                AvgPrice = 95m,
                CurrentPrice = 100m
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(15m, result.Data!.Quantity);
            Assert.Equal(1, await context.Holdings.CountAsync(h => h.ProfileId == profile.Id && h.InstrumentId == instrument.Id));
        }

        [Fact]
        public async Task UpsertHolding_ComputesMarketValueAndPnL()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _, instrument) = SeedBasicData(context);
            var service = new HoldingService(context);

            var result = await service.UpsertHoldingAsync(user.Id, profile.Id, new UpsertHoldingRequest
            {
                InstrumentId = instrument.Id,
                Quantity = 10m,
                AvgPrice = 100m,
                CurrentPrice = 120m
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(1200m, result.Data!.MarketValue);
            Assert.Equal(200m, result.Data.UnrealizedPnL);
        }

        [Fact]
        public async Task DeleteHolding_OtherUsersProfile_ReturnsForbidden()
        {
            using var context = CreateInMemoryDbContext();
            var (user1, profile, _, instrument) = SeedBasicData(context);
            var user2 = new User { Id = Guid.NewGuid(), Email = "u2@t.com", Name = "U2", PasswordHash = "h", IsVerified = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            var holding = new Holding { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Quantity = 10m, AvgPrice = 100m, CurrentPrice = 110m, MarketValue = 1100m, UnrealizedPnL = 100m, LastUpdated = DateTime.UtcNow };
            context.Users.Add(user2);
            context.Holdings.Add(holding);
            await context.SaveChangesAsync();

            var service = new HoldingService(context);
            var result = await service.DeleteHoldingAsync(user2.Id, profile.Id, holding.Id);

            Assert.False(result.IsSuccess);
            Assert.Equal(403, result.StatusCode);
        }

        [Fact]
        public async Task RecalculateFromTransactions_BuyOnly_SetsCorrectAvgPrice()
        {
            using var context = CreateInMemoryDbContext();
            var (_, profile, _, instrument) = SeedBasicData(context);
            context.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Type = TransactionType.Buy, Quantity = 5m, Price = 100m, Amount = 500m, TransactionDate = DateTime.UtcNow.AddDays(-2), Notes = "" },
                new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Type = TransactionType.Buy, Quantity = 5m, Price = 120m, Amount = 600m, TransactionDate = DateTime.UtcNow.AddDays(-1), Notes = "" }
            );
            await context.SaveChangesAsync();

            var service = new HoldingService(context);
            await service.RecalculateHoldingFromTransactionsAsync(profile.Id, instrument.Id);

            var holding = await context.Holdings.FirstAsync(h => h.ProfileId == profile.Id);
            Assert.Equal(10m, holding.Quantity);
            Assert.Equal(110m, holding.AvgPrice);
        }

        [Fact]
        public async Task RecalculateFromTransactions_BuysAndSells_ReducesQuantity()
        {
            using var context = CreateInMemoryDbContext();
            var (_, profile, _, instrument) = SeedBasicData(context);
            context.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Type = TransactionType.Buy, Quantity = 10m, Price = 100m, Amount = 1000m, TransactionDate = DateTime.UtcNow.AddDays(-3), Notes = "" },
                new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Type = TransactionType.Sell, Quantity = 3m, Price = 120m, Amount = 360m, TransactionDate = DateTime.UtcNow.AddDays(-1), Notes = "" }
            );
            await context.SaveChangesAsync();

            var service = new HoldingService(context);
            await service.RecalculateHoldingFromTransactionsAsync(profile.Id, instrument.Id);

            var holding = await context.Holdings.FirstAsync(h => h.ProfileId == profile.Id);
            Assert.Equal(7m, holding.Quantity);
        }

        [Fact]
        public async Task RecalculateFromTransactions_FullSell_DeletesHolding()
        {
            using var context = CreateInMemoryDbContext();
            var (_, profile, _, instrument) = SeedBasicData(context);
            var existingHolding = new Holding { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Quantity = 10m, AvgPrice = 100m, CurrentPrice = 110m, MarketValue = 1100m, UnrealizedPnL = 100m, LastUpdated = DateTime.UtcNow };
            context.Holdings.Add(existingHolding);
            context.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Type = TransactionType.Buy, Quantity = 10m, Price = 100m, Amount = 1000m, TransactionDate = DateTime.UtcNow.AddDays(-2), Notes = "" },
                new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Type = TransactionType.Sell, Quantity = 10m, Price = 120m, Amount = 1200m, TransactionDate = DateTime.UtcNow.AddDays(-1), Notes = "" }
            );
            await context.SaveChangesAsync();

            var service = new HoldingService(context);
            await service.RecalculateHoldingFromTransactionsAsync(profile.Id, instrument.Id);

            Assert.False(await context.Holdings.AnyAsync(h => h.ProfileId == profile.Id && h.InstrumentId == instrument.Id));
        }
    }
}
