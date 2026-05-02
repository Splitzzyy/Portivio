using Microsoft.EntityFrameworkCore;
using Portivio.Application.DTOs.PortfolioPerformance;
using Portivio.Application.Services;
using Portivio.Application.Services.Authorization;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class PortfolioPerformanceServiceTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private static (User user, Profile profile, Instrument instrument) SeedBasicData(PortivioDbContext context)
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
            return (user, profile, instrument);
        }

        [Fact]
        public async Task RecordSnapshot_EmptyPortfolio_RecordsZeroValues()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _) = SeedBasicData(context);
            var service = new PortfolioPerformanceService(context, new ProfileAccessGuard(context));

            var result = await service.RecordSnapshotAsync(user.Id, profile.Id, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(0m, result.Data!.TotalInvestment);
            Assert.Equal(0m, result.Data.CurrentValue);
        }

        [Fact]
        public async Task RecordSnapshot_WithHoldings_ComputesCurrentValue()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedBasicData(context);
            context.Holdings.Add(new Holding { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Quantity = 10m, AvgPrice = 100m, CurrentPrice = 120m, MarketValue = 1200m, UnrealizedPnL = 200m, LastUpdated = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var service = new PortfolioPerformanceService(context, new ProfileAccessGuard(context));
            var result = await service.RecordSnapshotAsync(user.Id, profile.Id, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(1000m, result.Data!.TotalInvestment);
            Assert.Equal(1200m, result.Data.CurrentValue);
            Assert.Equal(200m, result.Data.TotalReturn);
        }

        [Fact]
        public async Task RecordSnapshot_DuplicateDate_Overwrites()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = SeedBasicData(context);
            context.Holdings.Add(new Holding { Id = Guid.NewGuid(), ProfileId = profile.Id, InstrumentId = instrument.Id, Quantity = 10m, AvgPrice = 100m, CurrentPrice = 110m, MarketValue = 1100m, UnrealizedPnL = 100m, LastUpdated = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var service = new PortfolioPerformanceService(context, new ProfileAccessGuard(context));
            var date = new RecordSnapshotRequest { Date = DateTime.UtcNow };
            await service.RecordSnapshotAsync(user.Id, profile.Id, date);

            var holding = await context.Holdings.FirstAsync();
            holding.CurrentPrice = 130m;
            holding.MarketValue = 1300m;
            await context.SaveChangesAsync();

            var result = await service.RecordSnapshotAsync(user.Id, profile.Id, date);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, await context.PortfolioPerformances.CountAsync(pp => pp.ProfileId == profile.Id));
            Assert.Equal(1300m, result.Data!.CurrentValue);
        }

        [Fact]
        public async Task RecordSnapshot_OtherUsersProfile_ReturnsForbidden()
        {
            using var context = CreateInMemoryDbContext();
            var (_, profile, _) = SeedBasicData(context);
            var otherUser = new User { Id = Guid.NewGuid(), Email = "other@t.com", Name = "O", PasswordHash = "h", IsVerified = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            context.Users.Add(otherUser);
            await context.SaveChangesAsync();

            var service = new PortfolioPerformanceService(context, new ProfileAccessGuard(context));
            var result = await service.RecordSnapshotAsync(otherUser.Id, profile.Id, null);

            Assert.False(result.IsSuccess);
            Assert.Equal(403, result.StatusCode);
        }

        [Fact]
        public async Task GetPerformanceHistory_ReturnsCorrectDayWindow()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile, _) = SeedBasicData(context);
            var snapshots = new[]
            {
                new PortfolioPerformance { Id = Guid.NewGuid(), ProfileId = profile.Id, Date = DateTime.UtcNow.AddDays(-100), TotalInvestment = 1000m, CurrentValue = 1100m, DayChange = 0m, TotalReturn = 100m, XIRR = 0.1m, CreatedAt = DateTime.UtcNow },
                new PortfolioPerformance { Id = Guid.NewGuid(), ProfileId = profile.Id, Date = DateTime.UtcNow.AddDays(-30), TotalInvestment = 1000m, CurrentValue = 1150m, DayChange = 50m, TotalReturn = 150m, XIRR = 0.15m, CreatedAt = DateTime.UtcNow },
                new PortfolioPerformance { Id = Guid.NewGuid(), ProfileId = profile.Id, Date = DateTime.UtcNow.AddDays(-1), TotalInvestment = 1000m, CurrentValue = 1200m, DayChange = 50m, TotalReturn = 200m, XIRR = 0.2m, CreatedAt = DateTime.UtcNow }
            };
            context.PortfolioPerformances.AddRange(snapshots);
            await context.SaveChangesAsync();

            var service = new PortfolioPerformanceService(context, new ProfileAccessGuard(context));
            var result = await service.GetPerformanceHistoryAsync(user.Id, profile.Id, days: 90);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Data!.History.Count);
            Assert.NotNull(result.Data.Latest);
        }
    }
}
