using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portivio.Application.DTOs.Asset;
using Portivio.Application.Results;
using Portivio.Application.Services.Authorization;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using System.Text.Json;

namespace Portivio.Application.Services
{
    public interface IAssetInstrumentService
    {
        Task<Result<AssetIngestResponse>> AddMutualFundAsync(Guid userId, Guid profileId, AddMutualFundRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddFixedDepositAsync(Guid userId, Guid profileId, AddFixedDepositRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddRecurringDepositAsync(Guid userId, Guid profileId, AddRecurringDepositRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddPpfAsync(Guid userId, Guid profileId, AddPpfRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddPpfContributionAsync(Guid userId, Guid profileId, AddPpfContributionRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddGoldAsync(Guid userId, Guid profileId, AddGoldRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddStockAsync(Guid userId, Guid profileId, AddStockRequest req, CancellationToken ct = default);
    }

    public class AssetInstrumentService : IAssetInstrumentService
    {
        private readonly PortivioDbContext _context;
        private readonly ITransactionIngestService _ingest;
        private readonly IProfileAccessGuard _profileAccess;

        public AssetInstrumentService(PortivioDbContext context, ITransactionIngestService ingest, IProfileAccessGuard profileAccess)
        {
            _context = context;
            _ingest = ingest;
            _profileAccess = profileAccess;
        }

        public async Task<Result<AssetIngestResponse>> AddMutualFundAsync(Guid userId, Guid profileId, AddMutualFundRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.SchemeName))
                return Result<AssetIngestResponse>.BadRequest("SchemeName is required");
            if (string.IsNullOrWhiteSpace(req.SchemeCode))
                return Result<AssetIngestResponse>.BadRequest("SchemeCode is required");
            if (req.Units <= 0)
                return Result<AssetIngestResponse>.BadRequest("Units must be greater than zero");
            if (req.NavPerUnit <= 0)
                return Result<AssetIngestResponse>.BadRequest("NAV per unit must be greater than zero");

            var assetType = await GetOrCreateAssetTypeAsync("Mutual Fund", ct);
            var symbol = req.Isin ?? req.SchemeCode.Trim().ToUpperInvariant();

            var instrument = await GetOrCreateInstrumentAsync(
                name: req.SchemeName.Trim(),
                symbol: symbol,
                isin: req.Isin?.Trim().ToUpperInvariant(),
                currency: "INR",
                assetTypeId: assetType.Id,
                category: AssetCategory.MutualFund,
                priceSource: PriceSource.AmfiNav,
                priceSourceKey: req.SchemeCode.Trim(),
                metadata: JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    schemeCode = req.SchemeCode.Trim(),
                    isin = req.Isin?.Trim(),
                    plan = req.Plan?.Trim(),
                    option = req.Option?.Trim()
                })),
                ct);

            var cmd = new TransactionCommand(
                ProfileId: profileId,
                InstrumentId: instrument.Id,
                Type: TransactionType.Buy,
                Quantity: req.Units,
                Price: req.NavPerUnit,
                Amount: req.Units * req.NavPerUnit,
                TransactionDateUtc: req.Date,
                Notes: req.Notes,
                ClientTxnId: null);

            var txResult = await _ingest.IngestAsync(userId, cmd, TransactionSource.Manual, ct);
            if (txResult.IsFailure)
                return txResult.ToFailure<AssetIngestResponse>();

            return Result<AssetIngestResponse>.Success(new AssetIngestResponse
            {
                InstrumentId = instrument.Id,
                InstrumentName = instrument.Name,
                Symbol = instrument.Symbol,
                TransactionId = txResult.Data!.Id,
                Message = "Mutual fund investment recorded"
            }, "Mutual fund investment recorded", 201);
        }

        public async Task<Result<AssetIngestResponse>> AddFixedDepositAsync(Guid userId, Guid profileId, AddFixedDepositRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.Bank))
                return Result<AssetIngestResponse>.BadRequest("Bank is required");
            if (req.Principal <= 0)
                return Result<AssetIngestResponse>.BadRequest("Principal must be greater than zero");
            if (req.RatePercent <= 0)
                return Result<AssetIngestResponse>.BadRequest("Rate must be greater than zero");
            if (req.MaturityDate <= req.StartDate)
                return Result<AssetIngestResponse>.BadRequest("MaturityDate must be after StartDate");

            var assetType = await GetOrCreateAssetTypeAsync("Fixed Deposit", ct);
            var hasAccountNo = !string.IsNullOrWhiteSpace(req.AccountNo);
            var accountTrimmed = hasAccountNo ? req.AccountNo!.Trim() : null;
            var symbolSlot = hasAccountNo ? accountTrimmed!.ToUpperInvariant() : GenerateInstrumentSlot();
            var symbol = $"FD:{req.Bank.Trim().ToUpperInvariant()}:{symbolSlot}";
            var name = hasAccountNo
                ? $"FD - {req.Bank.Trim()} ({accountTrimmed})"
                : $"FD - {req.Bank.Trim()}";

            var instrument = await GetOrCreateInstrumentAsync(
                name: name,
                symbol: symbol,
                isin: null,
                currency: "INR",
                assetTypeId: assetType.Id,
                category: AssetCategory.FixedDeposit,
                priceSource: PriceSource.AccrualFormula,
                priceSourceKey: null,
                metadata: JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    bank = req.Bank.Trim(),
                    accountNo = accountTrimmed,
                    principal = req.Principal,
                    rate = req.RatePercent,
                    compounding = req.Compounding,
                    payoutFrequency = req.PayoutFrequency,
                    startDate = req.StartDate.ToString("yyyy-MM-dd"),
                    maturityDate = req.MaturityDate.ToString("yyyy-MM-dd"),
                    prematurePenaltyPct = req.PrematurePenaltyPct
                })),
                ct);

            // 1 unit = 1 FD; price = principal (avg cost = principal)
            var cmd = new TransactionCommand(
                ProfileId: profileId,
                InstrumentId: instrument.Id,
                Type: TransactionType.Deposit,
                Quantity: 1m,
                Price: req.Principal,
                Amount: req.Principal,
                TransactionDateUtc: req.StartDate,
                Notes: req.Notes,
                ClientTxnId: $"fd-open:{instrument.Id}");

            var txResult = await _ingest.IngestAsync(userId, cmd, TransactionSource.Manual, ct);
            if (txResult.IsFailure)
                return txResult.ToFailure<AssetIngestResponse>();

            return Result<AssetIngestResponse>.Success(new AssetIngestResponse
            {
                InstrumentId = instrument.Id,
                InstrumentName = instrument.Name,
                Symbol = instrument.Symbol,
                TransactionId = txResult.Data!.Id,
                Message = "Fixed deposit recorded"
            }, "Fixed deposit recorded", 201);
        }

        public async Task<Result<AssetIngestResponse>> AddRecurringDepositAsync(Guid userId, Guid profileId, AddRecurringDepositRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.Bank))
                return Result<AssetIngestResponse>.BadRequest("Bank is required");
            if (req.MonthlyAmount <= 0)
                return Result<AssetIngestResponse>.BadRequest("MonthlyAmount must be greater than zero");
            if (req.RatePercent <= 0)
                return Result<AssetIngestResponse>.BadRequest("Rate must be greater than zero");
            if (req.TenureMonths <= 0)
                return Result<AssetIngestResponse>.BadRequest("TenureMonths must be greater than zero");

            var assetType = await GetOrCreateAssetTypeAsync("Recurring Deposit", ct);
            var hasAccountNo = !string.IsNullOrWhiteSpace(req.AccountNo);
            var accountTrimmed = hasAccountNo ? req.AccountNo!.Trim() : null;
            var symbolSlot = hasAccountNo ? accountTrimmed!.ToUpperInvariant() : GenerateInstrumentSlot();
            var symbol = $"RD:{req.Bank.Trim().ToUpperInvariant()}:{symbolSlot}";
            var name = hasAccountNo
                ? $"RD - {req.Bank.Trim()} ({accountTrimmed})"
                : $"RD - {req.Bank.Trim()}";

            var instrument = await GetOrCreateInstrumentAsync(
                name: name,
                symbol: symbol,
                isin: null,
                currency: "INR",
                assetTypeId: assetType.Id,
                category: AssetCategory.RecurringDeposit,
                priceSource: PriceSource.AccrualFormula,
                priceSourceKey: null,
                metadata: JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    bank = req.Bank.Trim(),
                    accountNo = accountTrimmed,
                    monthly = req.MonthlyAmount,
                    rate = req.RatePercent,
                    startDate = req.StartDate.ToString("yyyy-MM-dd"),
                    tenureMonths = req.TenureMonths
                })),
                ct);

            var installmentNo = await _context.Transactions
                .CountAsync(t => t.InstrumentId == instrument.Id && t.ProfileId == profileId, ct) + 1;

            var cmd = new TransactionCommand(
                ProfileId: profileId,
                InstrumentId: instrument.Id,
                Type: TransactionType.Contribution,
                Quantity: 1m,
                Price: req.MonthlyAmount,
                Amount: req.MonthlyAmount,
                TransactionDateUtc: req.StartDate,
                Notes: req.Notes ?? $"RD installment #{installmentNo}",
                ClientTxnId: $"rd-contrib:{instrument.Id}:{req.StartDate:yyyyMMdd}");

            var txResult = await _ingest.IngestAsync(userId, cmd, TransactionSource.Manual, ct);
            if (txResult.IsFailure)
                return txResult.ToFailure<AssetIngestResponse>();

            return Result<AssetIngestResponse>.Success(new AssetIngestResponse
            {
                InstrumentId = instrument.Id,
                InstrumentName = instrument.Name,
                Symbol = instrument.Symbol,
                TransactionId = txResult.Data!.Id,
                Message = "Recurring deposit contribution recorded"
            }, "Recurring deposit contribution recorded", 201);
        }

        public async Task<Result<AssetIngestResponse>> AddPpfAsync(Guid userId, Guid profileId, AddPpfRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.AccountNo))
                return Result<AssetIngestResponse>.BadRequest("AccountNo is required");
            if (req.InitialContribution <= 0)
                return Result<AssetIngestResponse>.BadRequest("InitialContribution must be greater than zero");
            if (req.CurrentRatePercent <= 0)
                return Result<AssetIngestResponse>.BadRequest("CurrentRatePercent must be greater than zero");

            var assetType = await GetOrCreateAssetTypeAsync("PPF", ct);
            var symbol = $"PPF:{req.AccountNo.Trim().ToUpperInvariant()}";
            var lockInEndsOn = req.OpenedOn.AddYears(15);

            var instrument = await GetOrCreateInstrumentAsync(
                name: $"PPF - {req.AccountNo.Trim()}",
                symbol: symbol,
                isin: null,
                currency: "INR",
                assetTypeId: assetType.Id,
                category: AssetCategory.Ppf,
                priceSource: PriceSource.AccrualFormula,
                priceSourceKey: null,
                metadata: JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    accountNo = req.AccountNo.Trim(),
                    openedOn = req.OpenedOn.ToString("yyyy-MM-dd"),
                    lockInEndsOn = lockInEndsOn.ToString("yyyy-MM-dd"),
                    currentRate = req.CurrentRatePercent
                })),
                ct);

            var cmd = new TransactionCommand(
                ProfileId: profileId,
                InstrumentId: instrument.Id,
                Type: TransactionType.Contribution,
                Quantity: 1m,
                Price: req.InitialContribution,
                Amount: req.InitialContribution,
                TransactionDateUtc: req.ContributionDate,
                Notes: req.Notes ?? "PPF opening contribution",
                ClientTxnId: $"ppf-contrib:{instrument.Id}:{req.ContributionDate:yyyyMMdd}");

            var txResult = await _ingest.IngestAsync(userId, cmd, TransactionSource.Manual, ct);
            if (txResult.IsFailure)
                return txResult.ToFailure<AssetIngestResponse>();

            return Result<AssetIngestResponse>.Success(new AssetIngestResponse
            {
                InstrumentId = instrument.Id,
                InstrumentName = instrument.Name,
                Symbol = instrument.Symbol,
                TransactionId = txResult.Data!.Id,
                Message = "PPF account recorded"
            }, "PPF account recorded", 201);
        }

        public async Task<Result<AssetIngestResponse>> AddPpfContributionAsync(Guid userId, Guid profileId, AddPpfContributionRequest req, CancellationToken ct = default)
        {
            if (req.Amount <= 0)
                return Result<AssetIngestResponse>.BadRequest("Amount must be greater than zero");

            var instrument = await _context.Instruments
                .FirstOrDefaultAsync(i => i.Id == req.InstrumentId && i.Category == AssetCategory.Ppf, ct);
            if (instrument == null)
                return Result<AssetIngestResponse>.BadRequest("PPF instrument not found");

            var cmd = new TransactionCommand(
                ProfileId: profileId,
                InstrumentId: req.InstrumentId,
                Type: TransactionType.Contribution,
                Quantity: 1m,
                Price: req.Amount,
                Amount: req.Amount,
                TransactionDateUtc: req.Date,
                Notes: req.Notes ?? "PPF contribution",
                ClientTxnId: $"ppf-contrib:{req.InstrumentId}:{req.Date:yyyyMMdd}");

            var txResult = await _ingest.IngestAsync(userId, cmd, TransactionSource.Manual, ct);
            if (txResult.IsFailure)
                return txResult.ToFailure<AssetIngestResponse>();

            return Result<AssetIngestResponse>.Success(new AssetIngestResponse
            {
                InstrumentId = instrument.Id,
                InstrumentName = instrument.Name,
                Symbol = instrument.Symbol,
                TransactionId = txResult.Data!.Id,
                Message = "PPF contribution recorded"
            }, "PPF contribution recorded", 201);
        }

        public async Task<Result<AssetIngestResponse>> AddGoldAsync(Guid userId, Guid profileId, AddGoldRequest req, CancellationToken ct = default)
        {
            if (req.WeightGrams <= 0)
                return Result<AssetIngestResponse>.BadRequest("WeightGrams must be greater than zero");
            if (req.RatePerGram <= 0)
                return Result<AssetIngestResponse>.BadRequest("RatePerGram must be greater than zero");

            var assetType = await GetOrCreateAssetTypeAsync("Gold", ct);
            var purityNorm = req.Purity.Trim().ToUpperInvariant();
            var formNorm = req.Form.Trim().ToUpperInvariant();
            var symbol = $"GOLD:{purityNorm}:{formNorm}";
            var name = $"Gold {req.Purity} {req.Form}";

            var instrument = await GetOrCreateInstrumentAsync(
                name: name,
                symbol: symbol,
                isin: null,
                currency: "INR",
                assetTypeId: assetType.Id,
                category: AssetCategory.Gold,
                priceSource: PriceSource.Manual,
                priceSourceKey: null,
                metadata: JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    form = req.Form.Trim(),
                    purity = purityNorm,
                    makingChargesInr = req.MakingChargesInr
                })),
                ct);

            var totalCost = (req.WeightGrams * req.RatePerGram) + req.MakingChargesInr;

            var cmd = new TransactionCommand(
                ProfileId: profileId,
                InstrumentId: instrument.Id,
                Type: TransactionType.Buy,
                Quantity: req.WeightGrams,
                Price: totalCost / req.WeightGrams,
                Amount: totalCost,
                TransactionDateUtc: req.Date,
                Notes: req.Notes,
                ClientTxnId: null);

            var txResult = await _ingest.IngestAsync(userId, cmd, TransactionSource.Manual, ct);
            if (txResult.IsFailure)
                return txResult.ToFailure<AssetIngestResponse>();

            return Result<AssetIngestResponse>.Success(new AssetIngestResponse
            {
                InstrumentId = instrument.Id,
                InstrumentName = instrument.Name,
                Symbol = instrument.Symbol,
                TransactionId = txResult.Data!.Id,
                Message = "Gold purchase recorded"
            }, "Gold purchase recorded", 201);
        }

        public async Task<Result<AssetIngestResponse>> AddStockAsync(Guid userId, Guid profileId, AddStockRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Result<AssetIngestResponse>.BadRequest("Name is required");
            if (string.IsNullOrWhiteSpace(req.Symbol))
                return Result<AssetIngestResponse>.BadRequest("Symbol is required");
            if (req.Quantity <= 0)
                return Result<AssetIngestResponse>.BadRequest("Quantity must be greater than zero");
            if (req.Price <= 0)
                return Result<AssetIngestResponse>.BadRequest("Price must be greater than zero");

            var assetType = await GetOrCreateAssetTypeAsync("Equity", ct);
            var exchangeNorm = req.Exchange.Trim().ToUpperInvariant();
            var symbolNorm = req.Symbol.Trim().ToUpperInvariant();
            var symbol = $"{exchangeNorm}:{symbolNorm}";

            var instrument = await GetOrCreateInstrumentAsync(
                name: req.Name.Trim(),
                symbol: symbol,
                isin: req.Isin?.Trim().ToUpperInvariant(),
                currency: "INR",
                assetTypeId: assetType.Id,
                category: AssetCategory.Equity,
                priceSource: PriceSource.AlphaVantage,
                priceSourceKey: symbolNorm,
                metadata: JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    exchange = exchangeNorm,
                    isin = req.Isin?.Trim()
                })),
                ct);

            var cmd = new TransactionCommand(
                ProfileId: profileId,
                InstrumentId: instrument.Id,
                Type: TransactionType.Buy,
                Quantity: req.Quantity,
                Price: req.Price,
                Amount: req.Quantity * req.Price,
                TransactionDateUtc: req.Date,
                Notes: req.Notes,
                ClientTxnId: null);

            var txResult = await _ingest.IngestAsync(userId, cmd, TransactionSource.Manual, ct);
            if (txResult.IsFailure)
                return txResult.ToFailure<AssetIngestResponse>();

            return Result<AssetIngestResponse>.Success(new AssetIngestResponse
            {
                InstrumentId = instrument.Id,
                InstrumentName = instrument.Name,
                Symbol = instrument.Symbol,
                TransactionId = txResult.Data!.Id,
                Message = "Stock purchase recorded"
            }, "Stock purchase recorded", 201);
        }

        // Internal-only uniqueness slot for FD/RD when AccountNo is blank.
        // Never displayed to users — it sits inside Instrument.Symbol so the
        // unique index `(AssetTypeId, Symbol)` admits multiple anonymous deposits.
        private static string GenerateInstrumentSlot()
            => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        private async Task<AssetType> GetOrCreateAssetTypeAsync(string name, CancellationToken ct)
        {
            var existing = await _context.AssetTypes.FirstOrDefaultAsync(a => a.Name == name, ct);
            if (existing != null) return existing;
            var created = new AssetType { Id = Guid.NewGuid(), Name = name };
            _context.AssetTypes.Add(created);
            await _context.SaveChangesAsync(ct);
            return created;
        }

        private async Task<Instrument> GetOrCreateInstrumentAsync(
            string name, string symbol, string? isin, string currency,
            Guid assetTypeId, AssetCategory category, PriceSource priceSource,
            string? priceSourceKey, JsonDocument? metadata, CancellationToken ct)
        {
            var normalized = symbol.Trim();
            var existing = await _context.Instruments
                .FirstOrDefaultAsync(i => i.AssetTypeId == assetTypeId && i.Symbol == normalized, ct);
            if (existing != null) return existing;

            var created = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetTypeId,
                Category = category,
                Name = name,
                Symbol = normalized,
                Isin = isin,
                Currency = currency,
                PriceSource = priceSource,
                PriceSourceKey = priceSourceKey,
                Metadata = metadata
            };
            _context.Instruments.Add(created);
            await _context.SaveChangesAsync(ct);
            return created;
        }

        // Wraps an asset ingestion operation in a single database transaction so that
        // GetOrCreate* saves and the IngestAsync call are committed or rolled back together.
        private async Task<Result<AssetIngestResponse>> InTransactionAsync(
            Func<CancellationToken, Task<Result<AssetIngestResponse>>> operation,
            CancellationToken ct)
        {
            if (!_context.Database.IsRelational())
                return await operation(ct);

            // Only begin a transaction if one is not already active.
            var ownsTx = _context.Database.CurrentTransaction is null;
            IDbContextTransaction? tx = null;
            if (ownsTx)
                tx = await _context.Database.BeginTransactionAsync(ct);

            try
            {
                var result = await operation(ct);
                if (result.IsFailure)
                {
                    if (tx is not null) await tx.RollbackAsync(ct);
                    return result;
                }
                if (tx is not null) await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                if (tx is not null) await tx.RollbackAsync(ct);
                throw;
            }
            finally
            {
                if (tx is not null) await tx.DisposeAsync();
            }
        }
    }
}
