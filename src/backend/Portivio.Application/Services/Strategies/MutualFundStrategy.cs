using Microsoft.EntityFrameworkCore;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using System.Text.Json;

namespace Portivio.Application.Services.Strategies
{
    public class MutualFundStrategy : IAssetStrategy
    {
        private readonly PortivioDbContext _context;

        public MutualFundStrategy(PortivioDbContext context)
        {
            _context = context;
        }

        public AssetCategory Category => AssetCategory.MutualFund;

        public Result ValidateInstrumentMetadata(JsonDocument? meta) => Result.Success("Valid");

        public Result ValidateTransaction(Transaction tx, Instrument inst)
        {
            return tx.Type switch
            {
                TransactionType.Buy or TransactionType.Sell =>
                    tx.Quantity <= 0 ? Result.BadRequest("Units must be greater than zero for Buy/Sell") :
                    tx.Price <= 0 ? Result.BadRequest("NAV must be greater than zero for Buy/Sell") :
                    Result.Success("Valid"),
                TransactionType.Dividend =>
                    tx.Amount <= 0 ? Result.BadRequest("Amount must be greater than zero for Dividend") :
                    Result.Success("Valid"),
                TransactionType.BonusUnits or TransactionType.Split or TransactionType.Merger =>
                    tx.Quantity <= 0 ? Result.BadRequest("Units must be greater than zero for corporate actions") :
                    Result.Success("Valid"),
                _ => Result.BadRequest($"Transaction type '{tx.Type}' is not supported for Mutual Fund instruments")
            };
        }

        public Task<HoldingSnapshot> ComputeHoldingAsync(Holding holding, DateTime asOfUtc, IEnumerable<Transaction> transactions, decimal? latestPrice, CancellationToken ct)
        {
            var netUnits = transactions.Sum(t => t.Type switch
            {
                TransactionType.Buy or TransactionType.BonusUnits or TransactionType.Split or TransactionType.Merger => t.Quantity,
                TransactionType.Sell => -t.Quantity,
                _ => 0m
            });

            var buys = transactions.Where(t => t.Type == TransactionType.Buy).ToList();
            var totalBuyUnits = buys.Sum(t => t.Quantity);
            var avgNav = totalBuyUnits > 0 ? buys.Sum(t => t.Quantity * t.Price) / totalBuyUnits : 0m;

            var currentNav = latestPrice ?? (buys.Count > 0
                ? buys.OrderByDescending(t => t.TransactionDate).First().Price
                : 0m);

            return Task.FromResult(new HoldingSnapshot(
                Quantity: netUnits,
                AvgPrice: avgNav,
                CurrentPrice: currentNav,
                MarketValue: netUnits * currentNav,
                UnrealizedPnL: (currentNav - avgNav) * netUnits,
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
