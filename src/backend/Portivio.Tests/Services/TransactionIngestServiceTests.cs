using Microsoft.EntityFrameworkCore;
using Portivio.Application.Services;
using Portivio.Application.Services.Authorization;
using Portivio.Application.Services.Strategies;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class TransactionIngestServiceTests
    {
        private static PortivioDbContext CreateContext() =>
            new(new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        private static TransactionIngestService CreateService(PortivioDbContext context)
        {
            var guard = new ProfileAccessGuard(context);
            var equity = new EquityStrategy(context);
            var resolver = new AssetStrategyResolver(new IAssetStrategy[] { equity });
            return new TransactionIngestService(context, guard, resolver);
        }

        private static async Task<(User user, Profile profile, Instrument instrument)> SeedAsync(PortivioDbContext ctx)
        {
            var user = new User { Id = Guid.NewGuid(), Email = $"u-{Guid.NewGuid()}@t.com", Name = "U", PasswordHash = "h", IsVerified = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            var profile = new Profile { Id = Guid.NewGuid(), UserId = user.Id, Name = "P", BaseCurrency = "USD", Description = "", CreatedAt = DateTime.UtcNow };
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Equity" };
            var instrument = new Instrument { Id = Guid.NewGuid(), AssetTypeId = assetType.Id, Category = AssetCategory.Equity, Name = "Test Corp", Symbol = "TEST", Currency = "USD" };
            ctx.Users.Add(user);
            ctx.Profiles.Add(profile);
            ctx.AssetTypes.Add(assetType);
            ctx.Instruments.Add(instrument);
            await ctx.SaveChangesAsync();
            return (user, profile, instrument);
        }

        [Fact]
        public async Task IngestAsync_BuyTransaction_CreatesHolding()
        {
            using var ctx = CreateContext();
            var (user, profile, instrument) = await SeedAsync(ctx);
            var svc = CreateService(ctx);

            var cmd = new TransactionCommand(
                ProfileId: profile.Id,
                InstrumentId: instrument.Id,
                Type: TransactionType.Buy,
                Quantity: 10m,
                Price: 150m,
                Amount: 1500m,
                TransactionDateUtc: DateTime.UtcNow,
                Notes: null,
                ClientTxnId: null);

            var result = await svc.IngestAsync(user.Id, cmd, TransactionSource.Manual);

            Assert.True(result.IsSuccess);
            Assert.Equal(201, result.StatusCode);
            var holding = await ctx.Holdings.FirstOrDefaultAsync(h => h.ProfileId == profile.Id);
            Assert.NotNull(holding);
            Assert.Equal(10m, holding!.Quantity);
            Assert.Equal(150m, holding.AvgPrice);
        }

        [Fact]
        public async Task IngestAsync_InvalidInstrument_ReturnsBadRequest()
        {
            using var ctx = CreateContext();
            var (user, profile, _) = await SeedAsync(ctx);
            var svc = CreateService(ctx);

            var cmd = new TransactionCommand(
                ProfileId: profile.Id,
                InstrumentId: Guid.NewGuid(),
                Type: TransactionType.Buy,
                Quantity: 1m,
                Price: 10m,
                Amount: 10m,
                TransactionDateUtc: DateTime.UtcNow,
                Notes: null,
                ClientTxnId: null);

            var result = await svc.IngestAsync(user.Id, cmd, TransactionSource.Manual);

            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task IngestAsync_WithClientTxnId_IdempotentOnRepeat()
        {
            using var ctx = CreateContext();
            var (user, profile, instrument) = await SeedAsync(ctx);
            var svc = CreateService(ctx);
            var clientId = "sip:plan1:20260501";

            var cmd = new TransactionCommand(
                ProfileId: profile.Id,
                InstrumentId: instrument.Id,
                Type: TransactionType.Buy,
                Quantity: 5m,
                Price: 200m,
                Amount: 1000m,
                TransactionDateUtc: DateTime.UtcNow,
                Notes: null,
                ClientTxnId: clientId);

            var first = await svc.IngestAsync(user.Id, cmd, TransactionSource.Sip);
            var second = await svc.IngestAsync(user.Id, cmd, TransactionSource.Sip);

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.Equal(first.Data!.Id, second.Data!.Id);
            Assert.Equal(1, await ctx.Transactions.CountAsync(t => t.ProfileId == profile.Id));
        }

        [Fact]
        public async Task IngestAsync_OtherUsersProfile_ReturnsForbidden()
        {
            using var ctx = CreateContext();
            var (_, profile, instrument) = await SeedAsync(ctx);
            var other = new User { Id = Guid.NewGuid(), Email = "other@t.com", Name = "O", PasswordHash = "h", IsVerified = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            ctx.Users.Add(other);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var cmd = new TransactionCommand(profile.Id, instrument.Id, TransactionType.Buy, 1m, 10m, 10m, DateTime.UtcNow, null, null);

            var result = await svc.IngestAsync(other.Id, cmd, TransactionSource.Manual);

            Assert.False(result.IsSuccess);
            Assert.Equal(403, result.StatusCode);
        }

        [Fact]
        public async Task IngestAsync_SetsSourceAndTimestamps()
        {
            using var ctx = CreateContext();
            var (user, profile, instrument) = await SeedAsync(ctx);
            var svc = CreateService(ctx);

            var before = DateTime.UtcNow.AddSeconds(-1);
            var cmd = new TransactionCommand(profile.Id, instrument.Id, TransactionType.Buy, 2m, 50m, 100m, DateTime.UtcNow, null, null);
            await svc.IngestAsync(user.Id, cmd, TransactionSource.Sip);

            var tx = await ctx.Transactions.FirstAsync(t => t.ProfileId == profile.Id);
            Assert.Equal(TransactionSource.Sip, tx.Source);
            Assert.True(tx.CreatedAtUtc >= before);
            Assert.True(tx.UpdatedAtUtc >= before);
            Assert.False(tx.IsDeleted);
        }
    }
}
