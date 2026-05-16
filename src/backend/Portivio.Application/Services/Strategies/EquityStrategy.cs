using Microsoft.EntityFrameworkCore;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using System.Text.Json;

namespace Portivio.Application.Services.Strategies
{
    public class EquityStrategy : IAssetStrategy
    {
        private readonly PortivioDbContext _context;

        public EquityStrategy(PortivioDbContext context)
        {
            _context = context;
        }

        public AssetCategory Category => AssetCategory.Equity;

        public Result ValidateInstrumentMetadata(JsonDocument? meta) => Result.Success("Valid");

        public Result ValidateTransaction(Transaction tx, Instrument inst)
        {
            return tx.Type switch
            {
                TransactionType.Buy or TransactionType.Sell =>
                    tx.Quantity <= 0 ? Result.BadRequest("Quantity must be greater than zero for Buy/Sell") :
                    tx.Price <= 0 ? Result.BadRequest("Price must be greater than zero for Buy/Sell") :
                    Result.Success("Valid"),
                TransactionType.Dividend or TransactionType.Interest =>
                    tx.Amount <= 0 ? Result.BadRequest("Amount must be greater than zero for Dividend/Interest") :
                    Result.Success("Valid"),
                _ => Result.BadRequest($"Transaction type '{tx.Type}' is not supported for Equity instruments")
            };
        }

        public Task<HoldingSnapshot> ComputeHoldingAsync(Holding holding, DateTime asOfUtc, IEnumerable<Transaction> transactions, decimal? latestPrice, CancellationToken ct)
        {
            var buys = transactions.Where(t => t.Type == TransactionType.Buy).ToList();
            var sells = transactions.Where(t => t.Type == TransactionType.Sell).ToList();

            var totalBuyQty = buys.Sum(t => t.Quantity);
            var totalSellQty = sells.Sum(t => t.Quantity);
            var netQty = totalBuyQty - totalSellQty;

            var avgPrice = totalBuyQty > 0
                ? buys.Sum(t => t.Quantity * t.Price) / totalBuyQty
                : 0m;

            var currentPrice = latestPrice ?? (buys.Count > 0
                ? buys.OrderByDescending(t => t.TransactionDate).First().Price
                : 0m);

            var marketValue = netQty * currentPrice;
            var unrealizedPnL = (currentPrice - avgPrice) * netQty;

            return Task.FromResult(new HoldingSnapshot(
                Quantity: netQty,
                AvgPrice: avgPrice,
                CurrentPrice: currentPrice,
                MarketValue: marketValue,
                UnrealizedPnL: unrealizedPnL,
                RealizedPnL: 0m,
                AccruedInterest: 0m,
                Snapshot: null));
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
