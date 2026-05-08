using Microsoft.EntityFrameworkCore;
using Portivio.Application.DTOs.Asset;
using Portivio.Application.Services;
using Portivio.Application.Services.Authorization;
using Portivio.Application.Services.Strategies;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
using Xunit;

namespace Portivio.Tests.Services
{
    public class AssetInstrumentServiceTests
    {
        private static PortivioDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<PortivioDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PortivioDbContext(options);
        }

        private static (User user, Profile profile) SeedUserAndProfile(PortivioDbContext context)
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
            context.SaveChanges();
            return (user, profile);
        }

        private static AssetInstrumentService BuildService(PortivioDbContext context)
        {
            var strategies = new List<IAssetStrategy>
            {
                new EquityStrategy(context),
                new MutualFundStrategy(context),
                new GoldStrategy(context),
                new FixedDepositStrategy(context),
                new RecurringDepositStrategy(context),
                new PpfStrategy(context)
            };
            var resolver = new AssetStrategyResolver(strategies);
            var profileAccess = new ProfileAccessGuard(context);
            var ingest = new TransactionIngestService(context, profileAccess, resolver);
            return new AssetInstrumentService(context, ingest, profileAccess);
        }

        [Fact]
        public async Task AddFixedDepositAsync_WithoutAccountNo_CreatesInstrument_WithSyntheticSymbol()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile) = SeedUserAndProfile(context);
            var service = BuildService(context);

            var result = await service.AddFixedDepositAsync(user.Id, profile.Id, new AddFixedDepositRequest
            {
                Bank = "HDFC Bank",
                AccountNo = null,
                Principal = 100_000m,
                RatePercent = 7.0m,
                Compounding = "Quarterly",
                PayoutFrequency = "OnMaturity",
                StartDate = DateTime.UtcNow.Date.AddYears(-1),
                MaturityDate = DateTime.UtcNow.Date.AddYears(1)
            });

            Assert.True(result.IsSuccess);
            var instrument = await context.Instruments.FirstAsync(i => i.Id == result.Data!.InstrumentId);
            Assert.StartsWith("FD:HDFC BANK:", instrument.Symbol);
            // Slot is 8 hex chars after the second colon.
            var slot = instrument.Symbol.Split(':')[2];
            Assert.Equal(8, slot.Length);
            Assert.Matches("^[0-9A-F]{8}$", slot);
            Assert.Equal("FD - HDFC Bank", instrument.Name);
            // Metadata.accountNo is null (not the slot).
            Assert.NotNull(instrument.Metadata);
            Assert.True(instrument.Metadata!.RootElement.TryGetProperty("accountNo", out var acct));
            Assert.Equal(System.Text.Json.JsonValueKind.Null, acct.ValueKind);
        }

        [Fact]
        public async Task AddFixedDepositAsync_TwoAdditionsSameBank_NoAccountNo_AreDistinctInstruments()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile) = SeedUserAndProfile(context);
            var service = BuildService(context);

            var first = await service.AddFixedDepositAsync(user.Id, profile.Id, new AddFixedDepositRequest
            {
                Bank = "SBI",
                AccountNo = null,
                Principal = 50_000m,
                RatePercent = 6.5m,
                Compounding = "Quarterly",
                PayoutFrequency = "OnMaturity",
                StartDate = DateTime.UtcNow.Date.AddYears(-1),
                MaturityDate = DateTime.UtcNow.Date.AddYears(1)
            });
            var second = await service.AddFixedDepositAsync(user.Id, profile.Id, new AddFixedDepositRequest
            {
                Bank = "SBI",
                AccountNo = "",          // explicit empty string, equally treated as missing
                Principal = 75_000m,
                RatePercent = 6.5m,
                Compounding = "Quarterly",
                PayoutFrequency = "OnMaturity",
                StartDate = DateTime.UtcNow.Date.AddYears(-1),
                MaturityDate = DateTime.UtcNow.Date.AddYears(1)
            });

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.NotEqual(first.Data!.InstrumentId, second.Data!.InstrumentId);
            Assert.NotEqual(first.Data!.Symbol, second.Data!.Symbol);
            Assert.Equal(2, await context.Instruments.CountAsync(i => EF.Property<string>(i, "Symbol").StartsWith("FD:SBI:")));
        }

        [Fact]
        public async Task AddRecurringDepositAsync_WithoutAccountNo_CreatesInstrument()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile) = SeedUserAndProfile(context);
            var service = BuildService(context);

            var result = await service.AddRecurringDepositAsync(user.Id, profile.Id, new AddRecurringDepositRequest
            {
                Bank = "ICICI Bank",
                AccountNo = null,
                MonthlyAmount = 5_000m,
                RatePercent = 7.25m,
                StartDate = DateTime.UtcNow.Date.AddMonths(-1),
                TenureMonths = 24
            });

            Assert.True(result.IsSuccess);
            var instrument = await context.Instruments.FirstAsync(i => i.Id == result.Data!.InstrumentId);
            Assert.StartsWith("RD:ICICI BANK:", instrument.Symbol);
            Assert.Equal("RD - ICICI Bank", instrument.Name);
            Assert.Matches("^[0-9A-F]{8}$", instrument.Symbol.Split(':')[2]);
        }

        [Fact]
        public async Task AddFixedDepositAsync_WithAccountNo_PreservesExistingSymbolFormat()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile) = SeedUserAndProfile(context);
            var service = BuildService(context);

            var result = await service.AddFixedDepositAsync(user.Id, profile.Id, new AddFixedDepositRequest
            {
                Bank = "HDFC Bank",
                AccountNo = "FDR-001",
                Principal = 100_000m,
                RatePercent = 7.0m,
                Compounding = "Quarterly",
                PayoutFrequency = "OnMaturity",
                StartDate = DateTime.UtcNow.Date.AddYears(-1),
                MaturityDate = DateTime.UtcNow.Date.AddYears(1)
            });

            Assert.True(result.IsSuccess);
            var instrument = await context.Instruments.FirstAsync(i => i.Id == result.Data!.InstrumentId);
            Assert.Equal("FD:HDFC BANK:FDR-001", instrument.Symbol);
            Assert.Equal("FD - HDFC Bank (FDR-001)", instrument.Name);
            Assert.True(instrument.Metadata!.RootElement.TryGetProperty("accountNo", out var acct));
            Assert.Equal("FDR-001", acct.GetString());
        }
    }
}
