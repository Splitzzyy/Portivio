using Microsoft.EntityFrameworkCore;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using System.Text.Json;

namespace Portivio.Application.Services.Strategies
{
    public class PpfStrategy : IAssetStrategy
    {
        private readonly PortivioDbContext _context;

        public PpfStrategy(PortivioDbContext context)
        {
            _context = context;
        }

        public AssetCategory Category => AssetCategory.Ppf;

        public Result ValidateInstrumentMetadata(JsonDocument? meta)
        {
            if (meta == null) return Result.BadRequest("PPF metadata is required");
            var root = meta.RootElement;
            if (!root.TryGetProperty("accountNo", out _)) return Result.BadRequest("PPF metadata missing 'accountNo'");
            if (!root.TryGetProperty("openedOn", out _)) return Result.BadRequest("PPF metadata missing 'openedOn'");
            if (!root.TryGetProperty("currentRate", out _)) return Result.BadRequest("PPF metadata missing 'currentRate'");
            return Result.Success("Valid");
        }

        public Result ValidateTransaction(Transaction tx, Instrument inst)
        {
            return tx.Type switch
            {
                TransactionType.Contribution =>
                    tx.Amount <= 0 ? Result.BadRequest("Contribution amount must be greater than zero") : Result.Success("Valid"),
                TransactionType.Withdrawal =>
                    tx.Amount <= 0 ? Result.BadRequest("Withdrawal amount must be greater than zero") : Result.Success("Valid"),
                TransactionType.Maturity =>
                    tx.Amount <= 0 ? Result.BadRequest("Maturity amount must be greater than zero") : Result.Success("Valid"),
                _ => Result.BadRequest($"Transaction type '{tx.Type}' is not supported for PPF instruments")
            };
        }

        public Task<HoldingSnapshot> ComputeHoldingAsync(Holding holding, DateTime asOfUtc, IEnumerable<Transaction> transactions, decimal? latestPrice, CancellationToken ct)
        {
            var contributions = transactions.Where(t => t.Type == TransactionType.Contribution).Sum(t => t.Amount);
            var withdrawals = transactions.Where(t => t.Type is TransactionType.Withdrawal or TransactionType.Maturity).Sum(t => t.Amount);
            var netBalance = contributions - withdrawals;

            if (netBalance <= 0)
                return Task.FromResult(new HoldingSnapshot(0m, 0m, 0m, 0m, 0m, 0m, 0m, null));

            var rate = GetCurrentRate(holding.Instrument, holding.InstrumentId);
            var yearsHeld = GetYearsHeld(holding.Instrument, asOfUtc);

            // PPF: annual compounding — simplified to compound on total contributions
            var accruedInterest = netBalance * ((decimal)Math.Pow((double)(1 + rate / 100m), (double)yearsHeld) - 1m);
            var currentValue = netBalance + accruedInterest;

            return Task.FromResult(new HoldingSnapshot(
                Quantity: 1m,
                AvgPrice: netBalance,
                CurrentPrice: currentValue,
                MarketValue: currentValue,
                UnrealizedPnL: accruedInterest,
                RealizedPnL: 0m,
                AccruedInterest: accruedInterest,
                Snapshot: null));
        }

        public async Task<decimal?> FetchCurrentPriceAsync(Instrument inst, CancellationToken ct)
        {
            // Try latest PriceHistory (synced by StandardRateService)
            return await _context.PriceHistories
                .Where(ph => ph.InstrumentId == inst.Id)
                .OrderByDescending(ph => ph.Date)
                .Select(ph => (decimal?)ph.Price)
                .FirstOrDefaultAsync(ct);
        }

        private static decimal GetCurrentRate(Instrument? inst, Guid instrumentId)
        {
            if (inst?.Metadata == null) return 7.1m;
            return inst.Metadata.RootElement.TryGetProperty("currentRate", out var r) ? r.GetDecimal() : 7.1m;
        }

        private static double GetYearsHeld(Instrument? inst, DateTime asOfUtc)
        {
            if (inst?.Metadata == null) return 0;
            if (!inst.Metadata.RootElement.TryGetProperty("openedOn", out var o)) return 0;
            if (!DateTime.TryParse(o.GetString(), out var openedOn)) return 0;
            return Math.Max(0, (asOfUtc - openedOn).TotalDays / 365.25);
        }
    }
}
