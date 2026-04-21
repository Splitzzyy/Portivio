using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portivio.Application.DTOs.MarketData;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services.MarketData
{
    public interface IMarketDataService
    {
        Task<Result<StockPriceResponse>> SyncStockPriceAsync(string symbol, CancellationToken ct = default);
        Task<Result<MutualFundNavResponse>> SyncNavByIsinAsync(string isin, CancellationToken ct = default);
        Task<Result<SyncSummaryResponse>> SyncAllNavsAsync(CancellationToken ct = default);
        Task<Result<StockPriceResponse>> GetLatestStockPriceAsync(string symbol, CancellationToken ct = default);
        Task<Result<MutualFundNavResponse>> GetLatestNavAsync(string isin, CancellationToken ct = default);
    }

    public class MarketDataService : IMarketDataService
    {
        private const string StockAssetTypeName = "Stock";
        private const string MutualFundAssetTypeName = "Mutual Fund";
        private const string StockCurrency = "INR";
        private const string MutualFundCurrency = "INR";

        private readonly PortivioDbContext _context;
        private readonly IStockPriceProvider _stockProvider;
        private readonly IMutualFundNavProvider _navProvider;
        private readonly IHoldingService _holdingService;
        private readonly ILogger<MarketDataService> _logger;

        public MarketDataService(
            PortivioDbContext context,
            IStockPriceProvider stockProvider,
            IMutualFundNavProvider navProvider,
            IHoldingService holdingService,
            ILogger<MarketDataService> logger)
        {
            _context = context;
            _stockProvider = stockProvider;
            _navProvider = navProvider;
            _holdingService = holdingService;
            _logger = logger;
        }

        public async Task<Result<StockPriceResponse>> SyncStockPriceAsync(string symbol, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol))
                    return Result<StockPriceResponse>.BadRequest("Symbol is required");

                var quote = await _stockProvider.GetQuoteAsync(symbol, ct);
                if (quote is null)
                    return Result<StockPriceResponse>.NotFound($"No quote returned for symbol '{symbol}'");

                var assetType = await GetOrCreateAssetTypeAsync(StockAssetTypeName, ct);
                var instrument = await GetOrCreateInstrumentAsync(quote.Symbol, quote.Symbol, StockCurrency, assetType.Id, ct);

                await UpsertPriceAsync(instrument.Id, quote.Price, quote.AsOf, quote.Source, ct);

                return Result<StockPriceResponse>.Success(new StockPriceResponse
                {
                    Symbol = quote.Symbol,
                    Price = quote.Price,
                    AsOf = quote.AsOf,
                    Source = quote.Source
                }, "Stock price synced", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncStockPriceAsync failed for {Symbol}", symbol);
                return Result<StockPriceResponse>.InternalServerError($"Error syncing stock price: {ex.Message}");
            }
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
                var navs = await _navProvider.GetAllNavsAsync(ct);
                if (navs.Count == 0)
                {
                    summary.Errors.Add("Provider returned no NAV entries");
                    return Result<SyncSummaryResponse>.Success(summary, "No NAVs to sync");
                }

                var assetType = await GetOrCreateAssetTypeAsync(MutualFundAssetTypeName, ct);
                var existingBySymbol = await _context.Instruments
                    .Where(i => i.AssetTypeId == assetType.Id)
                    .ToDictionaryAsync(i => i.Symbol, StringComparer.OrdinalIgnoreCase, ct);

                foreach (var nav in navs)
                {
                    try
                    {
                        if (!existingBySymbol.TryGetValue(nav.Isin, out var instrument))
                        {
                            instrument = new Instrument
                            {
                                Id = Guid.NewGuid(),
                                AssetTypeId = assetType.Id,
                                Name = nav.SchemeName,
                                Symbol = nav.Isin,
                                Currency = MutualFundCurrency
                            };
                            _context.Instruments.Add(instrument);
                            existingBySymbol[nav.Isin] = instrument;
                            summary.CreatedInstruments++;
                        }

                        var inserted = await UpsertPriceAsync(instrument.Id, nav.Nav, nav.AsOf, nav.Source, ct);
                        if (inserted) summary.Inserted++;
                        else summary.Skipped++;
                    }
                    catch (Exception inner)
                    {
                        summary.Errors.Add($"ISIN {nav.Isin}: {inner.Message}");
                    }
                }

                await _context.SaveChangesAsync(ct);
                return Result<SyncSummaryResponse>.Success(summary, $"NAV sync complete: {summary.Inserted} inserted, {summary.Skipped} skipped, {summary.CreatedInstruments} new instruments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncAllNavsAsync failed");
                summary.Errors.Add(ex.Message);
                return Result<SyncSummaryResponse>.InternalServerError($"Error syncing NAVs: {ex.Message}");
            }
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
            var normalizedDate = DateTime.SpecifyKind(asOf.ToUniversalTime().Date, DateTimeKind.Utc);

            var exists = await _context.PriceHistories.AnyAsync(
                ph => ph.InstrumentId == instrumentId && ph.Date.Date == normalizedDate.Date, ct);

            if (exists) return false;

            _context.PriceHistories.Add(new PriceHistory
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Price = price,
                Date = normalizedDate,
                Source = source ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(ct);
            await _holdingService.UpdateCurrentPriceAsync(instrumentId, price);
            return true;
        }
    }
}
