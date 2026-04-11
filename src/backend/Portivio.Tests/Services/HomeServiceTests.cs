using Microsoft.EntityFrameworkCore;
using Portivio.Application.Services;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class HomeServiceTests
    {
        private PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new PortivioDbContext(options);
        }

        private async Task<(User user, Profile profile, Instrument instrument)> SeedUserWithPortfolioAsync(PortivioDbContext context)
        {
            var assetType = new AssetType
            {
                Id = Guid.NewGuid(),
                Name = "Equity"
            };

            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Name = "Acme Corp",
                Symbol = "ACME",
                Currency = "USD",
                AssetType = assetType
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "investor@example.com",
                Name = "Investor",
                PasswordHash = "hash",
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LastLoginAt = DateTime.UtcNow.AddHours(-1)
            };

            var profile = new Profile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = "Main Portfolio",
                BaseCurrency = "USD",
                Description = "Primary account",
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            };

            var holding = new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 10m,
                AvgPrice = 100m,
                CurrentPrice = 120m,
                MarketValue = 1200m,
                UnrealizedPnL = 200m,
                LastUpdated = DateTime.UtcNow
            };

            var transactionOld = new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = TransactionType.Buy,
                Quantity = 10m,
                Price = 100m,
                Amount = 1000m,
                TransactionDate = DateTime.UtcNow.AddDays(-10),
                Notes = "Initial buy"
            };

            var transactionNew = new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = TransactionType.Dividend,
                Quantity = 0m,
                Price = 0m,
                Amount = 25m,
                TransactionDate = DateTime.UtcNow.AddDays(-2),
                Notes = "Q1 dividend"
            };

            var activeSip = new SIPPlan
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Amount = 500m,
                SIPDay = 5,
                StartDate = DateTime.UtcNow.AddDays(-60),
                EndDate = DateTime.UtcNow.AddDays(300),
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            };

            var inactiveSip = new SIPPlan
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Amount = 200m,
                SIPDay = 15,
                StartDate = DateTime.UtcNow.AddDays(-120),
                EndDate = DateTime.UtcNow.AddDays(-30),
                IsActive = false,
                CreatedAt = DateTime.UtcNow.AddDays(-120)
            };

            var perfOld = new PortfolioPerformance
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                Date = DateTime.UtcNow.AddDays(-5),
                TotalInvestment = 1000m,
                CurrentValue = 1100m,
                DayChange = 10m,
                TotalReturn = 100m,
                XIRR = 0.12m,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            };

            var perfLatest = new PortfolioPerformance
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                Date = DateTime.UtcNow.AddDays(-1),
                TotalInvestment = 1000m,
                CurrentValue = 1200m,
                DayChange = 20m,
                TotalReturn = 200m,
                XIRR = 0.18m,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Users.Add(user);
            context.Profiles.Add(profile);
            context.Holdings.Add(holding);
            context.Transactions.AddRange(transactionOld, transactionNew);
            context.SIPPlans.AddRange(activeSip, inactiveSip);
            context.PortfolioPerformances.AddRange(perfOld, perfLatest);

            await context.SaveChangesAsync();

            return (user, profile, instrument);
        }

        [Fact]
        public async Task GetHomeDataAsync_WithValidUser_ReturnsFullPortfolio()
        {
            var context = CreateInMemoryDbContext();
            var (user, profile, instrument) = await SeedUserWithPortfolioAsync(context);
            var service = new HomeService(context);

            var result = await service.GetHomeDataAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(200, result.StatusCode);
            Assert.NotNull(result.Data);

            Assert.Equal(user.Id, result.Data!.User.Id);
            Assert.Equal(user.Email, result.Data.User.Email);
            Assert.Equal(user.Name, result.Data.User.Name);
            Assert.True(result.Data.User.IsVerified);
            Assert.True(result.Data.User.IsActive);

            Assert.Single(result.Data.Profiles);
            var profileDto = result.Data.Profiles[0];
            Assert.Equal(profile.Id, profileDto.Id);
            Assert.Equal("Main Portfolio", profileDto.Name);
            Assert.Equal("USD", profileDto.BaseCurrency);

            Assert.Single(profileDto.Holdings);
            var holdingDto = profileDto.Holdings[0];
            Assert.Equal(instrument.Id, holdingDto.InstrumentId);
            Assert.Equal("ACME", holdingDto.InstrumentSymbol);
            Assert.Equal("Equity", holdingDto.AssetType);
            Assert.Equal(10m, holdingDto.Quantity);
            Assert.Equal(1200m, holdingDto.MarketValue);
            Assert.Equal(200m, holdingDto.UnrealizedPnL);

            Assert.Equal(2, profileDto.Transactions.Count);
            Assert.Equal("Dividend", profileDto.Transactions[0].Type);
            Assert.Equal("Buy", profileDto.Transactions[1].Type);

            Assert.Equal(2, profileDto.SIPPlans.Count);

            Assert.NotNull(profileDto.LatestPerformance);
            Assert.Equal(0.18m, profileDto.LatestPerformance!.XIRR);
            Assert.Equal(1200m, profileDto.LatestPerformance.CurrentValue);
        }

        [Fact]
        public async Task GetHomeDataAsync_SummaryAggregatesCorrectly()
        {
            var context = CreateInMemoryDbContext();
            var (user, _, _) = await SeedUserWithPortfolioAsync(context);
            var service = new HomeService(context);

            var result = await service.GetHomeDataAsync(user.Id);

            Assert.True(result.IsSuccess);
            var summary = result.Data!.Summary;
            Assert.Equal(1, summary.ProfileCount);
            Assert.Equal(1, summary.HoldingCount);
            Assert.Equal(2, summary.TransactionCount);
            Assert.Equal(1, summary.ActiveSIPCount);
            Assert.Equal(1000m, summary.TotalInvestment);
            Assert.Equal(1200m, summary.TotalMarketValue);
            Assert.Equal(200m, summary.TotalUnrealizedPnL);
        }

        [Fact]
        public async Task GetHomeDataAsync_WithEmptyGuid_ReturnsBadRequest()
        {
            var context = CreateInMemoryDbContext();
            var service = new HomeService(context);

            var result = await service.GetHomeDataAsync(Guid.Empty);

            Assert.True(result.IsFailure);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("required", result.Message);
        }

        [Fact]
        public async Task GetHomeDataAsync_WithUnknownUser_ReturnsNotFound()
        {
            var context = CreateInMemoryDbContext();
            var service = new HomeService(context);

            var result = await service.GetHomeDataAsync(Guid.NewGuid());

            Assert.True(result.IsFailure);
            Assert.Equal(404, result.StatusCode);
            Assert.Contains("not found", result.Message);
        }

        [Fact]
        public async Task GetHomeDataAsync_UserWithNoProfiles_ReturnsEmptyAggregates()
        {
            var context = CreateInMemoryDbContext();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "solo@example.com",
                Name = "Solo",
                PasswordHash = "hash",
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var service = new HomeService(context);

            var result = await service.GetHomeDataAsync(user.Id);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data!.Profiles);
            Assert.Equal(0, result.Data.Summary.ProfileCount);
            Assert.Equal(0, result.Data.Summary.HoldingCount);
            Assert.Equal(0, result.Data.Summary.TransactionCount);
            Assert.Equal(0, result.Data.Summary.ActiveSIPCount);
            Assert.Equal(0m, result.Data.Summary.TotalInvestment);
            Assert.Equal(0m, result.Data.Summary.TotalMarketValue);
            Assert.Equal(0m, result.Data.Summary.TotalUnrealizedPnL);
        }

        [Fact]
        public async Task GetHomeDataAsync_DoesNotLeakOtherUsersData()
        {
            var context = CreateInMemoryDbContext();
            var (ownerUser, _, instrument) = await SeedUserWithPortfolioAsync(context);

            var otherUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "other@example.com",
                Name = "Other",
                PasswordHash = "hash",
                IsVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var otherProfile = new Profile
            {
                Id = Guid.NewGuid(),
                UserId = otherUser.Id,
                Name = "Other Portfolio",
                BaseCurrency = "EUR",
                Description = "Should not appear",
                CreatedAt = DateTime.UtcNow
            };

            var otherHolding = new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = otherProfile.Id,
                InstrumentId = instrument.Id,
                Quantity = 999m,
                AvgPrice = 50m,
                CurrentPrice = 60m,
                MarketValue = 59940m,
                UnrealizedPnL = 9990m,
                LastUpdated = DateTime.UtcNow
            };

            context.Users.Add(otherUser);
            context.Profiles.Add(otherProfile);
            context.Holdings.Add(otherHolding);
            await context.SaveChangesAsync();

            var service = new HomeService(context);

            var result = await service.GetHomeDataAsync(ownerUser.Id);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Data!.Profiles);
            Assert.Equal("Main Portfolio", result.Data.Profiles[0].Name);
            Assert.Equal(1200m, result.Data.Summary.TotalMarketValue);
            Assert.DoesNotContain(result.Data.Profiles, p => p.BaseCurrency == "EUR");
        }

        [Fact]
        public async Task GetHomeDataAsync_TransactionsOrderedDescendingByDate()
        {
            var context = CreateInMemoryDbContext();
            var (user, _, _) = await SeedUserWithPortfolioAsync(context);
            var service = new HomeService(context);

            var result = await service.GetHomeDataAsync(user.Id);

            Assert.True(result.IsSuccess);
            var txs = result.Data!.Profiles[0].Transactions;
            Assert.Equal(2, txs.Count);
            Assert.True(txs[0].TransactionDate >= txs[1].TransactionDate);
        }

        [Fact]
        public async Task GetHomeDataAsync_LatestPerformance_PicksMostRecentDate()
        {
            var context = CreateInMemoryDbContext();
            var (user, _, _) = await SeedUserWithPortfolioAsync(context);
            var service = new HomeService(context);

            var result = await service.GetHomeDataAsync(user.Id);

            Assert.True(result.IsSuccess);
            var latest = result.Data!.Profiles[0].LatestPerformance;
            Assert.NotNull(latest);
            Assert.Equal(1200m, latest!.CurrentValue);
            Assert.Equal(20m, latest.DayChange);
        }

        [Fact]
        public async Task GetHomeDataAsync_NoWrites_UserRowUnchanged()
        {
            var context = CreateInMemoryDbContext();
            var (user, _, _) = await SeedUserWithPortfolioAsync(context);
            var service = new HomeService(context);

            var originalLastLogin = user.LastLoginAt;
            var originalCreatedAt = user.CreatedAt;

            await service.GetHomeDataAsync(user.Id);

            var fresh = await context.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
            Assert.Equal(originalLastLogin, fresh.LastLoginAt);
            Assert.Equal(originalCreatedAt, fresh.CreatedAt);

            var holdings = await context.Holdings.AsNoTracking().ToListAsync();
            Assert.Single(holdings);
            Assert.Equal(1200m, holdings[0].MarketValue);
        }
    }
}
