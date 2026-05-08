using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portivio.Application.DTOs.Holding;
using Portivio.Application.Results;
using Portivio.Application.Services.Authorization;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services
{
    public interface IHoldingService
    {
        Task<Result<List<HoldingResponse>>> GetHoldingsAsync(Guid userId, Guid profileId);
        Task<Result<HoldingResponse>> UpsertHoldingAsync(Guid userId, Guid profileId, UpsertHoldingRequest request);
        Task<Result> DeleteHoldingAsync(Guid userId, Guid profileId, Guid holdingId);
        Task<Result> RecalculateHoldingFromTransactionsAsync(Guid profileId, Guid instrumentId);
        Task<Result> UpdateCurrentPriceAsync(Guid instrumentId, decimal currentPrice);
    }

    public class HoldingService : IHoldingService
    {
        private readonly PortivioDbContext _context;
        private readonly ILogger<HoldingService> _logger;
        private readonly IProfileAccessGuard _profileAccess;

        public HoldingService(PortivioDbContext context, ILogger<HoldingService> logger, IProfileAccessGuard profileAccess)
        {
            _context = context;
            _logger = logger;
            _profileAccess = profileAccess;
        }

        public async Task<Result<List<HoldingResponse>>> GetHoldingsAsync(Guid userId, Guid profileId)
        {
            try
            {
                var access = await _profileAccess.EnsureOwnerAsync(userId, profileId);
                if (access.IsFailure)
                {
                    _logger.LogWarning("Holdings lookup rejected. ProfileId={ProfileId} UserId={UserId} Reason={Reason}",
                        profileId, userId, access.Message);
                    return access.ToFailure<List<HoldingResponse>>();
                }

                var holdings = await _context.Holdings
                    .Include(h => h.Instrument).ThenInclude(i => i.AssetType)
                    .Where(h => h.ProfileId == profileId)
                    .OrderBy(h => h.Instrument.Name)
                    .Select(h => MapToResponse(h))
                    .ToListAsync();

                return Result<List<HoldingResponse>>.Success(holdings, "Holdings retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving holdings. ProfileId={ProfileId} UserId={UserId}", profileId, userId);
                return Result<List<HoldingResponse>>.InternalServerError($"Error retrieving holdings: {ex.Message}");
            }
        }

        public async Task<Result<HoldingResponse>> UpsertHoldingAsync(Guid userId, Guid profileId, UpsertHoldingRequest request)
        {
            try
            {
                if (request.Quantity <= 0)
                    return Result<HoldingResponse>.BadRequest("Quantity must be greater than zero");
                if (request.AvgPrice <= 0)
                    return Result<HoldingResponse>.BadRequest("Average price must be greater than zero");
                if (request.CurrentPrice < 0)
                    return Result<HoldingResponse>.BadRequest("Current price cannot be negative");

                var access = await _profileAccess.EnsureOwnerAsync(userId, profileId);
                if (access.IsFailure)
                {
                    _logger.LogWarning("Holding upsert rejected. ProfileId={ProfileId} UserId={UserId} Reason={Reason}",
                        profileId, userId, access.Message);
                    return access.ToFailure<HoldingResponse>();
                }

                var instrument = await _context.Instruments.Include(i => i.AssetType)
                    .FirstOrDefaultAsync(i => i.Id == request.InstrumentId);
                if (instrument == null)
                {
                    _logger.LogWarning("Holding upsert rejected: instrument not found. InstrumentId={InstrumentId}", request.InstrumentId);
                    return Result<HoldingResponse>.BadRequest("Instrument not found");
                }

                var existing = await _context.Holdings
                    .FirstOrDefaultAsync(h => h.ProfileId == profileId && h.InstrumentId == request.InstrumentId);

                Holding holding;
                bool isNew;
                if (existing != null)
                {
                    existing.Quantity = request.Quantity;
                    existing.AvgPrice = request.AvgPrice;
                    existing.CurrentPrice = request.CurrentPrice;
                    existing.MarketValue = request.Quantity * request.CurrentPrice;
                    existing.UnrealizedPnL = (request.CurrentPrice - request.AvgPrice) * request.Quantity;
                    existing.LastUpdated = DateTime.UtcNow;
                    holding = existing;
                    isNew = false;
                }
                else
                {
                    holding = new Holding
                    {
                        Id = Guid.NewGuid(),
                        ProfileId = profileId,
                        InstrumentId = request.InstrumentId,
                        Quantity = request.Quantity,
                        AvgPrice = request.AvgPrice,
                        CurrentPrice = request.CurrentPrice,
                        MarketValue = request.Quantity * request.CurrentPrice,
                        UnrealizedPnL = (request.CurrentPrice - request.AvgPrice) * request.Quantity,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.Holdings.Add(holding);
                    isNew = true;
                }

                // Reconcile transactions so the recalculation pipeline re-derives the
                // same quantity as the user just set.  Without this, the next Refresh call
                // would recompute from transactions and silently overwrite the direct edit.
                await ReconcileTransactionQuantityAsync(
                    profileId, request.InstrumentId, request.Quantity, request.AvgPrice);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Holding {Action}. HoldingId={HoldingId} ProfileId={ProfileId} InstrumentId={InstrumentId}",
                    isNew ? "created" : "updated", holding.Id, profileId, request.InstrumentId);

                return Result<HoldingResponse>.Success(new HoldingResponse
                {
                    Id = holding.Id,
                    ProfileId = holding.ProfileId,
                    InstrumentId = holding.InstrumentId,
                    InstrumentName = instrument.Name,
                    InstrumentSymbol = instrument.Symbol,
                    AssetTypeName = instrument.AssetType.Name,
                    Currency = instrument.Currency,
                    Quantity = holding.Quantity,
                    AvgPrice = holding.AvgPrice,
                    CurrentPrice = holding.CurrentPrice,
                    MarketValue = holding.MarketValue,
                    UnrealizedPnL = holding.UnrealizedPnL,
                    LastUpdated = holding.LastUpdated
                }, "Holding upserted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting holding. ProfileId={ProfileId} UserId={UserId} InstrumentId={InstrumentId}",
                    profileId, userId, request.InstrumentId);
                return Result<HoldingResponse>.InternalServerError($"Error upserting holding: {ex.Message}");
            }
        }

        public async Task<Result> DeleteHoldingAsync(Guid userId, Guid profileId, Guid holdingId)
        {
            try
            {
                var access = await _profileAccess.EnsureOwnerAsync(userId, profileId);
                if (access.IsFailure)
                {
                    _logger.LogWarning("Holding delete rejected. ProfileId={ProfileId} UserId={UserId} Reason={Reason}",
                        profileId, userId, access.Message);
                    return access.ToFailure();
                }

                var holding = await _context.Holdings.FirstOrDefaultAsync(h => h.Id == holdingId && h.ProfileId == profileId);
                if (holding == null)
                {
                    _logger.LogWarning("Holding delete rejected: not found. HoldingId={HoldingId} ProfileId={ProfileId}", holdingId, profileId);
                    return Result.NotFound("Holding not found");
                }

                var instrumentId = holding.InstrumentId;

                var transactions = await _context.Transactions
                    .Where(t => t.ProfileId == profileId && t.InstrumentId == instrumentId)
                    .ToListAsync();

                var now = DateTime.UtcNow;
                foreach (var tx in transactions)
                {
                    tx.IsDeleted = true;
                    tx.UpdatedAtUtc = now;
                }

                _context.Holdings.Remove(holding);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Holding deleted with {TxCount} transaction(s). HoldingId={HoldingId} ProfileId={ProfileId} InstrumentId={InstrumentId}",
                    transactions.Count, holdingId, profileId, instrumentId);

                return Result.Success("Holding deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting holding. HoldingId={HoldingId} ProfileId={ProfileId}", holdingId, profileId);
                return Result.InternalServerError($"Error deleting holding: {ex.Message}");
            }
        }

        private async Task ReconcileTransactionQuantityAsync(
            Guid profileId, Guid instrumentId, decimal targetQty, decimal pricePerUnit)
        {
            var txs = await _context.Transactions
                .Where(t => t.ProfileId == profileId && t.InstrumentId == instrumentId)
                .ToListAsync();

            if (!txs.Any()) return;

            var netBuy  = txs.Where(t => t.Type == TransactionType.Buy  || t.Type == TransactionType.BonusUnits).Sum(t => t.Quantity);
            var netSell = txs.Where(t => t.Type == TransactionType.Sell || t.Type == TransactionType.Withdrawal).Sum(t => t.Quantity);
            var currentNetQty = netBuy - netSell;

            var delta = targetQty - currentNetQty;
            if (Math.Abs(delta) < 0.00001m) return;

            var now = DateTime.UtcNow;
            _context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                ProfileId = profileId,
                InstrumentId = instrumentId,
                Type = delta > 0 ? TransactionType.Buy : TransactionType.Sell,
                Quantity = Math.Abs(delta),
                Price = pricePerUnit,
                Amount = Math.Abs(delta) * pricePerUnit,
                TransactionDate = now,
                Notes = "Direct holding adjustment",
                Source = TransactionSource.Manual,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            _logger.LogInformation(
                "Holding reconciliation transaction created. ProfileId={ProfileId} InstrumentId={InstrumentId} Delta={Delta} Type={Type}",
                profileId, instrumentId, delta, delta > 0 ? "Buy" : "Sell");
        }

        public async Task<Result> RecalculateHoldingFromTransactionsAsync(Guid profileId, Guid instrumentId)
        {
            try
            {
                var transactions = await _context.Transactions
                    .Where(t => t.ProfileId == profileId && t.InstrumentId == instrumentId)
                    .ToListAsync();

                var buyTransactions = transactions.Where(t => t.Type == TransactionType.Buy).ToList();
                var sellTransactions = transactions.Where(t => t.Type == TransactionType.Sell).ToList();

                var totalBuyQty = buyTransactions.Sum(t => t.Quantity);
                var totalSellQty = sellTransactions.Sum(t => t.Quantity);
                var netQty = totalBuyQty - totalSellQty;

                var existingHolding = await _context.Holdings
                    .FirstOrDefaultAsync(h => h.ProfileId == profileId && h.InstrumentId == instrumentId);

                if (netQty <= 0)
                {
                    if (existingHolding != null)
                    {
                        _context.Holdings.Remove(existingHolding);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Holding removed: position closed. ProfileId={ProfileId} InstrumentId={InstrumentId}",
                            profileId, instrumentId);
                    }
                    return Result.Success("Holding removed — position closed");
                }

                var weightedCostBasis = totalBuyQty > 0
                    ? buyTransactions.Sum(t => t.Quantity * t.Price) / totalBuyQty
                    : 0m;

                var latestPrice = await _context.PriceHistories
                    .Where(ph => ph.InstrumentId == instrumentId)
                    .OrderByDescending(ph => ph.Date)
                    .Select(ph => (decimal?)ph.Price)
                    .FirstOrDefaultAsync();

                var currentPrice = latestPrice ?? (buyTransactions.Count > 0
                    ? buyTransactions.OrderByDescending(t => t.TransactionDate).First().Price
                    : 0m);

                if (existingHolding != null)
                {
                    existingHolding.Quantity = netQty;
                    existingHolding.AvgPrice = weightedCostBasis;
                    existingHolding.CurrentPrice = currentPrice;
                    existingHolding.MarketValue = netQty * currentPrice;
                    existingHolding.UnrealizedPnL = (currentPrice - weightedCostBasis) * netQty;
                    existingHolding.LastUpdated = DateTime.UtcNow;
                }
                else
                {
                    _context.Holdings.Add(new Holding
                    {
                        Id = Guid.NewGuid(),
                        ProfileId = profileId,
                        InstrumentId = instrumentId,
                        Quantity = netQty,
                        AvgPrice = weightedCostBasis,
                        CurrentPrice = currentPrice,
                        MarketValue = netQty * currentPrice,
                        UnrealizedPnL = (currentPrice - weightedCostBasis) * netQty,
                        LastUpdated = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Holding recalculated. ProfileId={ProfileId} InstrumentId={InstrumentId} NetQty={NetQty} AvgPrice={AvgPrice}",
                    profileId, instrumentId, netQty, weightedCostBasis);

                return Result.Success("Holding recalculated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating holding. ProfileId={ProfileId} InstrumentId={InstrumentId}", profileId, instrumentId);
                return Result.InternalServerError($"Error recalculating holding: {ex.Message}");
            }
        }

        public async Task<Result> UpdateCurrentPriceAsync(Guid instrumentId, decimal currentPrice)
        {
            try
            {
                var holdings = await _context.Holdings
                    .Where(h => h.InstrumentId == instrumentId)
                    .ToListAsync();

                foreach (var holding in holdings)
                {
                    holding.CurrentPrice = currentPrice;
                    holding.MarketValue = holding.Quantity * currentPrice;
                    holding.UnrealizedPnL = (currentPrice - holding.AvgPrice) * holding.Quantity;
                    holding.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                if (holdings.Count > 0)
                    _logger.LogInformation("Current price propagated. InstrumentId={InstrumentId} Price={Price} HoldingsAffected={Count}",
                        instrumentId, currentPrice, holdings.Count);

                return Result.Success($"Updated current price for {holdings.Count} holding(s)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating current prices. InstrumentId={InstrumentId} Price={Price}", instrumentId, currentPrice);
                return Result.InternalServerError($"Error updating current prices: {ex.Message}");
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
