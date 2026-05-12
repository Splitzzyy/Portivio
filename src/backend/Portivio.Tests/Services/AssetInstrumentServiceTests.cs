using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portivio.Application.DTOs.Asset;
using Portivio.Application.DTOs.Holding;
using Portivio.Application.DTOs.MarketData;
using Portivio.Application.Results;
using Portivio.Application.Services;
using Portivio.Application.Services.Authorization;
using Portivio.Application.Services.MarketData;
using Portivio.Application.Services.Strategies;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;
using System.Text.Json;
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

        private sealed class NoopThrottle : IRefreshThrottle
        {
            public Task DelayAsync(TimeSpan delay, CancellationToken ct) => Task.CompletedTask;
        }

        private static IMarketDataService NoopMarketData()
        {
            var mock = new Mock<IMarketDataService>();
            mock.Setup(m => m.SyncAllNavsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SyncSummaryResponse>.Success(new SyncSummaryResponse()));
            mock.Setup(m => m.SyncStockPriceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<StockPriceResponse>.Success(new StockPriceResponse()));
            return mock.Object;
        }

        private static IGoldRateProvider NoopGoldRate()
        {
            var mock = new Mock<IGoldRateProvider>();
            mock.Setup(m => m.GetRatePerGramAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);
            return mock.Object;
        }

        private static ILivePriceApiStockProvider NoopLivePrice()
        {
            var mock = new Mock<ILivePriceApiStockProvider>();
            mock.Setup(m => m.GetQuoteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((StockQuote?)null);
            return mock.Object;
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
            var recalc = new HoldingRecalculationService(
                context,
                resolver,
                profileAccess,
                NoopMarketData(),
                NoopGoldRate(),
                NoopLivePrice(),
                new NoopThrottle(),
                Mock.Of<ILogger<HoldingRecalculationService>>());
            return new AssetInstrumentService(context, ingest, profileAccess, recalc);
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

        [Fact]
        public async Task UpdateStockAsync_UpdatesInstrumentTransactionAndHolding()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile) = SeedUserAndProfile(context);
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Equity" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Category = Portivio.Domain.Enums.AssetCategory.Equity,
                Name = "Old Name",
                Symbol = "NSE:OLD",
                Currency = "INR",
                PriceSource = Portivio.Domain.Enums.PriceSource.LivePriceApi
            };
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = Portivio.Domain.Enums.TransactionType.Buy,
                Quantity = 2m,
                Price = 100m,
                Amount = 200m,
                TransactionDate = DateTime.UtcNow.AddDays(-10),
                Notes = "old",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-10)
            };
            var seededHolding = new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 2m,
                AvgPrice = 100m,
                CurrentPrice = 100m,
                MarketValue = 200m,
                UnrealizedPnL = 0m,
                LastUpdated = DateTime.UtcNow.AddDays(-10)
            };
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Transactions.Add(transaction);
            context.Holdings.Add(seededHolding);
            await context.SaveChangesAsync();

            var service = BuildService(context);
            var result = await service.UpdateStockAsync(user.Id, profile.Id, instrument.Id, new UpdateStockRequest
            {
                Name = "Tata Consultancy Services",
                Symbol = "TCS",
                Exchange = "BSE",
                Isin = "INE467B01029",
                Quantity = 5m,
                Price = 4127.10m,
                Date = DateTime.UtcNow.AddDays(-3),
                Notes = "updated"
            });

            Assert.True(result.IsSuccess);
            var updatedInstrument = await context.Instruments.FirstAsync(i => i.Id == instrument.Id);
            Assert.Equal("Tata Consultancy Services", updatedInstrument.Name);
            Assert.Equal("BSE:TCS", updatedInstrument.Symbol);
            Assert.Equal("INE467B01029", updatedInstrument.Isin);
            Assert.Equal("TCS.BO", updatedInstrument.PriceSourceKey);

            var updatedTx = await context.Transactions.FirstAsync(t => t.Id == transaction.Id);
            Assert.Equal(5m, updatedTx.Quantity);
            Assert.Equal(4127.10m, updatedTx.Price);
            Assert.Equal(20635.50m, updatedTx.Amount);
            Assert.Equal("updated", updatedTx.Notes);

            var holding = await context.Holdings.FirstAsync(h => h.ProfileId == profile.Id && h.InstrumentId == instrument.Id);
            Assert.Equal(5m, holding.Quantity);
            Assert.Equal(4127.10m, holding.AvgPrice);
        }

        [Fact]
        public async Task UpdateMutualFundAsync_UpdatesMetadataAndHolding()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile) = SeedUserAndProfile(context);
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Mutual Fund" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Category = Portivio.Domain.Enums.AssetCategory.MutualFund,
                Name = "Old Fund",
                Symbol = "OLD",
                Currency = "INR",
                PriceSource = Portivio.Domain.Enums.PriceSource.AmfiNav
            };
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = Portivio.Domain.Enums.TransactionType.Buy,
                Quantity = 10m,
                Price = 100m,
                Amount = 1000m,
                TransactionDate = DateTime.UtcNow.AddDays(-20),
                Notes = "",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-20),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-20)
            };
            var seededHolding = new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 10m,
                AvgPrice = 100m,
                CurrentPrice = 100m,
                MarketValue = 1000m,
                UnrealizedPnL = 0m,
                LastUpdated = DateTime.UtcNow.AddDays(-20)
            };
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Transactions.Add(transaction);
            context.Holdings.Add(seededHolding);
            await context.SaveChangesAsync();

            var service = BuildService(context);
            var result = await service.UpdateMutualFundAsync(user.Id, profile.Id, instrument.Id, new UpdateMutualFundRequest
            {
                SchemeName = "PPFAS Flexi Cap Fund",
                SchemeCode = "120503",
                Isin = "INF846K01DP8",
                Plan = "Direct",
                Option = "Growth",
                Units = 25m,
                NavPerUnit = 86.42m,
                Date = DateTime.UtcNow.AddDays(-2),
                Notes = "rebalanced"
            });

            Assert.True(result.IsSuccess);
            var updatedInstrument = await context.Instruments.FirstAsync(i => i.Id == instrument.Id);
            Assert.Equal("PPFAS Flexi Cap Fund", updatedInstrument.Name);
            Assert.Equal("INF846K01DP8", updatedInstrument.Symbol);
            Assert.Equal("120503", updatedInstrument.PriceSourceKey);
            Assert.True(updatedInstrument.Metadata!.RootElement.TryGetProperty("plan", out var plan));
            Assert.Equal("Direct", plan.GetString());

            var updatedTx = await context.Transactions.FirstAsync(t => t.Id == transaction.Id);
            Assert.Equal(25m, updatedTx.Quantity);
            Assert.Equal(86.42m, updatedTx.Price);
            Assert.Equal(2160.5m, updatedTx.Amount);

            var holding = await context.Holdings.FirstAsync(h => h.ProfileId == profile.Id && h.InstrumentId == instrument.Id);
            Assert.Equal(25m, holding.Quantity);
            Assert.Equal(86.42m, holding.AvgPrice);
        }

        [Fact]
        public async Task UpdateGoldAsync_UpdatesInstrumentAndHolding()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile) = SeedUserAndProfile(context);
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Gold" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Category = Portivio.Domain.Enums.AssetCategory.Gold,
                Name = "Old Gold",
                Symbol = "GOLD:24K:COIN",
                Currency = "INR",
                PriceSource = Portivio.Domain.Enums.PriceSource.Manual
            };
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = Portivio.Domain.Enums.TransactionType.Buy,
                Quantity = 10m,
                Price = 6000m,
                Amount = 60000m,
                TransactionDate = DateTime.UtcNow.AddDays(-30),
                Notes = "",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-30)
            };
            var seededHolding = new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 10m,
                AvgPrice = 6000m,
                CurrentPrice = 6000m,
                MarketValue = 60000m,
                UnrealizedPnL = 0m,
                LastUpdated = DateTime.UtcNow.AddDays(-30)
            };
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Transactions.Add(transaction);
            context.Holdings.Add(seededHolding);
            await context.SaveChangesAsync();

            var service = BuildService(context);
            var result = await service.UpdateGoldAsync(user.Id, profile.Id, instrument.Id, new UpdateGoldRequest
            {
                Form = "Digital",
                Purity = "22K",
                WeightGrams = 8m,
                RatePerGram = 7300m,
                MakingChargesInr = 200m,
                Date = DateTime.UtcNow.AddDays(-1),
                Notes = "updated gold"
            });

            Assert.True(result.IsSuccess);
            var updatedInstrument = await context.Instruments.FirstAsync(i => i.Id == instrument.Id);
            Assert.Equal("Gold 22K Digital", updatedInstrument.Name);
            Assert.Equal("GOLD:22K:DIGITAL", updatedInstrument.Symbol);
            Assert.True(updatedInstrument.Metadata!.RootElement.TryGetProperty("makingChargesInr", out var charges));
            Assert.Equal(200m, charges.GetDecimal());

            var updatedTx = await context.Transactions.FirstAsync(t => t.Id == transaction.Id);
            Assert.Equal(8m, updatedTx.Quantity);
            Assert.Equal(7325m, updatedTx.Price);
            Assert.Equal(58600m, updatedTx.Amount);

            var holding = await context.Holdings.FirstAsync(h => h.ProfileId == profile.Id && h.InstrumentId == instrument.Id);
            Assert.Equal(8m, holding.Quantity);
            Assert.Equal(7325m, holding.AvgPrice);
        }

        [Fact]
        public async Task UpdatePpfAsync_UpdatesInstrumentAndHolding()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile) = SeedUserAndProfile(context);
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "PPF" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Category = Portivio.Domain.Enums.AssetCategory.Ppf,
                Name = "PPF - PPF001",
                Symbol = "PPF:PPF001",
                Currency = "INR",
                PriceSource = Portivio.Domain.Enums.PriceSource.AccrualFormula,
                Metadata = JsonDocument.Parse("{\"accountNo\":\"PPF001\",\"openedOn\":\"2020-04-01\",\"lockInEndsOn\":\"2035-04-01\",\"currentRate\":7.1}")
            };
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = Portivio.Domain.Enums.TransactionType.Contribution,
                Quantity = 1m,
                Price = 50000m,
                Amount = 50000m,
                TransactionDate = DateTime.UtcNow.AddYears(-3),
                Notes = "PPF opening contribution",
                CreatedAtUtc = DateTime.UtcNow.AddYears(-3),
                UpdatedAtUtc = DateTime.UtcNow.AddYears(-3)
            };
            var seededHolding = new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 1m,
                AvgPrice = 50000m,
                CurrentPrice = 50000m,
                MarketValue = 50000m,
                UnrealizedPnL = 0m,
                AccruedInterest = 0m,
                LastUpdated = DateTime.UtcNow.AddYears(-3)
            };
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Transactions.Add(transaction);
            context.Holdings.Add(seededHolding);
            await context.SaveChangesAsync();

            var service = BuildService(context);
            var result = await service.UpdatePpfAsync(user.Id, profile.Id, instrument.Id, new UpdatePpfRequest
            {
                AccountNo = "PPF999",
                OpenedOn = new DateTime(2018, 4, 1),
                CurrentRatePercent = 7.5m,
                InitialContribution = 75000m,
                ContributionDate = DateTime.UtcNow.AddYears(-2),
                Notes = "ppf updated"
            });

            Assert.True(result.IsSuccess);
            var updatedInstrument = await context.Instruments.FirstAsync(i => i.Id == instrument.Id);
            Assert.Equal("PPF - PPF999", updatedInstrument.Name);
            Assert.Equal("PPF:PPF999", updatedInstrument.Symbol);
            Assert.True(updatedInstrument.Metadata!.RootElement.TryGetProperty("currentRate", out var rate));
            Assert.Equal(7.5m, rate.GetDecimal());

            var updatedTx = await context.Transactions.FirstAsync(t => t.Id == transaction.Id);
            Assert.Equal(75000m, updatedTx.Amount);
            Assert.Equal("ppf updated", updatedTx.Notes);

            var holding = await context.Holdings.FirstAsync(h => h.ProfileId == profile.Id && h.InstrumentId == instrument.Id);
            Assert.Equal(1m, holding.Quantity);
            Assert.True(holding.AccruedInterest >= 0m);
        }

        [Fact]
        public async Task UpdateFixedDepositAsync_UpdatesSyntheticSymbolAndHolding()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile) = SeedUserAndProfile(context);
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Fixed Deposit" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Category = Portivio.Domain.Enums.AssetCategory.FixedDeposit,
                Name = "FD - HDFC Bank",
                Symbol = "FD:HDFC BANK:ABCD1234",
                Currency = "INR",
                PriceSource = Portivio.Domain.Enums.PriceSource.AccrualFormula,
                Metadata = JsonDocument.Parse("{\"bank\":\"HDFC Bank\",\"accountNo\":null,\"principal\":100000,\"rate\":7.0,\"compounding\":\"Quarterly\",\"payoutFrequency\":\"OnMaturity\",\"startDate\":\"2024-01-01\",\"maturityDate\":\"2026-01-01\",\"prematurePenaltyPct\":0}")
            };
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = Portivio.Domain.Enums.TransactionType.Deposit,
                Quantity = 1m,
                Price = 100000m,
                Amount = 100000m,
                TransactionDate = DateTime.UtcNow.AddYears(-1),
                Notes = "",
                CreatedAtUtc = DateTime.UtcNow.AddYears(-1),
                UpdatedAtUtc = DateTime.UtcNow.AddYears(-1)
            };
            var seededHolding = new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 1m,
                AvgPrice = 100000m,
                CurrentPrice = 100000m,
                MarketValue = 100000m,
                UnrealizedPnL = 0m,
                AccruedInterest = 0m,
                LastUpdated = DateTime.UtcNow.AddYears(-1)
            };
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Transactions.Add(transaction);
            context.Holdings.Add(seededHolding);
            await context.SaveChangesAsync();

            var service = BuildService(context);
            var result = await service.UpdateFixedDepositAsync(user.Id, profile.Id, instrument.Id, new UpdateFixedDepositRequest
            {
                Bank = "HDFC Bank",
                AccountNo = null,
                Principal = 125000m,
                RatePercent = 7.25m,
                Compounding = "Quarterly",
                PayoutFrequency = "OnMaturity",
                StartDate = DateTime.UtcNow.AddYears(-2),
                MaturityDate = DateTime.UtcNow.AddYears(1),
                PrematurePenaltyPct = 1.5m,
                Notes = "updated fd"
            });

            Assert.True(result.IsSuccess);
            var updatedInstrument = await context.Instruments.FirstAsync(i => i.Id == instrument.Id);
            Assert.Equal("FD - HDFC Bank", updatedInstrument.Name);
            Assert.Equal("FD:HDFC BANK:ABCD1234", updatedInstrument.Symbol);
            Assert.True(updatedInstrument.Metadata!.RootElement.TryGetProperty("principal", out var principal));
            Assert.Equal(125000m, principal.GetDecimal());

            var updatedTx = await context.Transactions.FirstAsync(t => t.Id == transaction.Id);
            Assert.Equal(125000m, updatedTx.Amount);
            Assert.Equal("updated fd", updatedTx.Notes);

            var holding = await context.Holdings.FirstAsync(h => h.ProfileId == profile.Id && h.InstrumentId == instrument.Id);
            Assert.Equal(1m, holding.Quantity);
            Assert.True(holding.CurrentPrice >= 125000m);
        }

        [Fact]
        public async Task UpdateRecurringDepositAsync_UpdatesInstrumentAndHolding()
        {
            using var context = CreateInMemoryDbContext();
            var (user, profile) = SeedUserAndProfile(context);
            var assetType = new AssetType { Id = Guid.NewGuid(), Name = "Recurring Deposit" };
            var instrument = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetType.Id,
                Category = Portivio.Domain.Enums.AssetCategory.RecurringDeposit,
                Name = "RD - ICICI",
                Symbol = "RD:ICICI BANK:RD001",
                Currency = "INR",
                PriceSource = Portivio.Domain.Enums.PriceSource.AccrualFormula,
                Metadata = JsonDocument.Parse("{\"bank\":\"ICICI Bank\",\"accountNo\":\"RD001\",\"monthly\":5000,\"rate\":6.5,\"startDate\":\"2024-01-01\",\"tenureMonths\":12}")
            };
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Type = Portivio.Domain.Enums.TransactionType.Contribution,
                Quantity = 1m,
                Price = 5000m,
                Amount = 5000m,
                TransactionDate = DateTime.UtcNow.AddMonths(-3),
                Notes = "",
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-3),
                UpdatedAtUtc = DateTime.UtcNow.AddMonths(-3)
            };
            var seededHolding = new Holding
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                InstrumentId = instrument.Id,
                Quantity = 1m,
                AvgPrice = 5000m,
                CurrentPrice = 5000m,
                MarketValue = 5000m,
                UnrealizedPnL = 0m,
                AccruedInterest = 0m,
                LastUpdated = DateTime.UtcNow.AddMonths(-3)
            };
            context.AssetTypes.Add(assetType);
            context.Instruments.Add(instrument);
            context.Transactions.Add(transaction);
            context.Holdings.Add(seededHolding);
            await context.SaveChangesAsync();

            var service = BuildService(context);
            var result = await service.UpdateRecurringDepositAsync(user.Id, profile.Id, instrument.Id, new UpdateRecurringDepositRequest
            {
                Bank = "ICICI Bank",
                AccountNo = "RD002",
                MonthlyAmount = 6500m,
                RatePercent = 7.1m,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                TenureMonths = 18,
                Notes = "updated rd"
            });

            Assert.True(result.IsSuccess);
            var updatedInstrument = await context.Instruments.FirstAsync(i => i.Id == instrument.Id);
            Assert.Equal("RD - ICICI Bank (RD002)", updatedInstrument.Name);
            Assert.Equal("RD:ICICI BANK:RD002", updatedInstrument.Symbol);
            Assert.True(updatedInstrument.Metadata!.RootElement.TryGetProperty("monthly", out var monthly));
            Assert.Equal(6500m, monthly.GetDecimal());

            var updatedTx = await context.Transactions.FirstAsync(t => t.Id == transaction.Id);
            Assert.Equal(6500m, updatedTx.Amount);
            Assert.Equal("updated rd", updatedTx.Notes);

            var holding = await context.Holdings.FirstAsync(h => h.ProfileId == profile.Id && h.InstrumentId == instrument.Id);
            Assert.Equal(1m, holding.Quantity);
            Assert.True(holding.CurrentPrice >= 6500m);
        }
    }
}
