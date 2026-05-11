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
        Task<Result<AssetIngestResponse>> UpdateMutualFundAsync(Guid userId, Guid profileId, Guid instrumentId, UpdateMutualFundRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddFixedDepositAsync(Guid userId, Guid profileId, AddFixedDepositRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> UpdateFixedDepositAsync(Guid userId, Guid profileId, Guid instrumentId, UpdateFixedDepositRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddRecurringDepositAsync(Guid userId, Guid profileId, AddRecurringDepositRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> UpdateRecurringDepositAsync(Guid userId, Guid profileId, Guid instrumentId, UpdateRecurringDepositRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddPpfAsync(Guid userId, Guid profileId, AddPpfRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> UpdatePpfAsync(Guid userId, Guid profileId, Guid instrumentId, UpdatePpfRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddPpfContributionAsync(Guid userId, Guid profileId, AddPpfContributionRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddGoldAsync(Guid userId, Guid profileId, AddGoldRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> UpdateGoldAsync(Guid userId, Guid profileId, Guid instrumentId, UpdateGoldRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> AddStockAsync(Guid userId, Guid profileId, AddStockRequest req, CancellationToken ct = default);
        Task<Result<AssetIngestResponse>> UpdateStockAsync(Guid userId, Guid profileId, Guid instrumentId, UpdateStockRequest req, CancellationToken ct = default);
    }

    public class AssetInstrumentService : IAssetInstrumentService
    {
        private readonly PortivioDbContext _context;
        private readonly ITransactionIngestService _ingest;
        private readonly IProfileAccessGuard _profileAccess;
        private readonly IHoldingRecalculationService _recalculation;

        public AssetInstrumentService(
            PortivioDbContext context,
            ITransactionIngestService ingest,
            IProfileAccessGuard profileAccess,
            IHoldingRecalculationService recalculation)
        {
            _context = context;
            _ingest = ingest;
            _profileAccess = profileAccess;
            _recalculation = recalculation;
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
            if (req.InitialContribution <= 0)
                return Result<AssetIngestResponse>.BadRequest("InitialContribution must be greater than zero");
            if (req.CurrentRatePercent <= 0)
                return Result<AssetIngestResponse>.BadRequest("CurrentRatePercent must be greater than zero");

            var assetType = await GetOrCreateAssetTypeAsync("PPF", ct);
            var hasAccountNo = !string.IsNullOrWhiteSpace(req.AccountNo);
            var accountTrimmed = hasAccountNo ? req.AccountNo!.Trim() : null;
            var symbolSlot = hasAccountNo ? accountTrimmed!.ToUpperInvariant() : GenerateInstrumentSlot();
            var symbol = $"PPF:{symbolSlot}";
            var name = hasAccountNo ? $"PPF - {accountTrimmed}" : "PPF Account";
            var lockInEndsOn = req.OpenedOn.AddYears(15);

            var instrument = await GetOrCreateInstrumentAsync(
                name: name,
                symbol: symbol,
                isin: null,
                currency: "INR",
                assetTypeId: assetType.Id,
                category: AssetCategory.Ppf,
                priceSource: PriceSource.AccrualFormula,
                priceSourceKey: null,
                metadata: JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    accountNo = accountTrimmed,
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

            var exchangeSuffix = exchangeNorm == "BSE" ? "BO" : "NS";
            var instrument = await GetOrCreateInstrumentAsync(
                name: req.Name.Trim(),
                symbol: symbol,
                isin: req.Isin?.Trim().ToUpperInvariant(),
                currency: "INR",
                assetTypeId: assetType.Id,
                category: AssetCategory.Equity,
                priceSource: PriceSource.LivePriceApi,
                priceSourceKey: $"{symbolNorm}.{exchangeSuffix}",
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

        public async Task<Result<AssetIngestResponse>> UpdateMutualFundAsync(Guid userId, Guid profileId, Guid instrumentId, UpdateMutualFundRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.SchemeName))
                return Result<AssetIngestResponse>.BadRequest("SchemeName is required");
            if (string.IsNullOrWhiteSpace(req.SchemeCode))
                return Result<AssetIngestResponse>.BadRequest("SchemeCode is required");
            if (req.Units <= 0)
                return Result<AssetIngestResponse>.BadRequest("Units must be greater than zero");
            if (req.NavPerUnit <= 0)
                return Result<AssetIngestResponse>.BadRequest("NAV per unit must be greater than zero");

            return await UpdateInvestmentAsync(userId, profileId, async innerCt =>
            {
                var instrument = await _context.Instruments
                    .FirstOrDefaultAsync(i => i.Id == instrumentId, innerCt);
                if (instrument == null)
                    return Result<AssetIngestResponse>.NotFound("Instrument not found");
                if (instrument.Category != AssetCategory.MutualFund)
                    return Result<AssetIngestResponse>.BadRequest("Instrument is not a mutual fund");

                var transaction = await GetPrimaryTransactionAsync(profileId, instrumentId, TransactionType.Buy, innerCt);
                if (transaction == null)
                    return Result<AssetIngestResponse>.NotFound("Mutual fund investment not found");

                var schemeCode = req.SchemeCode.Trim();
                var isin = req.Isin?.Trim().ToUpperInvariant();
                var symbol = isin ?? schemeCode.ToUpperInvariant();

                instrument.Name = req.SchemeName.Trim();
                instrument.Symbol = symbol;
                instrument.Isin = isin;
                instrument.Currency = "INR";
                instrument.PriceSource = PriceSource.AmfiNav;
                instrument.PriceSourceKey = schemeCode;
                instrument.Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    schemeCode,
                    isin = req.Isin?.Trim(),
                    plan = req.Plan?.Trim(),
                    option = req.Option?.Trim()
                }));

                transaction.Quantity = req.Units;
                transaction.Price = req.NavPerUnit;
                transaction.Amount = req.Units * req.NavPerUnit;
                transaction.TransactionDate = NormalizeUtc(req.Date);
                transaction.Notes = req.Notes?.Trim() ?? string.Empty;
                transaction.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync(innerCt);

                var recalc = await _recalculation.RefreshProfileAsync(userId, profileId, innerCt);
                if (recalc.IsFailure)
                    return Result<AssetIngestResponse>.InternalServerError($"Holding recalculation failed: {recalc.Message}");

                return Result<AssetIngestResponse>.Success(new AssetIngestResponse
                {
                    InstrumentId = instrument.Id,
                    InstrumentName = instrument.Name,
                    Symbol = instrument.Symbol,
                    TransactionId = transaction.Id,
                    Message = "Mutual fund investment updated"
                }, "Mutual fund investment updated");
            }, ct);
        }

        public async Task<Result<AssetIngestResponse>> UpdateFixedDepositAsync(Guid userId, Guid profileId, Guid instrumentId, UpdateFixedDepositRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.Bank))
                return Result<AssetIngestResponse>.BadRequest("Bank is required");
            if (req.Principal <= 0)
                return Result<AssetIngestResponse>.BadRequest("Principal must be greater than zero");
            if (req.RatePercent <= 0)
                return Result<AssetIngestResponse>.BadRequest("Rate must be greater than zero");
            if (req.MaturityDate <= req.StartDate)
                return Result<AssetIngestResponse>.BadRequest("MaturityDate must be after StartDate");

            return await UpdateInvestmentAsync(userId, profileId, async innerCt =>
            {
                var instrument = await _context.Instruments
                    .FirstOrDefaultAsync(i => i.Id == instrumentId, innerCt);
                if (instrument == null)
                    return Result<AssetIngestResponse>.NotFound("Instrument not found");
                if (instrument.Category != AssetCategory.FixedDeposit)
                    return Result<AssetIngestResponse>.BadRequest("Instrument is not a fixed deposit");

                var transaction = await GetPrimaryTransactionAsync(profileId, instrumentId, TransactionType.Deposit, innerCt);
                if (transaction == null)
                    return Result<AssetIngestResponse>.NotFound("Fixed deposit investment not found");

                var hasAccountNo = !string.IsNullOrWhiteSpace(req.AccountNo);
                var accountTrimmed = hasAccountNo ? req.AccountNo!.Trim() : null;
                var symbolSlot = hasAccountNo
                    ? accountTrimmed!.ToUpperInvariant()
                    : ResolveFixedDepositSlot(instrument.Symbol);
                var symbol = $"FD:{req.Bank.Trim().ToUpperInvariant()}:{symbolSlot}";
                var name = hasAccountNo
                    ? $"FD - {req.Bank.Trim()} ({accountTrimmed})"
                    : $"FD - {req.Bank.Trim()}";

                instrument.Name = name;
                instrument.Symbol = symbol;
                instrument.Isin = null;
                instrument.Currency = "INR";
                instrument.PriceSource = PriceSource.AccrualFormula;
                instrument.PriceSourceKey = null;
                instrument.Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
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
                }));

                transaction.Quantity = 1m;
                transaction.Price = req.Principal;
                transaction.Amount = req.Principal;
                transaction.TransactionDate = NormalizeUtc(req.StartDate);
                transaction.Notes = req.Notes?.Trim() ?? string.Empty;
                transaction.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync(innerCt);

                var recalc = await _recalculation.RefreshProfileAsync(userId, profileId, innerCt);
                if (recalc.IsFailure)
                    return Result<AssetIngestResponse>.InternalServerError($"Holding recalculation failed: {recalc.Message}");

                return Result<AssetIngestResponse>.Success(new AssetIngestResponse
                {
                    InstrumentId = instrument.Id,
                    InstrumentName = instrument.Name,
                    Symbol = instrument.Symbol,
                    TransactionId = transaction.Id,
                    Message = "Fixed deposit investment updated"
                }, "Fixed deposit investment updated");
            }, ct);
        }

        public async Task<Result<AssetIngestResponse>> UpdateRecurringDepositAsync(Guid userId, Guid profileId, Guid instrumentId, UpdateRecurringDepositRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.Bank))
                return Result<AssetIngestResponse>.BadRequest("Bank is required");
            if (req.MonthlyAmount <= 0)
                return Result<AssetIngestResponse>.BadRequest("MonthlyAmount must be greater than zero");
            if (req.RatePercent <= 0)
                return Result<AssetIngestResponse>.BadRequest("Rate must be greater than zero");
            if (req.TenureMonths <= 0)
                return Result<AssetIngestResponse>.BadRequest("TenureMonths must be greater than zero");

            return await UpdateInvestmentAsync(userId, profileId, async innerCt =>
            {
                var instrument = await _context.Instruments
                    .FirstOrDefaultAsync(i => i.Id == instrumentId, innerCt);
                if (instrument == null)
                    return Result<AssetIngestResponse>.NotFound("Instrument not found");
                if (instrument.Category != AssetCategory.RecurringDeposit)
                    return Result<AssetIngestResponse>.BadRequest("Instrument is not a recurring deposit");

                var transaction = await GetPrimaryTransactionAsync(profileId, instrumentId, TransactionType.Contribution, innerCt);
                if (transaction == null)
                    return Result<AssetIngestResponse>.NotFound("Recurring deposit investment not found");

                var hasAccountNo = !string.IsNullOrWhiteSpace(req.AccountNo);
                var accountTrimmed = hasAccountNo ? req.AccountNo!.Trim() : null;
                var symbolSlot = hasAccountNo
                    ? accountTrimmed!.ToUpperInvariant()
                    : ResolveRecurringDepositSlot(instrument.Symbol);
                var symbol = $"RD:{req.Bank.Trim().ToUpperInvariant()}:{symbolSlot}";
                var name = hasAccountNo
                    ? $"RD - {req.Bank.Trim()} ({accountTrimmed})"
                    : $"RD - {req.Bank.Trim()}";

                instrument.Name = name;
                instrument.Symbol = symbol;
                instrument.Isin = null;
                instrument.Currency = "INR";
                instrument.PriceSource = PriceSource.AccrualFormula;
                instrument.PriceSourceKey = null;
                instrument.Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    bank = req.Bank.Trim(),
                    accountNo = accountTrimmed,
                    monthly = req.MonthlyAmount,
                    rate = req.RatePercent,
                    startDate = req.StartDate.ToString("yyyy-MM-dd"),
                    tenureMonths = req.TenureMonths
                }));

                transaction.Quantity = 1m;
                transaction.Price = req.MonthlyAmount;
                transaction.Amount = req.MonthlyAmount;
                transaction.TransactionDate = NormalizeUtc(req.StartDate);
                transaction.Notes = req.Notes?.Trim() ?? string.Empty;
                transaction.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync(innerCt);

                var recalc = await _recalculation.RefreshProfileAsync(userId, profileId, innerCt);
                if (recalc.IsFailure)
                    return Result<AssetIngestResponse>.InternalServerError($"Holding recalculation failed: {recalc.Message}");

                return Result<AssetIngestResponse>.Success(new AssetIngestResponse
                {
                    InstrumentId = instrument.Id,
                    InstrumentName = instrument.Name,
                    Symbol = instrument.Symbol,
                    TransactionId = transaction.Id,
                    Message = "Recurring deposit investment updated"
                }, "Recurring deposit investment updated");
            }, ct);
        }

        public async Task<Result<AssetIngestResponse>> UpdatePpfAsync(Guid userId, Guid profileId, Guid instrumentId, UpdatePpfRequest req, CancellationToken ct = default)
        {
            if (req.InitialContribution <= 0)
                return Result<AssetIngestResponse>.BadRequest("InitialContribution must be greater than zero");
            if (req.CurrentRatePercent <= 0)
                return Result<AssetIngestResponse>.BadRequest("CurrentRatePercent must be greater than zero");

            return await UpdateInvestmentAsync(userId, profileId, async innerCt =>
            {
                var instrument = await _context.Instruments
                    .FirstOrDefaultAsync(i => i.Id == instrumentId, innerCt);
                if (instrument == null)
                    return Result<AssetIngestResponse>.NotFound("Instrument not found");
                if (instrument.Category != AssetCategory.Ppf)
                    return Result<AssetIngestResponse>.BadRequest("Instrument is not a PPF account");

                var transaction = await GetPrimaryTransactionAsync(profileId, instrumentId, TransactionType.Contribution, innerCt);
                if (transaction == null)
                    return Result<AssetIngestResponse>.NotFound("PPF investment not found");

                var accountTrimmed = req.AccountNo?.Trim();
                var symbolSlot = string.IsNullOrWhiteSpace(accountTrimmed)
                    ? ResolvePpfSlot(instrument.Symbol)
                    : accountTrimmed!.ToUpperInvariant();
                var symbol = $"PPF:{symbolSlot}";
                var name = string.IsNullOrWhiteSpace(accountTrimmed)
                    ? "PPF Account"
                    : $"PPF - {accountTrimmed}";
                var lockInEndsOn = req.OpenedOn.AddYears(15);

                instrument.Name = name;
                instrument.Symbol = symbol;
                instrument.Isin = null;
                instrument.Currency = "INR";
                instrument.PriceSource = PriceSource.AccrualFormula;
                instrument.PriceSourceKey = null;
                instrument.Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    accountNo = accountTrimmed,
                    openedOn = req.OpenedOn.ToString("yyyy-MM-dd"),
                    lockInEndsOn = lockInEndsOn.ToString("yyyy-MM-dd"),
                    currentRate = req.CurrentRatePercent
                }));

                transaction.Quantity = 1m;
                transaction.Price = req.InitialContribution;
                transaction.Amount = req.InitialContribution;
                transaction.TransactionDate = NormalizeUtc(req.ContributionDate);
                transaction.Notes = req.Notes?.Trim() ?? "PPF opening contribution";
                transaction.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync(innerCt);

                var recalc = await _recalculation.RefreshProfileAsync(userId, profileId, innerCt);
                if (recalc.IsFailure)
                    return Result<AssetIngestResponse>.InternalServerError($"Holding recalculation failed: {recalc.Message}");

                return Result<AssetIngestResponse>.Success(new AssetIngestResponse
                {
                    InstrumentId = instrument.Id,
                    InstrumentName = instrument.Name,
                    Symbol = instrument.Symbol,
                    TransactionId = transaction.Id,
                    Message = "PPF investment updated"
                }, "PPF investment updated");
            }, ct);
        }

        public async Task<Result<AssetIngestResponse>> UpdateGoldAsync(Guid userId, Guid profileId, Guid instrumentId, UpdateGoldRequest req, CancellationToken ct = default)
        {
            if (req.WeightGrams <= 0)
                return Result<AssetIngestResponse>.BadRequest("WeightGrams must be greater than zero");
            if (req.RatePerGram <= 0)
                return Result<AssetIngestResponse>.BadRequest("RatePerGram must be greater than zero");

            return await UpdateInvestmentAsync(userId, profileId, async innerCt =>
            {
                var instrument = await _context.Instruments
                    .FirstOrDefaultAsync(i => i.Id == instrumentId, innerCt);
                if (instrument == null)
                    return Result<AssetIngestResponse>.NotFound("Instrument not found");
                if (instrument.Category != AssetCategory.Gold)
                    return Result<AssetIngestResponse>.BadRequest("Instrument is not a gold holding");

                var transaction = await GetPrimaryTransactionAsync(profileId, instrumentId, TransactionType.Buy, innerCt);
                if (transaction == null)
                    return Result<AssetIngestResponse>.NotFound("Gold investment not found");

                var purityNorm = req.Purity.Trim().ToUpperInvariant();
                var formNorm = req.Form.Trim().ToUpperInvariant();
                var totalCost = (req.WeightGrams * req.RatePerGram) + req.MakingChargesInr;

                instrument.Name = $"Gold {req.Purity} {req.Form}";
                instrument.Symbol = $"GOLD:{purityNorm}:{formNorm}";
                instrument.Isin = null;
                instrument.Currency = "INR";
                instrument.PriceSource = PriceSource.Manual;
                instrument.PriceSourceKey = null;
                instrument.Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    form = req.Form.Trim(),
                    purity = purityNorm,
                    makingChargesInr = req.MakingChargesInr
                }));

                transaction.Quantity = req.WeightGrams;
                transaction.Price = totalCost / req.WeightGrams;
                transaction.Amount = totalCost;
                transaction.TransactionDate = NormalizeUtc(req.Date);
                transaction.Notes = req.Notes?.Trim() ?? string.Empty;
                transaction.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync(innerCt);

                var recalc = await _recalculation.RefreshProfileAsync(userId, profileId, innerCt);
                if (recalc.IsFailure)
                    return Result<AssetIngestResponse>.InternalServerError($"Holding recalculation failed: {recalc.Message}");

                return Result<AssetIngestResponse>.Success(new AssetIngestResponse
                {
                    InstrumentId = instrument.Id,
                    InstrumentName = instrument.Name,
                    Symbol = instrument.Symbol,
                    TransactionId = transaction.Id,
                    Message = "Gold investment updated"
                }, "Gold investment updated");
            }, ct);
        }

        public async Task<Result<AssetIngestResponse>> UpdateStockAsync(Guid userId, Guid profileId, Guid instrumentId, UpdateStockRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Result<AssetIngestResponse>.BadRequest("Name is required");
            if (string.IsNullOrWhiteSpace(req.Symbol))
                return Result<AssetIngestResponse>.BadRequest("Symbol is required");
            if (req.Quantity <= 0)
                return Result<AssetIngestResponse>.BadRequest("Quantity must be greater than zero");
            if (req.Price <= 0)
                return Result<AssetIngestResponse>.BadRequest("Price must be greater than zero");

            return await UpdateInvestmentAsync(userId, profileId, async innerCt =>
            {
                var instrument = await _context.Instruments
                    .FirstOrDefaultAsync(i => i.Id == instrumentId, innerCt);
                if (instrument == null)
                    return Result<AssetIngestResponse>.NotFound("Instrument not found");
                if (instrument.Category != AssetCategory.Equity)
                    return Result<AssetIngestResponse>.BadRequest("Instrument is not a stock");

                var transaction = await GetPrimaryTransactionAsync(profileId, instrumentId, TransactionType.Buy, innerCt);
                if (transaction == null)
                    return Result<AssetIngestResponse>.NotFound("Stock investment not found");

                var exchangeNorm = req.Exchange.Trim().ToUpperInvariant();
                var symbolNorm = req.Symbol.Trim().ToUpperInvariant();
                var symbol = $"{exchangeNorm}:{symbolNorm}";
                var exchangeSuffix = exchangeNorm == "BSE" ? "BO" : "NS";

                instrument.Name = req.Name.Trim();
                instrument.Symbol = symbol;
                instrument.Isin = req.Isin?.Trim().ToUpperInvariant();
                instrument.Currency = "INR";
                instrument.PriceSource = PriceSource.LivePriceApi;
                instrument.PriceSourceKey = $"{symbolNorm}.{exchangeSuffix}";
                instrument.Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    exchange = exchangeNorm,
                    isin = req.Isin?.Trim()
                }));

                transaction.Quantity = req.Quantity;
                transaction.Price = req.Price;
                transaction.Amount = req.Quantity * req.Price;
                transaction.TransactionDate = NormalizeUtc(req.Date);
                transaction.Notes = req.Notes?.Trim() ?? string.Empty;
                transaction.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync(innerCt);

                var recalc = await _recalculation.RefreshProfileAsync(userId, profileId, innerCt);
                if (recalc.IsFailure)
                    return Result<AssetIngestResponse>.InternalServerError($"Holding recalculation failed: {recalc.Message}");

                return Result<AssetIngestResponse>.Success(new AssetIngestResponse
                {
                    InstrumentId = instrument.Id,
                    InstrumentName = instrument.Name,
                    Symbol = instrument.Symbol,
                    TransactionId = transaction.Id,
                    Message = "Stock investment updated"
                }, "Stock investment updated");
            }, ct);
        }

        // Internal-only uniqueness slot for FD/RD when AccountNo is blank.
        // Never displayed to users — it sits inside Instrument.Symbol so the
        // unique index `(AssetTypeId, Symbol)` admits multiple anonymous deposits.
        private static string GenerateInstrumentSlot()
            => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        private async Task<Result<AssetIngestResponse>> UpdateInvestmentAsync(
            Guid userId,
            Guid profileId,
            Func<CancellationToken, Task<Result<AssetIngestResponse>>> operation,
            CancellationToken ct)
        {
            var access = await _profileAccess.EnsureOwnerAsync(userId, profileId, ct);
            if (access.IsFailure)
                return access.ToFailure<AssetIngestResponse>();

            return await InTransactionAsync(operation, ct);
        }

        private async Task<Transaction?> GetPrimaryTransactionAsync(
            Guid profileId,
            Guid instrumentId,
            TransactionType expectedType,
            CancellationToken ct)
        {
            return await _context.Transactions
                .Where(t => t.ProfileId == profileId && t.InstrumentId == instrumentId && t.Type == expectedType)
                .OrderBy(t => t.CreatedAtUtc)
                .ThenBy(t => t.TransactionDate)
                .ThenBy(t => t.Id)
                .FirstOrDefaultAsync(ct);
        }

        private static DateTime NormalizeUtc(DateTime value)
            => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

        private static string ResolveFixedDepositSlot(string symbol)
        {
            var parts = symbol.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length >= 3 ? parts[2].ToUpperInvariant() : GenerateInstrumentSlot();
        }

        private static string ResolveRecurringDepositSlot(string symbol)
        {
            var parts = symbol.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length >= 3 ? parts[2].ToUpperInvariant() : GenerateInstrumentSlot();
        }

        private static string ResolvePpfSlot(string symbol)
        {
            var parts = symbol.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length >= 2 ? parts[1].ToUpperInvariant() : GenerateInstrumentSlot();
        }

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
