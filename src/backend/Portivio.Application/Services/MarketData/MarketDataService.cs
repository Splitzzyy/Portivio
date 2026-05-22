using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portivio.Application.DTOs.MarketData;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services.MarketData
{
    public interface IMarketDataService
    {
        Task<Result<MutualFundNavResponse>> SyncNavByIsinAsync(string isin, CancellationToken ct = default);
        Task<Result<SyncSummaryResponse>> SyncAllNavsAsync(CancellationToken ct = default);
        Task<Result<StockPriceResponse>> GetLatestStockPriceAsync(string symbol, CancellationToken ct = default);
        Task<Result<MutualFundNavResponse>> GetLatestNavAsync(string isin, CancellationToken ct = default);
    }

    public class MarketDataService : IMarketDataService
    {
        private const string StockAssetTypeName = "Stock";
        private const string MutualFundAssetTypeName = "Mutual Fund";
        private const string MutualFundCurrency = "INR";

        private readonly PortivioDbContext _context;
        private readonly IMutualFundNavProvider _navProvider;
        private readonly IHoldingService _holdingService;
        private readonly IMarketDataRefreshGate _refreshGate;
        private readonly IMarketDataDistributedLock _distributedLock;
        private readonly ILogger<MarketDataService> _logger;

        private sealed record NavSyncTarget(Guid InstrumentId, string Symbol, string? Isin, string? PriceSourceKey);

        public MarketDataService(
            PortivioDbContext context,
            IMutualFundNavProvider navProvider,
            IHoldingService holdingService,
            IMarketDataRefreshGate refreshGate,
            IMarketDataDistributedLock distributedLock,
            ILogger<MarketDataService> logger)
        {
            _context = context;
            _navProvider = navProvider;
            _holdingService = holdingService;
            _refreshGate = refreshGate;
            _distributedLock = distributedLock;
            _logger = logger;
        }

        public async Task<Result<MutualFundNavResponse>> SyncNavByIsinAsync(string isin, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(isin))
                    return Result<MutualFundNavResponse>.BadRequest("ISIN is required");

                var nav = await _navProvider.GetByIsinAsync(isin, ct);
                if (nav is null)
                    return Result<MutualFundNavResponse>.NotFound($"No NAV found for ISIN '{isin}'");

                var assetType = await GetOrCreateAssetTypeAsync(MutualFundAssetTypeName, ct);
                var instrument = await GetOrCreateInstrumentAsync(nav.SchemeName, nav.Isin, MutualFundCurrency, assetType.Id, ct);

                await UpsertPriceAsync(instrument.Id, nav.Nav, nav.AsOf, nav.Source, ct);

                return Result<MutualFundNavResponse>.Success(new MutualFundNavResponse
                {
                    Isin = nav.Isin,
                    SchemeName = nav.SchemeName,
                    Nav = nav.Nav,
                    AsOf = nav.AsOf,
                    Source = nav.Source
                }, "NAV synced", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncNavByIsinAsync failed for {Isin}", isin);
                return Result<MutualFundNavResponse>.InternalServerError($"Error syncing NAV: {ex.Message}");
            }
        }

        public async Task<Result<SyncSummaryResponse>> SyncAllNavsAsync(CancellationToken ct = default)
        {
            var summary = new SyncSummaryResponse();
            try
            {
                var targetsByNavKey = await GetMutualFundNavTargetsInUseAsync(ct);
                if (targetsByNavKey.Count == 0)
                {
                    return Result<SyncSummaryResponse>.Success(summary, "No in-use mutual fund instruments to sync");
                }

                var instrumentIds = targetsByNavKey.Values
                    .SelectMany(v => v.Select(t => t.InstrumentId))
                    .Distinct()
                    .ToList();

                if (await AllTargetsRefreshedTodayAsync(instrumentIds, ct))
                {
                    summary.Skipped = instrumentIds.Count;
                    return Result<SyncSummaryResponse>.Success(summary, "In-use mutual fund NAVs already refreshed today");
                }

                var lockKey = $"amfi:sync-all:{DateTime.UtcNow:yyyyMMdd}";
                return await _refreshGate.RunAsync(lockKey, localCt =>
                    _distributedLock.RunAsync(lockKey, async innerCt =>
                    {
                        if (await AllTargetsRefreshedTodayAsync(instrumentIds, innerCt))
                        {
                            summary.Skipped = instrumentIds.Count;
                            return Result<SyncSummaryResponse>.Success(summary, "In-use mutual fund NAVs already refreshed today");
                        }

                        var navs = await _navProvider.GetAllNavsAsync(innerCt);
                    if (navs.Count == 0)
                    {
                        summary.Errors.Add("Provider returned no NAV entries");
                        return Result<SyncSummaryResponse>.Success(summary, "No NAVs to sync");
                    }

                    var matchedNavs = navs
                        .Where(n => !string.IsNullOrWhiteSpace(n.Isin) && targetsByNavKey.ContainsKey(n.Isin))
                        .ToList();
                    if (matchedNavs.Count == 0)
                    {
                        return Result<SyncSummaryResponse>.Success(summary, "No matching in-use mutual fund NAVs to sync");
                    }

                    // Check for existing prices based on the dates returned by the provider
                    var datesToCheck = matchedNavs.Select(n => n.AsOf.ToUniversalTime().Date).Distinct().ToList();
                    var existingPriceEntries = await _context.PriceHistories
                        .Where(ph => instrumentIds.Contains(ph.InstrumentId) && datesToCheck.Contains(ph.Date.Date))
                        .Select(ph => new { ph.InstrumentId, ph.Date })
                        .ToListAsync(innerCt);

                    var existingPriceSet = new HashSet<string>(
                        existingPriceEntries.Select(x => $"{x.InstrumentId}_{x.Date:yyyy-MM-dd}"));
                    var instrumentPricesToUpdate = new Dictionary<Guid, decimal>();

                    foreach (var nav in matchedNavs)
                    {
                        var targets = targetsByNavKey[nav.Isin];

                        var normalizedDate = nav.AsOf.ToUniversalTime().Date;
                        foreach (var target in targets)
                        {
                            var key = $"{target.InstrumentId}_{normalizedDate:yyyy-MM-dd}";

                            if (existingPriceSet.Contains(key))
                            {
                                summary.Skipped++;
                                continue;
                            }

                            _context.PriceHistories.Add(new PriceHistory
                            {
                                Id = Guid.NewGuid(),
                                InstrumentId = target.InstrumentId,
                                Price = nav.Nav,
                                Date = DateTime.SpecifyKind(normalizedDate, DateTimeKind.Utc),
                                Source = nav.Source ?? string.Empty,
                                CreatedAt = DateTime.UtcNow
                            });

                            instrumentPricesToUpdate[target.InstrumentId] = nav.Nav;
                            summary.Inserted++;
                            existingPriceSet.Add(key); // Prevent duplicate inserts in same batch
                        }
                    }

                    await _context.SaveChangesAsync(innerCt);
                
                    if (instrumentPricesToUpdate.Count > 0)
                    {
                        await _holdingService.BulkUpdateCurrentPricesAsync(instrumentPricesToUpdate);
                    }

                        return Result<SyncSummaryResponse>.Success(summary, $"NAV sync complete: {summary.Inserted} inserted, {summary.Skipped} skipped");
                    }, localCt), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncAllNavsAsync failed");
                summary.Errors.Add(ex.Message);
                return Result<SyncSummaryResponse>.InternalServerError($"Error syncing NAVs: {ex.Message}");
            }
        }

        private async Task<bool> AllTargetsRefreshedTodayAsync(IReadOnlyCollection<Guid> instrumentIds, CancellationToken ct)
        {
            if (instrumentIds.Count == 0) return false;

            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var refreshedToday = await _context.PriceHistories
                .Where(ph => instrumentIds.Contains(ph.InstrumentId) && ph.CreatedAt >= today && ph.CreatedAt < tomorrow)
                .Select(ph => ph.InstrumentId)
                .Distinct()
                .CountAsync(ct);

            return refreshedToday == instrumentIds.Count;
        }

        private async Task<Dictionary<string, List<NavSyncTarget>>> GetMutualFundNavTargetsInUseAsync(CancellationToken ct)
        {
            var targets = await _context.Instruments
                .AsNoTracking()
                .Where(i => i.Category == AssetCategory.MutualFund && i.PriceSource == PriceSource.AmfiNav)
                .Where(i => i.Holdings.Any() || i.Transactions.Any(t => !t.IsDeleted))
                .Select(i => new NavSyncTarget(i.Id, i.Symbol, i.Isin, i.PriceSourceKey))
                .ToListAsync(ct);

            var byKey = new Dictionary<string, List<NavSyncTarget>>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in targets)
            {
                foreach (var key in GetNavIdentityKeys(target))
                {
                    if (!byKey.TryGetValue(key, out var keyedTargets))
                    {
                        keyedTargets = new List<NavSyncTarget>();
                        byKey[key] = keyedTargets;
                    }

                    if (!keyedTargets.Any(t => t.InstrumentId == target.InstrumentId))
                        keyedTargets.Add(target);
                }
            }

            return byKey;
        }

        private static IEnumerable<string> GetNavIdentityKeys(NavSyncTarget target)
        {
            if (!string.IsNullOrWhiteSpace(target.Symbol))
                yield return target.Symbol.Trim();
            if (!string.IsNullOrWhiteSpace(target.Isin))
                yield return target.Isin.Trim();
            if (!string.IsNullOrWhiteSpace(target.PriceSourceKey))
                yield return target.PriceSourceKey.Trim();
        }

        public async Task<Result<StockPriceResponse>> GetLatestStockPriceAsync(string symbol, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return Result<StockPriceResponse>.BadRequest("Symbol is required");

            var normalized = symbol.Trim().ToUpperInvariant();
            var latest = await _context.PriceHistories
                .Where(ph => ph.Instrument.Symbol == normalized && ph.Instrument.AssetType.Name == StockAssetTypeName)
                .OrderByDescending(ph => ph.Date)
                .Select(ph => new StockPriceResponse
                {
                    Symbol = ph.Instrument.Symbol,
                    Price = ph.Price,
                    AsOf = ph.Date,
                    Source = ph.Source
                })
                .FirstOrDefaultAsync(ct);

            return latest is null
                ? Result<StockPriceResponse>.NotFound($"No price recorded for '{symbol}'")
                : Result<StockPriceResponse>.Success(latest, "Latest stock price retrieved");
        }

        public async Task<Result<MutualFundNavResponse>> GetLatestNavAsync(string isin, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(isin))
                return Result<MutualFundNavResponse>.BadRequest("ISIN is required");

            var latest = await _context.PriceHistories
                .Where(ph => ph.Instrument.Symbol == isin && ph.Instrument.AssetType.Name == MutualFundAssetTypeName)
                .OrderByDescending(ph => ph.Date)
                .Select(ph => new MutualFundNavResponse
                {
                    Isin = ph.Instrument.Symbol,
                    SchemeName = ph.Instrument.Name,
                    Nav = ph.Price,
                    AsOf = ph.Date,
                    Source = ph.Source
                })
                .FirstOrDefaultAsync(ct);

            return latest is null
                ? Result<MutualFundNavResponse>.NotFound($"No NAV recorded for ISIN '{isin}'")
                : Result<MutualFundNavResponse>.Success(latest, "Latest NAV retrieved");
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

        private async Task<Instrument> GetOrCreateInstrumentAsync(string name, string symbol, string currency, Guid assetTypeId, CancellationToken ct)
        {
            var normalized = symbol.Trim();
            var existing = await _context.Instruments
                .FirstOrDefaultAsync(i => i.AssetTypeId == assetTypeId && i.Symbol == normalized, ct);

            if (existing != null) return existing;

            var created = new Instrument
            {
                Id = Guid.NewGuid(),
                AssetTypeId = assetTypeId,
                Name = name,
                Symbol = normalized,
                Currency = currency
            };
            _context.Instruments.Add(created);
            await _context.SaveChangesAsync(ct);
            return created;
        }

        private async Task<bool> UpsertPriceAsync(Guid instrumentId, decimal price, DateTime asOf, string source, CancellationToken ct)
        {
            var normalizedDate = asOf.ToUniversalTime().Date;
            var keyDate = normalizedDate.ToString("yyyy-MM-dd");

            var exists = await _context.PriceHistories.AnyAsync(
                ph => ph.InstrumentId == instrumentId && ph.Date.Date == normalizedDate, ct);

            if (exists) return false;

            _context.PriceHistories.Add(new PriceHistory
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Price = price,
                Date = DateTime.SpecifyKind(normalizedDate, DateTimeKind.Utc),
                Source = source ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(ct);
            await _holdingService.UpdateCurrentPriceAsync(instrumentId, price);
            return true;
        }
    }
}
