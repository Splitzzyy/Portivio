using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portivio.Application.DTOs.MarketData;
using Portivio.Application.Results;
using Portivio.Application.Services;
using Portivio.Application.Services.MarketData;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class StandardRateServiceTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private static ILogger<StandardRateService> CreateMockLogger() => new Mock<ILogger<StandardRateService>>().Object;
        private static IOptions<MarketDataOptions> CreateMockOptions() => Options.Create(new MarketDataOptions());

        [Fact]
        public async Task SyncFdRatesAsync_OnlyUpdatesExistingInstruments()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "FD" };
            context.AssetTypes.Add(assetType);
            
            var instrument = new Instrument 
            { 
                Id = Guid.NewGuid(), 
                AssetTypeId = assetType.Id, 
                Name = "FD SBI 12M", 
                Symbol = "FD:SBI:12M", 
                Currency = "INR" 
            };
            context.Instruments.Add(instrument);
            await context.SaveChangesAsync();

            var mockRateProvider = new Mock<IStandardRateProvider>();
            mockRateProvider.Setup(p => p.GetFdRatesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FdRateEntry>
                {
                    new FdRateEntry("SBI", 12, 7.5m, DateTime.UtcNow, "SBI_WEBSITE"),
                    new FdRateEntry("HDFC", 24, 8.0m, DateTime.UtcNow, "HDFC_WEBSITE")
                });

            var mockHoldingService = new Mock<IHoldingService>();
            mockHoldingService.Setup(h => h.BulkUpdateCurrentPricesAsync(It.IsAny<Dictionary<Guid, decimal>>()))
                .ReturnsAsync(Result.Success());

            var service = new StandardRateService(context, mockRateProvider.Object, mockHoldingService.Object, CreateMockOptions(), CreateMockLogger());

            // Act
            var result = await service.SyncFdRatesAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Data!.Inserted);
            
            // Verify that SBI was updated but HDFC was not created
            Assert.Equal(1, await context.Instruments.CountAsync());
            Assert.True(await context.PriceHistories.AnyAsync(ph => ph.InstrumentId == instrument.Id && ph.Price == 7.5m));
            
            // Verify bulk update was called
            mockHoldingService.Verify(h => h.BulkUpdateCurrentPricesAsync(It.Is<Dictionary<Guid, decimal>>(d => d.ContainsKey(instrument.Id) && d[instrument.Id] == 7.5m)), Times.Once);
        }

        [Fact]
        public async Task SyncFdRatesAsync_SkipsIfPriceAlreadyExistsForToday()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "FD" };
            context.AssetTypes.Add(assetType);
            
            var instrument = new Instrument 
            { 
                Id = Guid.NewGuid(), 
                AssetTypeId = assetType.Id, 
                Name = "FD SBI 12M", 
                Symbol = "FD:SBI:12M", 
                Currency = "INR" 
            };
            context.Instruments.Add(instrument);
            
            var today = DateTime.UtcNow.Date;
            context.PriceHistories.Add(new PriceHistory
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrument.Id,
                Price = 7.4m,
                Date = DateTime.SpecifyKind(today, DateTimeKind.Utc),
                Source = "Seed",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var mockRateProvider = new Mock<IStandardRateProvider>();
            mockRateProvider.Setup(p => p.GetFdRatesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FdRateEntry>
                {
                    new FdRateEntry("SBI", 12, 7.5m, DateTime.UtcNow, "SBI_WEBSITE")
                });

            var mockHoldingService = new Mock<IHoldingService>();

            var service = new StandardRateService(context, mockRateProvider.Object, mockHoldingService.Object, CreateMockOptions(), CreateMockLogger());

            // Act
            var result = await service.SyncFdRatesAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Data!.Inserted);
            Assert.Equal(1, result.Data!.Skipped);
            
            mockHoldingService.Verify(h => h.BulkUpdateCurrentPricesAsync(It.IsAny<Dictionary<Guid, decimal>>()), Times.Never);
        }
    }
}
