using Microsoft.EntityFrameworkCore;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using System.Text.Json;

namespace Portivio.Application.Services.Strategies
{
    public class RecurringDepositStrategy : IAssetStrategy
    {
        private readonly PortivioDbContext _context;

        public RecurringDepositStrategy(PortivioDbContext context)
        {
            _context = context;
        }

        public AssetCategory Category => AssetCategory.RecurringDeposit;

        public Result ValidateInstrumentMetadata(JsonDocument? meta)
        {
            if (meta == null) return Result.BadRequest("RD metadata is required");
            var root = meta.RootElement;
            if (!root.TryGetProperty("monthly", out _)) return Result.BadRequest("RD metadata missing 'monthly'");
            if (!root.TryGetProperty("rate", out _)) return Result.BadRequest("RD metadata missing 'rate'");
            if (!root.TryGetProperty("startDate", out _)) return Result.BadRequest("RD metadata missing 'startDate'");
            if (!root.TryGetProperty("tenureMonths", out _)) return Result.BadRequest("RD metadata missing 'tenureMonths'");
            return Result.Success("Valid");
        }

        public Result ValidateTransaction(Transaction tx, Instrument inst)
        {
            return tx.Type switch
            {
                TransactionType.Contribution =>
                    tx.Amount <= 0 ? Result.BadRequest("Amount must be greater than zero for Contribution") : Result.Success("Valid"),
                TransactionType.Maturity or TransactionType.Withdrawal =>
                    tx.Amount <= 0 ? Result.BadRequest("Amount must be greater than zero") : Result.Success("Valid"),
                _ => Result.BadRequest($"Transaction type '{tx.Type}' is not supported for Recurring Deposit instruments")
            };
        }

        public async Task<HoldingSnapshot> ComputeHoldingAsync(Guid profileId, Guid instrumentId, DateTime asOfUtc, CancellationToken ct)
        {
            var transactions = await _context.Transactions
                .Where(t => t.ProfileId == profileId && t.InstrumentId == instrumentId)
                .ToListAsync(ct);

            var contributions = transactions.Where(t => t.Type == TransactionType.Contribution).Sum(t => t.Amount);
            var withdrawals = transactions.Where(t => t.Type is TransactionType.Withdrawal or TransactionType.Maturity).Sum(t => t.Amount);
            var netContributed = contributions - withdrawals;

            if (netContributed <= 0)
                return new HoldingSnapshot(0m, 0m, 0m, 0m, 0m, 0m, 0m, null);

            var instrument = await _context.Instruments.FindAsync(new object[] { instrumentId }, ct);
            var accruedInterest = instrument?.Metadata != null
                ? ComputeRdInterest(instrument.Metadata, transactions.Count(t => t.Type == TransactionType.Contribution), asOfUtc)
                : 0m;

            var currentValue = netContributed + accruedInterest;

            return new HoldingSnapshot(
                Quantity: 1m,
                AvgPrice: netContributed,
                CurrentPrice: currentValue,
                MarketValue: currentValue,
                UnrealizedPnL: accruedInterest,
                RealizedPnL: 0m,
                AccruedInterest: accruedInterest,
                Snapshot: null);
        }

        public async Task<decimal?> FetchCurrentPriceAsync(Instrument inst, CancellationToken ct)
        {
            if (inst.Metadata == null) return null;
            var contributions = await _context.Transactions
                .CountAsync(t => t.InstrumentId == inst.Id && t.Type == TransactionType.Contribution, ct);
            return (decimal?)ComputeRdInterest(inst.Metadata, contributions, DateTime.UtcNow);
        }

        // RD maturity using standard formula: M = R × [(1+i)^n - 1] / (1-(1+i)^(-1/3))
        // Simplified: sum of each installment compounded quarterly to maturity
        private static decimal ComputeRdInterest(JsonDocument meta, int installmentsPaid, DateTime asOfUtc)
        {
            var root = meta.RootElement;
            if (!root.TryGetProperty("monthly", out var mProp)) return 0m;
            if (!root.TryGetProperty("rate", out var rProp)) return 0m;
            if (!root.TryGetProperty("startDate", out var sProp)) return 0m;
            if (!root.TryGetProperty("tenureMonths", out var tProp)) return 0m;

            var monthly = mProp.GetDecimal();
            var annualRate = rProp.GetDecimal() / 100m;
            var tenureMonths = tProp.GetInt32();

            if (!DateTime.TryParse(sProp.GetString(), out var startDate)) return 0m;

            var maturityDate = startDate.AddMonths(tenureMonths);
            var effectiveEnd = asOfUtc < maturityDate ? asOfUtc : maturityDate;

            var paid = Math.Min(installmentsPaid, tenureMonths);
            var totalDeposited = monthly * paid;

            // Simple interest approximation for partially-accrued RD
            var avgHoldingYears = (decimal)(effectiveEnd - startDate).TotalDays / 365.25m / 2m;
            var interest = totalDeposited * annualRate * avgHoldingYears;
            return interest;
        }
    }
}
