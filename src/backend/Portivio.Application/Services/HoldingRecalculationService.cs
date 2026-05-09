using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portivio.Application.DTOs.Holding;
using Portivio.Application.Results;
using Portivio.Application.Services.Authorization;
using Portivio.Application.Services.MarketData;
using Portivio.Application.Services.Strategies;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services
{
    public sealed record RecalculationSummary(
        int InstrumentsAttempted,
        int PricesUpdated,
        int PricesSkipped,
        int HoldingsRecomputed,
        int Errors,
        IReadOnlyList<string> ErrorMessages);

    public interface IHoldingRecalculationService
    {
        Task<Result<RecalculationSummary>> RunDailyRefreshAsync(CancellationToken ct = default);

        Task<Result<List<HoldingResponse>>> RefreshProfileAsync(
            Guid userId, Guid profileId, CancellationToken ct = default);
    }

    public class HoldingRecalculationService : IHoldingRecalculationService
    {
        // AlphaVantage free tier: 5 calls/min and 500 calls/day. Stay under both.
        private static readonly TimeSpan AlphaVantageThrottle = TimeSpan.FromSeconds(12);
        private const int AlphaVantageDailyCap = 500;

        private readonly PortivioDbContext _context;
        private readonly AssetStrategyResolver _strategies;
        private readonly IProfileAccessGuard _profileAccess;
        private readonly IMarketDataService _marketData;
        private readonly IGoldRateProvider _goldRate;
        private readonly ILivePriceApiStockProvider _livePrice;
        private readonly IRefreshThrottle _throttle;
        private readonly ILogger<HoldingRecalculationService> _logger;

        public HoldingRecalculationService(
            PortivioDbContext context,
            AssetStrategyResolver strategies,
            IProfileAccessGuard profileAccess,
            IMarketDataService marketData,
            IGoldRateProvider goldRate,
            ILivePriceApiStockProvider livePrice,
            IRefreshThrottle throttle,
            ILogger<HoldingRecalculationService> logger)
        {
            _context = context;
            _strategies = strategies;
            _profileAccess = profileAccess;
            _marketData = marketData;
            _goldRate = goldRate;
            _livePrice = livePrice;
            _throttle = throttle;
            _logger = logger;
        }

        public async Task<Result<RecalculationSummary>> RunDailyRefreshAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Daily holdings refresh started");

            var attempted = 0;
            var pricesUpdated = 0;
            var pricesSkipped = 0;
            var holdingsRecomputed = 0;
            var errorMessages = new List<string>();

            // Bulk AMFI sync once per run (single network call covers all MF instruments).
            try
            {
                var amfi = await _marketData.SyncAllNavsAsync(ct);
                if (amfi.IsSuccess && amfi.Data != null)
                    pricesUpdated += amfi.Data.Inserted;
            }
            catch (Exception ex)
            {
                errorMessages.Add($"AMFI bulk sync: {ex.Message}");
                _logger.LogWarning(ex, "AMFI bulk sync failed");
            }

            // Per-instrument external fetch for non-AMFI sources.
            var instruments = await _context.Instruments.AsNoTracking().ToListAsync(ct);
            var alphaCallCount = 0;

            foreach (var inst in instruments)
            {
                attempted++;
                try
                {
                    switch (inst.PriceSource)
                    {
                        case PriceSource.AlphaVantage:
                            if (alphaCallCount >= AlphaVantageDailyCap)
                            {
                                pricesSkipped++;
                                continue;
                            }
                            if (alphaCallCount > 0)
                                await _throttle.DelayAsync(AlphaVantageThrottle, ct);
                            alphaCallCount++;
                            var stock = await _marketData.SyncStockPriceAsync(inst.Symbol, ct);
                            if (stock.IsSuccess) pricesUpdated++;
                            else pricesSkipped++;
                            break;

                        case PriceSource.AmfiNav:
                            // Already handled by the bulk sync above.
                            break;

                        case PriceSource.AccrualFormula:
                            // No external call — accrual is computed by the per-strategy snapshot below.
                            break;

                        case PriceSource.LivePriceApi:
                            var ticker = ResolveTicker(inst);
                            var liveQuote = await _livePrice.GetQuoteAsync(ticker, ct);
                            if (liveQuote is not null)
                            {
                                await UpsertLivePriceAsync(inst.Id, liveQuote.Price, liveQuote.AsOf, liveQuote.Source, ct);
                                pricesUpdated++;
                            }
                            else
                                pricesSkipped++;
                            break;

                        case PriceSource.Manual when inst.Category == AssetCategory.Gold:
                            var goldUpdated = await TryUpsertGoldPriceAsync(inst, ct);
                            if (goldUpdated) pricesUpdated++;
                            else pricesSkipped++;
                            break;

                        case PriceSource.Manual:
                            pricesSkipped++;
                            break;

                        default:
                            pricesSkipped++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    errorMessages.Add($"{inst.Symbol}: {ex.Message}");
                    _logger.LogWarning(ex,
                        "Per-instrument refresh failed. InstrumentId={InstrumentId} Symbol={Symbol} Source={Source}",
                        inst.Id, inst.Symbol, inst.PriceSource);
                }
            }

            // Recompute holdings per profile via strategy snapshots.
            var holdings = await _context.Holdings
                .Include(h => h.Instrument)
                .ToListAsync(ct);
            var asOf = DateTime.UtcNow;
            var groups = holdings.GroupBy(h => h.ProfileId);

            foreach (var group in groups)
            {
                foreach (var holding in group)
                {
                    try
                    {
                        var strategy = _strategies.For(holding.Instrument.Category);
                        var snapshot = await strategy.ComputeHoldingAsync(
                            holding.ProfileId, holding.InstrumentId, asOf, ct);

                        if (snapshot.Quantity <= 0)
                        {
                            _context.Holdings.Remove(holding);
                            continue;
                        }

                        holding.Quantity = snapshot.Quantity;
                        holding.AvgPrice = snapshot.AvgPrice;
                        holding.CurrentPrice = snapshot.CurrentPrice;
                        holding.MarketValue = snapshot.MarketValue;
                        holding.UnrealizedPnL = snapshot.UnrealizedPnL;
                        holding.RealizedPnL = snapshot.RealizedPnL;
                        holding.AccruedInterest = snapshot.AccruedInterest;
                        holding.Snapshot = snapshot.Snapshot;
                        holding.LastUpdated = asOf;
                        holdingsRecomputed++;
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"holding {holding.Id}: {ex.Message}");
                        _logger.LogWarning(ex, "Per-holding recompute failed. HoldingId={HoldingId}", holding.Id);
                    }
                }
                await _context.SaveChangesAsync(ct);
            }

            var summary = new RecalculationSummary(
                attempted, pricesUpdated, pricesSkipped, holdingsRecomputed,
                errorMessages.Count, errorMessages);

            _logger.LogInformation(
                "Daily holdings refresh complete. Attempted={InstrumentsAttempted} Updated={PricesUpdated} Skipped={PricesSkipped} Recomputed={HoldingsRecomputed} Errors={Errors}",
                attempted, pricesUpdated, pricesSkipped, holdingsRecomputed, errorMessages.Count);

            return Result<RecalculationSummary>.Success(summary, "Daily refresh complete");
        }

        public async Task<Result<List<HoldingResponse>>> RefreshProfileAsync(
            Guid userId, Guid profileId, CancellationToken ct = default)
        {
            try
            {
                var access = await _profileAccess.EnsureOwnerAsync(userId, profileId, ct);
                if (access.IsFailure)
                {
                    _logger.LogWarning("Holdings refresh rejected. ProfileId={ProfileId} UserId={UserId} Reason={Reason}",
                        profileId, userId, access.Message);
                    return access.ToFailure<List<HoldingResponse>>();
                }

                var holdings = await _context.Holdings
                    .Include(h => h.Instrument).ThenInclude(i => i.AssetType)
                    .Where(h => h.ProfileId == profileId)
                    .ToListAsync(ct);

                // Refresh gold rates from config first so the recompute below picks up today's PriceHistory.
                var goldInstruments = holdings
                    .Where(h => h.Instrument.Category == AssetCategory.Gold && h.Instrument.PriceSource == PriceSource.Manual)
                    .Select(h => h.Instrument)
                    .GroupBy(i => i.Id)
                    .Select(g => g.First())
                    .ToList();
                foreach (var inst in goldInstruments)
                {
                    try { await TryUpsertGoldPriceAsync(inst, ct); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Gold rate upsert failed during manual refresh. InstrumentId={InstrumentId} Symbol={Symbol}",
                            inst.Id, inst.Symbol);
                    }
                }

                // Fetch live prices for all equity holdings (both LivePriceApi and legacy AlphaVantage).
                var equityInstruments = holdings
                    .Where(h => h.Instrument.Category == AssetCategory.Equity)
                    .Select(h => h.Instrument)
                    .GroupBy(i => i.Id)
                    .Select(g => g.First())
                    .ToList();
                foreach (var inst in equityInstruments)
                {
                    try
                    {
                        var ticker = ResolveTicker(inst);
                        var quote = await _livePrice.GetQuoteAsync(ticker, ct);
                        if (quote is not null)
                            await UpsertLivePriceAsync(inst.Id, quote.Price, quote.AsOf, quote.Source, ct);
                        else
                            _logger.LogWarning("Live price unavailable during manual refresh. Ticker={Ticker}", ticker);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Live price fetch failed during manual refresh. InstrumentId={InstrumentId} Symbol={Symbol}",
                            inst.Id, inst.Symbol);
                    }
                }

                var asOf = DateTime.UtcNow;
                var recomputed = 0;

                foreach (var holding in holdings)
                {
                    var strategy = _strategies.For(holding.Instrument.Category);
                    var snapshot = await strategy.ComputeHoldingAsync(profileId, holding.InstrumentId, asOf, ct);

                    if (snapshot.Quantity <= 0)
                    {
                        _context.Holdings.Remove(holding);
                        continue;
                    }

                    holding.Quantity = snapshot.Quantity;
                    holding.AvgPrice = snapshot.AvgPrice;
                    holding.CurrentPrice = snapshot.CurrentPrice;
                    holding.MarketValue = snapshot.MarketValue;
                    holding.UnrealizedPnL = snapshot.UnrealizedPnL;
                    holding.RealizedPnL = snapshot.RealizedPnL;
                    holding.AccruedInterest = snapshot.AccruedInterest;
                    holding.Snapshot = snapshot.Snapshot;
                    holding.LastUpdated = asOf;
                    recomputed++;
                }

                await _context.SaveChangesAsync(ct);

                _logger.LogInformation("Holdings refreshed. UserId={UserId} ProfileId={ProfileId} HoldingsRecomputed={Count}",
                    userId, profileId, recomputed);

                var response = await _context.Holdings
                    .Include(h => h.Instrument).ThenInclude(i => i.AssetType)
                    .Where(h => h.ProfileId == profileId)
                    .OrderBy(h => h.Instrument.Name)
                    .ToListAsync(ct);

                return Result<List<HoldingResponse>>.Success(response.Select(MapToResponse).ToList(),
                    "Holdings refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing holdings. ProfileId={ProfileId} UserId={UserId}", profileId, userId);
                return Result<List<HoldingResponse>>.InternalServerError($"Error refreshing holdings: {ex.Message}");
            }
        }

        private async Task<bool> TryUpsertGoldPriceAsync(Instrument inst, CancellationToken ct)
        {
            if (inst.Metadata == null) return false;

            string? purity = null;
            if (inst.Metadata.RootElement.TryGetProperty("purity", out var purityProp))
                purity = purityProp.GetString();
            if (string.IsNullOrWhiteSpace(purity)) return false;

            var rate = await _goldRate.GetRatePerGramAsync(purity!, ct);
            if (rate is null || rate <= 0)
            {
                _logger.LogWarning("Gold rate unavailable. InstrumentId={InstrumentId} Symbol={Symbol} Purity={Purity}",
                    inst.Id, inst.Symbol, purity);
                return false;
            }

            var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            var alreadyToday = await _context.PriceHistories.AnyAsync(
                ph => ph.InstrumentId == inst.Id && ph.Date.Date == today.Date, ct);
            if (alreadyToday) return false;

            _context.PriceHistories.Add(new PriceHistory
            {
                Id = Guid.NewGuid(),
                InstrumentId = inst.Id,
                Price = rate.Value,
                Date = today,
                Source = "config",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(ct);
            return true;
        }

        private static string ResolveTicker(Instrument inst)
        {
            // priceSourceKey is "TCS.NS" for new instruments; bare "TCS" for legacy AlphaVantage ones
            if (!string.IsNullOrWhiteSpace(inst.PriceSourceKey) && inst.PriceSourceKey.Contains('.'))
                return inst.PriceSourceKey;

            var sym = inst.PriceSourceKey ?? inst.Symbol;
            var suffix = "NS";
            if (inst.Metadata?.RootElement.TryGetProperty("exchange", out var exc) == true
                && exc.GetString()?.Equals("BSE", StringComparison.OrdinalIgnoreCase) == true)
                suffix = "BO";
            return $"{sym}.{suffix}";
        }

        private async Task UpsertLivePriceAsync(Guid instrumentId, decimal price, DateTime asOf, string source, CancellationToken ct)
        {
            var today = DateTime.SpecifyKind(asOf.Date, DateTimeKind.Utc);
            var existing = await _context.PriceHistories
                .FirstOrDefaultAsync(ph => ph.InstrumentId == instrumentId && ph.Date.Date == today.Date, ct);

            if (existing is not null)
            {
                existing.Price = price;
                existing.Source = source;
            }
            else
            {
                _context.PriceHistories.Add(new PriceHistory
                {
                    Id = Guid.NewGuid(),
                    InstrumentId = instrumentId,
                    Price = price,
                    Date = today,
                    Source = source,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync(ct);
        }

        private static HoldingResponse MapToResponse(Holding h) => new()
        {
            Id = h.Id,
            ProfileId = h.ProfileId,
            InstrumentId = h.InstrumentId,
            InstrumentName = h.Instrument.Name,
            InstrumentSymbol = h.Instrument.Symbol,
            AssetTypeName = h.Instrument.AssetType.Name,
            Currency = h.Instrument.Currency,
            Quantity = h.Quantity,
            AvgPrice = h.AvgPrice,
            CurrentPrice = h.CurrentPrice,
            MarketValue = h.MarketValue,
            UnrealizedPnL = h.UnrealizedPnL,
            LastUpdated = h.LastUpdated
        };
    }
}
