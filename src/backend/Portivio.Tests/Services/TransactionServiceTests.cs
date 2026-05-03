using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portivio.Application.DTOs.Transaction;
using Portivio.Application.Services;
using Portivio.Application.Services.Authorization;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class TransactionServiceTests
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

        private static TransactionService CreateService(PortivioDbContext context)
        {
            var guard = new ProfileAccessGuard(context);
            var holdings = new HoldingService(context, new Mock<ILogger<HoldingService>>().Object, guard);
            return new TransactionService(context, holdings, guard);
        }

        [Fact]
        public async Task CreateTransaction_Buy_ValidRequest_ReturnsSuccess()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _, instrument) = SeedBasicData(context);
            var service = CreateService(context);

            var result = await service.CreateTransactionAsync(user.Id, profile.Id, new CreateTransactionRequest
            {
                InstrumentId = instrument.Id,
                Type = TransactionType.Buy,
                Quantity = 10m,
                Price = 100m,
                Amount = 1000m,
                TransactionDate = DateTime.UtcNow
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(201, result.StatusCode);
            Assert.Equal(TransactionType.Buy, result.Data!.Type);
        }

        [Fact]
        public async Task CreateTransaction_Buy_UpdatesHolding()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _, instrument) = SeedBasicData(context);
            var service = CreateService(context);

            await service.CreateTransactionAsync(user.Id, profile.Id, new CreateTransactionRequest
            {
                InstrumentId = instrument.Id,
                Type = TransactionType.Buy,
                Quantity = 10m,
                Price = 100m,
                Amount = 1000m,
                TransactionDate = DateTime.UtcNow
            });

            var holding = await context.Holdings.FirstOrDefaultAsync(h => h.ProfileId == profile.Id);
            Assert.NotNull(holding);
            Assert.Equal(10m, holding!.Quantity);
            Assert.Equal(100m, holding.AvgPrice);
        }

        [Fact]
        public async Task CreateTransaction_InvalidType_ReturnsBadRequest()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _, instrument) = SeedBasicData(context);
            var service = CreateService(context);

            var result = await service.CreateTransactionAsync(user.Id, profile.Id, new CreateTransactionRequest
            {
                InstrumentId = instrument.Id,
                Type = (TransactionType)999,
                Quantity = 10m,
                Price = 100m,
                Amount = 1000m,
                TransactionDate = DateTime.UtcNow
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task CreateTransaction_Dividend_ZeroQuantityAllowed()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _, instrument) = SeedBasicData(context);
            var service = CreateService(context);

            var result = await service.CreateTransactionAsync(user.Id, profile.Id, new CreateTransactionRequest
            {
                InstrumentId = instrument.Id,
                Type = TransactionType.Dividend,
                Quantity = 0m,
                Price = 0m,
                Amount = 500m,
                TransactionDate = DateTime.UtcNow
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(TransactionType.Dividend, result.Data!.Type);
        }

        [Fact]
        public async Task CreateTransaction_OtherUsersProfile_ReturnsForbidden()
        {
            using var context = CreateInMemoryDbContext();
            var (_, profile, _, instrument) = SeedBasicData(context);
            var otherUser = new User { Id = Guid.NewGuid(), Email = "other@t.com", Name = "O", PasswordHash = "h", IsVerified = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            context.Users.Add(otherUser);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.CreateTransactionAsync(otherUser.Id, profile.Id, new CreateTransactionRequest
            {
                InstrumentId = instrument.Id,
                Type = TransactionType.Buy,
                Quantity = 10m,
                Price = 100m,
                Amount = 1000m,
                TransactionDate = DateTime.UtcNow
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(403, result.StatusCode);
        }

        [Fact]
        public async Task DeleteTransaction_RemovesFromDb()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _, instrument) = SeedBasicData(context);
            var tx = new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Type = TransactionType.Buy, Quantity = 5m, Price = 100m, Amount = 500m, TransactionDate = DateTime.UtcNow, Notes = "" };
            context.Transactions.Add(tx);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.DeleteTransactionAsync(user.Id, profile.Id, tx.Id);

            Assert.True(result.IsSuccess);
            Assert.False(await context.Transactions.AnyAsync(t => t.Id == tx.Id));
        }

        [Fact]
        public async Task GetTransactions_Paginated_ReturnsCorrectPage()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _, instrument) = SeedBasicData(context);
            for (int i = 0; i < 5; i++)
            {
                context.Transactions.Add(new Transaction { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Type = TransactionType.Buy, Quantity = 1m, Price = 100m, Amount = 100m, TransactionDate = DateTime.UtcNow.AddDays(-i), Notes = "" });
            }
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.GetTransactionsAsync(user.Id, profile.Id, page: 1, pageSize: 3);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Data!.Items.Count);
            Assert.Equal(5, result.Data.Total);
            Assert.Equal(1, result.Data.Page);
            Assert.Equal(3, result.Data.PageSize);
            Assert.True(result.Data.HasMore);
        }

        [Fact]
        public async Task GetTransactions_DoesNotLeakOtherProfilesData()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile1, _, instrument) = SeedBasicData(context);
            var profile2 = new Profile { Id = Guid.NewGuid(), UserId = user.Id, Name = "P2", BaseCurrency = "USD", Description = "", CreatedAt = DateTime.UtcNow };
            context.Profiles.Add(profile2);
            context.Transactions.Add(new Transaction { Id = Guid.NewGuid(), ProfileId = profile2.Id, InstrumentId = instrument.Id, Type = TransactionType.Buy, Quantity = 1m, Price = 100m, Amount = 100m, TransactionDate = DateTime.UtcNow, Notes = "" });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.GetTransactionsAsync(user.Id, profile1.Id, page: 1, pageSize: 50);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Data!.Items);
            Assert.Equal(0, result.Data.Total);
        }
    }
}
