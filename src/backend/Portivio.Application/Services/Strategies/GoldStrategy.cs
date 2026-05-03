using Microsoft.EntityFrameworkCore;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using System.Text.Json;

namespace Portivio.Application.Services.Strategies
{
    public class GoldStrategy : IAssetStrategy
    {
        private readonly PortivioDbContext _context;

        public GoldStrategy(PortivioDbContext context)
        {
            _context = context;
        }

        public AssetCategory Category => AssetCategory.Gold;

        public Result ValidateInstrumentMetadata(JsonDocument? meta) => Result.Success("Valid");

        public Result ValidateTransaction(Transaction tx, Instrument inst)
        {
            return tx.Type switch
            {
                TransactionType.Buy or TransactionType.Sell =>
                    tx.Quantity <= 0 ? Result.BadRequest("Weight in grams must be greater than zero for Buy/Sell") :
                    tx.Price <= 0 ? Result.BadRequest("Price per gram must be greater than zero for Buy/Sell") :
                    Result.Success("Valid"),
                TransactionType.Dividend =>
                    tx.Amount <= 0 ? Result.BadRequest("SGB interest amount must be greater than zero") :
                    Result.Success("Valid"),
                TransactionType.Maturity =>
                    tx.Amount <= 0 ? Result.BadRequest("Maturity amount must be greater than zero") :
                    Result.Success("Valid"),
                _ => Result.BadRequest($"Transaction type '{tx.Type}' is not supported for Gold instruments")
            };
        }

        public async Task<HoldingSnapshot> ComputeHoldingAsync(Guid profileId, Guid instrumentId, DateTime asOfUtc, CancellationToken ct)
        {
            var transactions = await _context.Transactions
                .Where(t => t.ProfileId == profileId && t.InstrumentId == instrumentId)
                .ToListAsync(ct);

            var buys = transactions.Where(t => t.Type == TransactionType.Buy).ToList();
            var sells = transactions.Where(t => t.Type == TransactionType.Sell).ToList();

            var totalBuyGrams = buys.Sum(t => t.Quantity);
            var totalSellGrams = sells.Sum(t => t.Quantity);
            var netGrams = totalBuyGrams - totalSellGrams;

            var avgCostPerGram = totalBuyGrams > 0
                ? buys.Sum(t => t.Quantity * t.Price) / totalBuyGrams
                : 0m;

            var latestPrice = await _context.PriceHistories
                .Where(ph => ph.InstrumentId == instrumentId && ph.Date <= asOfUtc)
                .OrderByDescending(ph => ph.Date)
                .Select(ph => (decimal?)ph.Price)
                .FirstOrDefaultAsync(ct);

            var currentPrice = latestPrice ?? (buys.Count > 0
                ? buys.OrderByDescending(t => t.TransactionDate).First().Price
                : 0m);

            return new HoldingSnapshot(
                Quantity: netGrams,
                AvgPrice: avgCostPerGram,
                CurrentPrice: currentPrice,
                MarketValue: netGrams * currentPrice,
                UnrealizedPnL: (currentPrice - avgCostPerGram) * netGrams,
                RealizedPnL: 0m,
                AccruedInterest: 0m,
                Snapshot: null);
        }

        public async Task<decimal?> FetchCurrentPriceAsync(Instrument inst, CancellationToken ct)
        {
            return await _context.PriceHistories
                .Where(ph => ph.InstrumentId == inst.Id)
                .OrderByDescending(ph => ph.Date)
                .Select(ph => (decimal?)ph.Price)
                .FirstOrDefaultAsync(ct);
        }
    }
}
