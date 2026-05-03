using Microsoft.EntityFrameworkCore;
using Portivio.Application.Results;
using Portivio.Domain.Entities;
using Portivio.Domain.Enums;
using Portivio.Infrastructure.Data;
using System.Text.Json;

namespace Portivio.Application.Services.Strategies
{
    public class FixedDepositStrategy : IAssetStrategy
    {
        private readonly PortivioDbContext _context;

        public FixedDepositStrategy(PortivioDbContext context)
        {
            _context = context;
        }

        public AssetCategory Category => AssetCategory.FixedDeposit;

        public Result ValidateInstrumentMetadata(JsonDocument? meta)
        {
            if (meta == null) return Result.BadRequest("FD metadata is required");
            var root = meta.RootElement;
            if (!root.TryGetProperty("principal", out _)) return Result.BadRequest("FD metadata missing 'principal'");
            if (!root.TryGetProperty("rate", out _)) return Result.BadRequest("FD metadata missing 'rate'");
            if (!root.TryGetProperty("startDate", out _)) return Result.BadRequest("FD metadata missing 'startDate'");
            if (!root.TryGetProperty("maturityDate", out _)) return Result.BadRequest("FD metadata missing 'maturityDate'");
            return Result.Success("Valid");
        }

        public Result ValidateTransaction(Transaction tx, Instrument inst)
        {
            return tx.Type switch
            {
                TransactionType.Deposit =>
                    tx.Amount <= 0 ? Result.BadRequest("Amount must be greater than zero for Deposit") : Result.Success("Valid"),
                TransactionType.Maturity or TransactionType.Withdrawal =>
                    tx.Amount <= 0 ? Result.BadRequest("Amount must be greater than zero") : Result.Success("Valid"),
                TransactionType.Interest =>
                    tx.Amount <= 0 ? Result.BadRequest("Interest amount must be greater than zero") : Result.Success("Valid"),
                _ => Result.BadRequest($"Transaction type '{tx.Type}' is not supported for Fixed Deposit instruments")
            };
        }

        public async Task<HoldingSnapshot> ComputeHoldingAsync(Guid profileId, Guid instrumentId, DateTime asOfUtc, CancellationToken ct)
        {
            var transactions = await _context.Transactions
                .Where(t => t.ProfileId == profileId && t.InstrumentId == instrumentId)
                .ToListAsync(ct);

            var deposits = transactions.Where(t => t.Type == TransactionType.Deposit).Sum(t => t.Amount);
            var withdrawals = transactions.Where(t => t.Type is TransactionType.Withdrawal or TransactionType.Maturity).Sum(t => t.Amount);
            var netDeposited = deposits - withdrawals;

            if (netDeposited <= 0)
                return new HoldingSnapshot(0m, 0m, 0m, 0m, 0m, 0m, 0m, null);

            var instrument = await _context.Instruments.FindAsync(new object[] { instrumentId }, ct);
            var accruedInterest = instrument?.Metadata != null
                ? ComputeAccruedInterest(instrument.Metadata, asOfUtc)
                : 0m;

            var currentValue = netDeposited + accruedInterest;

            return new HoldingSnapshot(
                Quantity: 1m,
                AvgPrice: netDeposited,
                CurrentPrice: currentValue,
                MarketValue: currentValue,
                UnrealizedPnL: accruedInterest,
                RealizedPnL: 0m,
                AccruedInterest: accruedInterest,
                Snapshot: null);
        }

        public Task<decimal?> FetchCurrentPriceAsync(Instrument inst, CancellationToken ct)
        {
            if (inst.Metadata == null) return Task.FromResult<decimal?>(null);
            var value = ComputeAccruedInterest(inst.Metadata, DateTime.UtcNow);
            var root = inst.Metadata.RootElement;
            var principal = root.TryGetProperty("principal", out var p) ? p.GetDecimal() : 0m;
            return Task.FromResult<decimal?>(principal + value);
        }

        private static decimal ComputeAccruedInterest(JsonDocument meta, DateTime asOfUtc)
        {
            var root = meta.RootElement;
            if (!root.TryGetProperty("principal", out var pProp)) return 0m;
            if (!root.TryGetProperty("rate", out var rProp)) return 0m;
            if (!root.TryGetProperty("startDate", out var sProp)) return 0m;
            if (!root.TryGetProperty("maturityDate", out var mProp)) return 0m;

            var principal = pProp.GetDecimal();
            var annualRate = rProp.GetDecimal() / 100m;
            var compounding = root.TryGetProperty("compounding", out var cProp) ? cProp.GetString() ?? "Quarterly" : "Quarterly";

            if (!DateTime.TryParse(sProp.GetString(), out var startDate)) return 0m;
            if (!DateTime.TryParse(mProp.GetString(), out var maturityDate)) return 0m;

            var effectiveEnd = asOfUtc < maturityDate ? asOfUtc : maturityDate;
            if (effectiveEnd <= startDate) return 0m;

            var n = compounding.ToUpperInvariant() switch
            {
                "MONTHLY" => 12,
                "QUARTERLY" => 4,
                "HALFYEARLY" => 2,
                "YEARLY" => 1,
                _ => 4
            };

            var years = (decimal)(effectiveEnd - startDate).TotalDays / 365.25m;
            var maturity = principal * (decimal)Math.Pow((double)(1 + annualRate / n), (double)(n * years));
            return maturity - principal;
        }
    }
}
