using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portivio.Application.DTOs.Holding;
using Portivio.Application.Results;
using Portivio.Application.Services.Authorization;
using Portivio.Application.Services.Strategies;
using Portivio.Domain.Entities;
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
        private readonly PortivioDbContext _context;
        private readonly AssetStrategyResolver _strategies;
        private readonly IProfileAccessGuard _profileAccess;
        private readonly ILogger<HoldingRecalculationService> _logger;

        public HoldingRecalculationService(
            PortivioDbContext context,
            AssetStrategyResolver strategies,
            IProfileAccessGuard profileAccess,
            ILogger<HoldingRecalculationService> logger)
        {
            _context = context;
            _strategies = strategies;
            _profileAccess = profileAccess;
            _logger = logger;
        }

        public Task<Result<RecalculationSummary>> RunDailyRefreshAsync(CancellationToken ct = default)
        {
            // Slice #29 fills this in (external price fetch + per-instrument failure tolerance + throttling).
            // Returning a no-op success keeps the interface stable for DI registration today.
            var summary = new RecalculationSummary(0, 0, 0, 0, 0, Array.Empty<string>());
            return Task.FromResult(Result<RecalculationSummary>.Success(summary, "Daily refresh not yet implemented"));
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
