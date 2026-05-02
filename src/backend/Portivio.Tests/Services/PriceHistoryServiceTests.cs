using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portivio.Application.DTOs.PriceHistory;
using Portivio.Application.Services;
using Portivio.Application.Services.Authorization;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class PriceHistoryServiceTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private static (AssetType assetType, Instrument instrument) SeedInstrument(PortivioDbContext context)
        {
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Equity" };
            var instrument = new Instrument { Id = Guid.NewGuid(), AssetTypeId = assetType.Id, Name = "Test Corp", Symbol = "TEST", Currency = "USD" };
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.SaveChanges();
            return (assetType, instrument);
        }

        private static PriceHistoryService CreateService(PortivioDbContext context) =>
            new(context, new HoldingService(context, new Mock<ILogger<HoldingService>>().Object, new ProfileAccessGuard(context)));

        [Fact]
        public async Task AddPrice_ValidRequest_InsertsAndReturnsSuccess()
        {
            using var context = CreateInMemoryDbContext();
            var (_, instrument) = SeedInstrument(context);
            var service = CreateService(context);

            var result = await service.AddPriceAsync(instrument.Id, new AddPriceRequest
            {
                Price = 150m,
                Date = DateTime.UtcNow,
                Source = "Manual"
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(201, result.StatusCode);
            Assert.Equal(150m, result.Data!.Price);
            Assert.True(await context.PriceHistories.AnyAsync(ph => ph.InstrumentId == instrument.Id));
        }

        [Fact]
        public async Task AddPrice_DuplicateDate_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var (_, instrument) = SeedInstrument(context);
            var service = CreateService(context);
            var date = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);

            await service.AddPriceAsync(instrument.Id, new AddPriceRequest { Price = 100m, Date = date });
            var result = await service.AddPriceAsync(instrument.Id, new AddPriceRequest { Price = 110m, Date = date });

            Assert.False(result.IsSuccess);
            Assert.Equal(409, result.StatusCode);
        }

        [Fact]
        public async Task AddPrice_UpdatesHoldingCurrentPrice()
        {
            using var context = CreateInMemoryDbContext();
            var (_, instrument) = SeedInstrument(context);
            var user = new User { Id = Guid.NewGuid(), Email = "u@t.com", Name = "U", PasswordHash = "h", IsVerified = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            var profile = new Profile { Id = Guid.NewGuid(), UserId = user.Id, Name = "P", BaseCurrency = "USD", Description = "", CreatedAt = DateTime.UtcNow };
            var holding = new Holding { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Quantity = 10m, AvgPrice = 100m, CurrentPrice = 100m, MarketValue = 1000m, UnrealizedPnL = 0m, LastUpdated = DateTime.UtcNow };
            context.Users.Add(user);
            context.Profiles.Add(profile);
            context.Holdings.Add(holding);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            await service.AddPriceAsync(instrument.Id, new AddPriceRequest { Price = 130m, Date = DateTime.UtcNow });

            var updatedHolding = await context.Holdings.FindAsync(holding.Id);
            Assert.Equal(130m, updatedHolding!.CurrentPrice);
            Assert.Equal(1300m, updatedHolding.MarketValue);
            Assert.Equal(300m, updatedHolding.UnrealizedPnL);
        }

        [Fact]
        public async Task BulkAdd_MixedValidAndDuplicate_ReportsCorrectCounts()
        {
            using var context = CreateInMemoryDbContext();
            var (_, instrument) = SeedInstrument(context);
            var existingDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            context.PriceHistories.Add(new PriceHistory { Id = Guid.NewGuid(), InstrumentId = instrument.Id, Price = 100m, Date = existingDate, Source = "Existing", CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.BulkAddPricesAsync(instrument.Id, new BulkAddPriceRequest
            {
                Prices = new()
                {
                    new AddPriceRequest { Price = 110m, Date = new DateTime(2025, 1, 11, 0, 0, 0, DateTimeKind.Utc) },
                    new AddPriceRequest { Price = 120m, Date = new DateTime(2025, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
                    new AddPriceRequest { Price = 130m, Date = existingDate }
                }
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Data!.Inserted);
            Assert.Equal(1, result.Data.Skipped);
        }

        [Fact]
        public async Task GetPriceHistory_DateRangeFilter_ReturnsCorrectSlice()
        {
            using var context = CreateInMemoryDbContext();
            var (_, instrument) = SeedInstrument(context);
            for (int i = 1; i <= 5; i++)
            {
                context.PriceHistories.Add(new PriceHistory { Id = Guid.NewGuid(), InstrumentId = instrument.Id, Price = 100m + i, Date = new DateTime(2025, 1, i, 0, 0, 0, DateTimeKind.Utc), Source = "", CreatedAt = DateTime.UtcNow });
            }
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.GetPriceHistoryAsync(instrument.Id,
                from: new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                to: new DateTime(2025, 1, 4, 0, 0, 0, DateTimeKind.Utc));

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Data!.Count);
        }

        [Fact]
        public async Task GetLatestPrice_ReturnsHighestDate()
        {
            using var context = CreateInMemoryDbContext();
            var (_, instrument) = SeedInstrument(context);
            context.PriceHistories.AddRange(
                new PriceHistory { Id = Guid.NewGuid(), InstrumentId = instrument.Id, Price = 100m, Date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), Source = "", CreatedAt = DateTime.UtcNow },
                new PriceHistory { Id = Guid.NewGuid(), InstrumentId = instrument.Id, Price = 200m, Date = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc), Source = "", CreatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.GetLatestPriceAsync(instrument.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(200m, result.Data!.Price);
        }
    }
}
