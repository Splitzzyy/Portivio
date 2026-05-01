using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portivio.Application.DTOs.Instrument;
using Portivio.Application.Services;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class InstrumentServiceTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private static ILogger<InstrumentService> CreateMockLogger() => new Mock<ILogger<InstrumentService>>().Object;

        private static AssetType CreateAssetType(string name = "Equity") => new()
        {
            Id = Guid.NewGuid(),
            Name = name
        };

        private static Instrument CreateInstrument(Guid assetTypeId, string symbol = "TEST") => new()
        {
            Id = Guid.NewGuid(),
            AssetTypeId = assetTypeId,
            Name = "Test Corp",
            Symbol = symbol,
            Currency = "USD"
        };

        [Fact]
        public async Task CreateAssetType_ValidRequest_ReturnsSuccess()
        {
            using var context = CreateInMemoryDbContext();
            var service = new InstrumentService(context, CreateMockLogger());

            var result = await service.CreateAssetTypeAsync(new CreateAssetTypeRequest { Name = "Equity" });

            Assert.True(result.IsSuccess);
            Assert.Equal(201, result.StatusCode);
            Assert.Equal("Equity", result.Data!.Name);
        }

        [Fact]
        public async Task CreateAssetType_DuplicateName_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            context.AssetTypes.Add(CreateAssetType("Equity"));
            await context.SaveChangesAsync();

            var service = new InstrumentService(context, CreateMockLogger());
            var result = await service.CreateAssetTypeAsync(new CreateAssetTypeRequest { Name = "equity" });

            Assert.False(result.IsSuccess);
            Assert.Equal(409, result.StatusCode);
        }

        [Fact]
        public async Task DeleteAssetType_WithInstruments_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var assetType = CreateAssetType();
            var instrument = CreateInstrument(assetType.Id);
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            await context.SaveChangesAsync();

            var service = new InstrumentService(context, CreateMockLogger());
            var result = await service.DeleteAssetTypeAsync(assetType.Id);

            Assert.False(result.IsSuccess);
            Assert.Equal(409, result.StatusCode);
        }

        [Fact]
        public async Task DeleteAssetType_NoInstruments_ReturnsSuccess()
        {
            using var context = CreateInMemoryDbContext();
            var assetType = CreateAssetType();
            context.AssetTypes.Add(assetType);
            await context.SaveChangesAsync();

            var service = new InstrumentService(context, CreateMockLogger());
            var result = await service.DeleteAssetTypeAsync(assetType.Id);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task CreateInstrument_ValidRequest_ReturnsSuccess()
        {
            using var context = CreateInMemoryDbContext();
            var assetType = CreateAssetType();
            context.AssetTypes.Add(assetType);
            await context.SaveChangesAsync();

            var service = new InstrumentService(context, CreateMockLogger());
            var result = await service.CreateInstrumentAsync(new CreateInstrumentRequest
            {
                AssetTypeId = assetType.Id,
                Name = "Reliance Industries",
                Symbol = "RELIANCE",
                Currency = "INR"
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(201, result.StatusCode);
            Assert.Equal("RELIANCE", result.Data!.Symbol);
        }

        [Fact]
        public async Task CreateInstrument_DuplicateSymbol_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var assetType = CreateAssetType();
            var instrument = CreateInstrument(assetType.Id, "RELIANCE");
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            await context.SaveChangesAsync();

            var service = new InstrumentService(context, CreateMockLogger());
            var result = await service.CreateInstrumentAsync(new CreateInstrumentRequest
            {
                AssetTypeId = assetType.Id,
                Name = "Another Corp",
                Symbol = "reliance",
                Currency = "INR"
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(409, result.StatusCode);
        }

        [Fact]
        public async Task CreateInstrument_NonexistentAssetType_ReturnsBadRequest()
        {
            using var context = CreateInMemoryDbContext();
            var service = new InstrumentService(context, CreateMockLogger());

            var result = await service.CreateInstrumentAsync(new CreateInstrumentRequest
            {
                AssetTypeId = Guid.NewGuid(),
                Name = "Test",
                Symbol = "TST",
                Currency = "USD"
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task DeleteInstrument_WithHoldings_ReturnsConflict()
        {
            using var context = CreateInMemoryDbContext();
            var assetType = CreateAssetType();
            var instrument = CreateInstrument(assetType.Id);
            var user = new User { Id = Guid.NewGuid(), Email = "u@test.com", Name = "U", PasswordHash = "h", IsVerified = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            var profile = new Profile { Id = Guid.NewGuid(), UserId = user.Id, Name = "P", BaseCurrency = "USD", Description = "", CreatedAt = DateTime.UtcNow };
            var holding = new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 10m,
                AvgPrice = 100m,
                CurrentPrice = 110m,
                MarketValue = 1100m,
                UnrealizedPnL = 100m,
                LastUpdated = DateTime.UtcNow
            };

            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Users.Add(user);
            context.Profiles.Add(profile);
            context.Holdings.Add(holding);
            await context.SaveChangesAsync();

            var service = new InstrumentService(context, CreateMockLogger());
            var result = await service.DeleteInstrumentAsync(instrument.Id);

            Assert.False(result.IsSuccess);
            Assert.Equal(409, result.StatusCode);
        }

        [Fact]
        public async Task GetInstruments_FilterByAssetType_ReturnsFilteredList()
        {
            using var context = CreateInMemoryDbContext();
            var equity = CreateAssetType("Equity");
            var bond = CreateAssetType("Bond");
            var equityInstrument = CreateInstrument(equity.Id, "AAPL");
            var bondInstrument = CreateInstrument(bond.Id, "USGOV");
            context.AssetTypes.AddRange(equity, bond);
            context.Instruments.AddRange(equityInstrument, bondInstrument);
            await context.SaveChangesAsync();

            var service = new InstrumentService(context, CreateMockLogger());
            var result = await service.GetInstrumentsAsync(equity.Id);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Data!);
            Assert.Equal("AAPL", result.Data![0].Symbol);
        }
    }
}
