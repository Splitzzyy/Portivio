using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portivio.Application.DTOs.MarketData;
using Portivio.Application.Results;
using Portivio.Application.Services;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services.MarketData
{
    public interface IStandardRateService
    {
        Task<Result<PpfRateResponse>> SyncPpfRateAsync(CancellationToken ct = default);
        Task<Result<SyncSummaryResponse>> SyncFdRatesAsync(CancellationToken ct = default);
        Task<Result<PpfRateResponse>> GetLatestPpfRateAsync(CancellationToken ct = default);
        Task<Result<List<FdRateResponse>>> GetLatestFdRatesAsync(string? bank, int? tenureMonths, CancellationToken ct = default);
        Task<Result<FdRateResponse>> UpsertFdRateAsync(UpsertFdRateRequest request, CancellationToken ct = default);
    }

    public class StandardRateService : IStandardRateService
    {
        private const string PpfAssetTypeName = "PPF";
        private const string FdAssetTypeName = "FD";
        private const string PpfSymbol = "PPF:GOVT";
        private const string PpfName = "Public Provident Fund";
        private const string Currency = "INR";

        private readonly PortivioDbContext _context;
        private readonly IStandardRateProvider _rateProvider;
        private readonly IHoldingService _holdingService;
        private readonly MarketDataOptions _options;
        private readonly ILogger<StandardRateService> _logger;

        public StandardRateService(
            PortivioDbContext context,
            IStandardRateProvider rateProvider,
            IHoldingService holdingService,
            IOptions<MarketDataOptions> options,
            ILogger<StandardRateService> logger)
        {
            _context = context;
            _rateProvider = rateProvider;
            _holdingService = holdingService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<Result<PpfRateResponse>> SyncPpfRateAsync(CancellationToken ct = default)
        {
            try
            {
                var entry = await _rateProvider.GetPpfRateAsync(ct);
                var assetType = await GetOrCreateAssetTypeAsync(PpfAssetTypeName, ct);
                var instrument = await GetOrCreateInstrumentAsync(PpfName, PpfSymbol, assetType.Id, ct);

                await UpsertRateAsync(instrument.Id, entry.RatePercent, entry.AsOf, entry.Source, ct);

                return Result<PpfRateResponse>.Success(new PpfRateResponse
                {
                    RatePercent = entry.RatePercent,
                    AsOf = entry.AsOf,
                    Source = entry.Source
                }, "PPF rate synced", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncPpfRateAsync failed");
                return Result<PpfRateResponse>.InternalServerError($"Error syncing PPF rate: {ex.Message}");
            }
        }

        public async Task<Result<SyncSummaryResponse>> SyncFdRatesAsync(CancellationToken ct = default)
        {
            var summary = new SyncSummaryResponse();
            try
            {
                var rates = await _rateProvider.GetFdRatesAsync(ct);
                if (rates.Count == 0)
                {
                    summary.Errors.Add("No FD rates configured");
                    return Result<SyncSummaryResponse>.Success(summary, "No FD rates to sync");
                }

                var assetType = await _context.AssetTypes.FirstOrDefaultAsync(a => a.Name == FdAssetTypeName, ct);
                if (assetType == null)
                {
                    summary.Errors.Add("FD asset type not found");
                    return Result<SyncSummaryResponse>.Success(summary, "FD asset type not found");
                }

                var existingBySymbol = await _context.Instruments
                    .Where(i => i.AssetTypeId == assetType.Id)
                    .ToDictionaryAsync(i => i.Symbol, StringComparer.OrdinalIgnoreCase, ct);

                var instrumentIds = existingBySymbol.Values.Select(i => i.Id).ToList();
                // Check for existing prices based on the dates returned by the provider
                var datesToCheck = rates.Select(r => r.AsOf.ToUniversalTime().Date).Distinct().ToList();
                var existingPriceEntries = await _context.PriceHistories
                    .Where(ph => instrumentIds.Contains(ph.InstrumentId) && datesToCheck.Contains(ph.Date.Date))
                    .Select(ph => new { ph.InstrumentId, ph.Date })
                    .ToListAsync(ct);

                var existingPriceSet = new HashSet<string>(
                    existingPriceEntries.Select(x => $"{x.InstrumentId}_{x.Date:yyyy-MM-dd}"));
                var instrumentPricesToUpdate = new Dictionary<Guid, decimal>();

                foreach (var rate in rates)
                {
                    var symbol = BuildFdSymbol(rate.Bank, rate.TenureMonths);
                    if (!existingBySymbol.TryGetValue(symbol, out var instrument))
                    {
                        // Skip if instrument doesn't exist
                        continue;
                    }

                    var normalizedDate = rate.AsOf.ToUniversalTime().Date;
                    var key = $"{instrument.Id}_{normalizedDate:yyyy-MM-dd}";

                    if (existingPriceSet.Contains(key))
                    {
                        summary.Skipped++;
                        continue;
                    }

                    _context.PriceHistories.Add(new PriceHistory
                    {
                        Id = Guid.NewGuid(),
                        InstrumentId = instrument.Id,
                        Price = rate.RatePercent,
                        Date = DateTime.SpecifyKind(normalizedDate, DateTimeKind.Utc),
                        Source = rate.Source ?? string.Empty,
                        CreatedAt = DateTime.UtcNow
                    });

                    instrumentPricesToUpdate[instrument.Id] = rate.RatePercent;
                    summary.Inserted++;
                    existingPriceSet.Add(key);
                }

                await _context.SaveChangesAsync(ct);

                if (instrumentPricesToUpdate.Count > 0)
                {
                    await _holdingService.BulkUpdateCurrentPricesAsync(instrumentPricesToUpdate);
                }

                return Result<SyncSummaryResponse>.Success(summary, $"FD rate sync complete: {summary.Inserted} inserted, {summary.Skipped} skipped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncFdRatesAsync failed");
                summary.Errors.Add(ex.Message);
                return Result<SyncSummaryResponse>.InternalServerError($"Error syncing FD rates: {ex.Message}");
            }
        }

        public async Task<Result<PpfRateResponse>> GetLatestPpfRateAsync(CancellationToken ct = default)
        {
            var latest = await _context.PriceHistories
                .Where(ph => ph.Instrument.Symbol == PpfSymbol && ph.Instrument.AssetType.Name == PpfAssetTypeName)
                .OrderByDescending(ph => ph.Date)
                .Select(ph => new PpfRateResponse
                {
                    RatePercent = ph.Price,
                    AsOf = ph.Date,
                    Source = ph.Source
                })
                .FirstOrDefaultAsync(ct);

            return latest is null
                ? Result<PpfRateResponse>.NotFound("No PPF rate recorded yet")
                : Result<PpfRateResponse>.Success(latest, "Latest PPF rate retrieved");
        }

        public async Task<Result<List<FdRateResponse>>> GetLatestFdRatesAsync(string? bank, int? tenureMonths, CancellationToken ct = default)
        {
            var query = _context.PriceHistories
                .Where(ph => ph.Instrument.AssetType.Name == FdAssetTypeName);

            if (!string.IsNullOrWhiteSpace(bank))
            {
                var prefix = $"FD:{bank.Trim().ToUpperInvariant()}:";
                query = query.Where(ph => ph.Instrument.Symbol.StartsWith(prefix));
            }

            if (tenureMonths.HasValue)
            {
                var suffix = $":{tenureMonths.Value}M";
                query = query.Where(ph => ph.Instrument.Symbol.EndsWith(suffix));
            }

            var latestPerInstrument = await query
                .GroupBy(ph => ph.InstrumentId)
                .Select(g => g.OrderByDescending(p => p.Date).First())
                .Select(ph => new
                {
                    ph.Instrument.Symbol,
                    ph.Instrument.Name,
                    ph.Price,
                    ph.Date,
                    ph.Source
                })
                .ToListAsync(ct);

            var responses = latestPerInstrument
                .Select(x =>
                {
                    var (bankName, months) = ParseFdSymbol(x.Symbol);
                    return new FdRateResponse
                    {
                        Bank = bankName,
                        TenureMonths = months,
                        RatePercent = x.Price,
                        AsOf = x.Date,
                        Source = x.Source
                    };
                })
                .OrderBy(r => r.Bank).ThenBy(r => r.TenureMonths)
                .ToList();

            return Result<List<FdRateResponse>>.Success(responses, "Latest FD rates retrieved");
        }

        public async Task<Result<FdRateResponse>> UpsertFdRateAsync(UpsertFdRateRequest request, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Bank))
                    return Result<FdRateResponse>.BadRequest("Bank is required");
                if (request.TenureMonths <= 0)
                    return Result<FdRateResponse>.BadRequest("TenureMonths must be greater than zero");
                if (request.RatePercent <= 0)
                    return Result<FdRateResponse>.BadRequest("RatePercent must be greater than zero");

                var assetType = await GetOrCreateAssetTypeAsync(FdAssetTypeName, ct);
                var symbol = BuildFdSymbol(request.Bank, request.TenureMonths);
                var instrument = await GetOrCreateInstrumentAsync(
                    $"FD {request.Bank} {request.TenureMonths}M",
                    symbol,
                    assetType.Id,
                    ct);

                var source = $"BANK:{request.Bank.Trim().ToUpperInvariant()}";
                var asOf = DateTime.UtcNow.Date;

                await UpsertRateAsync(instrument.Id, request.RatePercent, asOf, source, ct);

                return Result<FdRateResponse>.Success(new FdRateResponse
                {
                    Bank = request.Bank.Trim(),
                    TenureMonths = request.TenureMonths,
                    RatePercent = request.RatePercent,
                    AsOf = asOf,
                    Source = source
                }, "FD rate upserted", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpsertFdRateAsync failed");
                return Result<FdRateResponse>.InternalServerError($"Error upserting FD rate: {ex.Message}");
            }
        }

        private static string BuildFdSymbol(string bank, int tenureMonths)
            => $"FD:{bank.Trim().ToUpperInvariant()}:{tenureMonths}M";

        private static (string Bank, int TenureMonths) ParseFdSymbol(string symbol)
        {
            var parts = symbol.Split(':');
            if (parts.Length < 3) return (symbol, 0);

            var bank = parts[1];
            var tenureToken = parts[2].TrimEnd('M');
            int.TryParse(tenureToken, out var months);
            return (bank, months);
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

        private async Task<Instrument> GetOrCreateInstrumentAsync(string name, string symbol, Guid assetTypeId, CancellationToken ct)
        {
            var existing = await _context.Instruments
                .FirstOrDefaultAsync(i => i.AssetTypeId == assetTypeId && i.Symbol == symbol, ct);

            if (existing != null) return existing;

            var created = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetTypeId,
                Name = name,
                Symbol = symbol,
                Currency = Currency
            };
            _context.Instruments.Add(created);
            await _context.SaveChangesAsync(ct);
            return created;
        }

        private async Task<bool> UpsertRateAsync(Guid instrumentId, decimal ratePercent, DateTime asOf, string source, CancellationToken ct)
        {
            var normalizedDate = asOf.ToUniversalTime().Date;
            var exists = await _context.PriceHistories.AnyAsync(
                ph => ph.InstrumentId == instrumentId && ph.Date.Date == normalizedDate, ct);

            if (exists) return false;

            _context.PriceHistories.Add(new PriceHistory
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Price = ratePercent,
                Date = DateTime.SpecifyKind(normalizedDate, DateTimeKind.Utc),
                Source = source ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(ct);
            return true;
        }
    }
}
