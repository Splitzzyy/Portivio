using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portivio.Application.DTOs.MarketData;
using Portivio.Application.Results;
using Portivio.Application.Services;
using Portivio.Application.Services.Authorization;
using Portivio.Application.Services.MarketData;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class MarketDataServiceTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private static ILogger<MarketDataService> CreateMockLogger() => new Mock<ILogger<MarketDataService>>().Object;

        private static Profile SeedProfile(PortivioDbContext context)
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

            context.Users.Add(user);
            context.Profiles.Add(profile);
            return profile;
        }

        [Fact]
        public async Task SyncAllNavsAsync_OnlyUpdatesInUseExistingInstruments()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            var profile = SeedProfile(context);
            
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Mutual Fund" };
            context.AssetTypes.Add(assetType);
            
            var instrument = new Instrument 
            { 
                Id = Guid.NewGuid(), 
                AssetTypeId = assetType.Id, 
                Name = "Existing Fund", 
                Symbol = "INF123", 
                Currency = "INR",
                Category = AssetCategory.MutualFund,
                PriceSource = PriceSource.AmfiNav
            };
            context.Instruments.Add(instrument);
            context.Holdings.Add(new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 10m,
                AvgPrice = 100m,
                CurrentPrice = 100m,
                MarketValue = 1000m,
                UnrealizedPnL = 0m,
                LastUpdated = DateTime.UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();

            var mockNavProvider = new Mock<IMutualFundNavProvider>();
            mockNavProvider.Setup(p => p.GetAllNavsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MutualFundNav>
                {
                    new MutualFundNav("INF123", "Existing Fund", 150.5m, DateTime.UtcNow, "AMFI"),
                    new MutualFundNav("INF999", "Non-existent Fund", 200m, DateTime.UtcNow, "AMFI")
                });

            var mockHoldingService = new Mock<IHoldingService>();
            mockHoldingService.Setup(h => h.BulkUpdateCurrentPricesAsync(It.IsAny<Dictionary<Guid, decimal>>()))
                .ReturnsAsync(Result.Success());

            var service = new MarketDataService(context, mockNavProvider.Object, mockHoldingService.Object, CreateMockLogger());

            // Act
            var result = await service.SyncAllNavsAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.Inserted);
            Assert.Equal(0, result.Data!.CreatedInstruments);
            
            // Verify that INF123 was updated but INF999 was not created
            Assert.Equal(1, await context.Instruments.CountAsync());
            Assert.True(await context.PriceHistories.AnyAsync(ph => ph.InstrumentId == instrument.Id && ph.Price == 150.5m));
            
            // Verify bulk update was called with correct data
            mockHoldingService.Verify(h => h.BulkUpdateCurrentPricesAsync(It.Is<Dictionary<Guid, decimal>>(d => d.ContainsKey(instrument.Id) && d[instrument.Id] == 150.5m)), Times.Once);
        }

        [Fact]
        public async Task SyncAllNavsAsync_SkipsIfPriceAlreadyExistsForToday()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            var profile = SeedProfile(context);
            
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Mutual Fund" };
            context.AssetTypes.Add(assetType);
            
            var instrument = new Instrument 
            { 
                Id = Guid.NewGuid(), 
                AssetTypeId = assetType.Id, 
                Name = "Existing Fund", 
                Symbol = "INF123", 
                Currency = "INR",
                Category = AssetCategory.MutualFund,
                PriceSource = PriceSource.AmfiNav
            };
            context.Instruments.Add(instrument);
            context.Holdings.Add(new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 10m,
                AvgPrice = 100m,
                CurrentPrice = 100m,
                MarketValue = 1000m,
                UnrealizedPnL = 0m,
                LastUpdated = DateTime.UtcNow.AddDays(-1)
            });
            
            var today = DateTime.UtcNow.Date;
            context.PriceHistories.Add(new PriceHistory
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrument.Id,
                Price = 149m,
                Date = DateTime.SpecifyKind(today, DateTimeKind.Utc),
                Source = "Seed",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var mockNavProvider = new Mock<IMutualFundNavProvider>();
            mockNavProvider.Setup(p => p.GetAllNavsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MutualFundNav>
                {
                    new MutualFundNav("INF123", "Existing Fund", 150.5m, DateTime.UtcNow, "AMFI")
                });

            var mockHoldingService = new Mock<IHoldingService>();

            var service = new MarketDataService(context, mockNavProvider.Object, mockHoldingService.Object, CreateMockLogger());

            // Act
            var result = await service.SyncAllNavsAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Data!.Inserted);
            Assert.Equal(1, result.Data!.Skipped);
            
            mockHoldingService.Verify(h => h.BulkUpdateCurrentPricesAsync(It.IsAny<Dictionary<Guid, decimal>>()), Times.Never);
        }

        [Fact]
        public async Task SyncAllNavsAsync_SkipsExistingMutualFundInstrument_WhenNotUsedByAnyProfile()
        {
            using var context = CreateInMemoryDbContext();

            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Mutual Fund" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Name = "Catalog Fund",
                Symbol = "INF123",
                Currency = "INR",
                Category = AssetCategory.MutualFund,
                PriceSource = PriceSource.AmfiNav
            };
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            await context.SaveChangesAsync();

            var mockNavProvider = new Mock<IMutualFundNavProvider>();
            var mockHoldingService = new Mock<IHoldingService>();

            var service = new MarketDataService(context, mockNavProvider.Object, mockHoldingService.Object, CreateMockLogger());

            var result = await service.SyncAllNavsAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Data!.Inserted);
            Assert.Equal(0, await context.PriceHistories.CountAsync());
            mockNavProvider.Verify(p => p.GetAllNavsAsync(It.IsAny<CancellationToken>()), Times.Never);
            mockHoldingService.Verify(h => h.BulkUpdateCurrentPricesAsync(It.IsAny<Dictionary<Guid, decimal>>()), Times.Never);
        }

        [Fact]
        public async Task SyncAllNavsAsync_IncludesMutualFundInstrumentUsedByNonDeletedTransaction()
        {
            using var context = CreateInMemoryDbContext();
            var profile = SeedProfile(context);

            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Mutual Fund" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Name = "Existing Fund",
                Symbol = "INF123",
                Currency = "INR",
                Category = AssetCategory.MutualFund,
                PriceSource = PriceSource.AmfiNav
            };
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = TransactionType.Buy,
                Quantity = 10m,
                Price = 100m,
                Amount = 1000m,
                TransactionDate = DateTime.UtcNow.AddDays(-2),
                Notes = "",
                Source = TransactionSource.Manual,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
            });
            await context.SaveChangesAsync();

            var mockNavProvider = new Mock<IMutualFundNavProvider>();
            mockNavProvider.Setup(p => p.GetAllNavsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MutualFundNav>
                {
                    new MutualFundNav("INF123", "Existing Fund", 150.5m, DateTime.UtcNow, "AMFI")
                });

            var mockHoldingService = new Mock<IHoldingService>();
            mockHoldingService.Setup(h => h.BulkUpdateCurrentPricesAsync(It.IsAny<Dictionary<Guid, decimal>>()))
                .ReturnsAsync(Result.Success());

            var service = new MarketDataService(context, mockNavProvider.Object, mockHoldingService.Object, CreateMockLogger());

            var result = await service.SyncAllNavsAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.Inserted);
            Assert.True(await context.PriceHistories.AnyAsync(ph => ph.InstrumentId == instrument.Id && ph.Price == 150.5m));
        }
    }
}
