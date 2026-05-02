# Manual Investment & Asset-Flexible Backend — Design

> **Status:** proposed · **Author:** Portivio core · **Last updated:** 2026-05-03
>
> Scope: redesign the backend ingest path for investments so that (a) manual entry, SIP automation, and external imports share the same pipeline, (b) new asset types (MF, Stocks, FD/RD, Gold, PPF, future EPF/Bonds/Crypto/Real-Estate) can be added without breaking existing rows, and (c) NAV / stock-price tracking remains decoupled from transaction writes.

---

## 1. Goals

1. **One ingest pipeline.** Manual UI, SIP background job, and CSV/API imports all funnel through a single `TransactionIngestService.IngestAsync(...)`.
2. **Schema-stable polymorphism.** Adding a new asset category never requires altering existing tables or backfilling old rows.
3. **Holdings are derived, not authored.** Transactions are the single source of truth; holdings are projections recomputed by per-asset strategies.
4. **Idempotent + concurrency-safe.** SIP retries, dual submits, and concurrent edits cannot produce duplicate transactions or lost updates.
5. **Price ingestion stays independent.** `PriceHistory` continues to be written by market-data jobs; holdings react via events, not synchronous calls.
6. **Tax-ready.** Cost-basis lots, realized PnL, and accrued interest are first-class so STCG/LTCG and FD/PPF interest reports become trivial.

## 2. Non-goals (this iteration)

- Real-time WebSocket price streams.
- FX-converted aggregate dashboards (groundwork only — full FX rollups deferred).
- Goal/bucket linkage UI (entity reserved, surfaced later).
- Broker integrations (Zerodha/Groww APIs) — covered by import pipeline once ingest is stable.

## 3. Current state (May 2026)

| Concern | Today | Pain |
|---|---|---|
| Instrument shape | flat `Name/Symbol/Currency/AssetTypeId` | no room for FD rate, PPF account, gold purity, MF folio |
| Holding source of truth | `UpsertHoldingAsync` POST + `RecalculateHoldingFromTransactionsAsync` | client can drift holding away from transactions |
| Atomicity | `Transactions.Add` + `SaveChanges` then recalc + `SaveChanges` | crash mid-recalc leaves orphan txn |
| Transaction types | `Buy / Sell / Dividend / Interest` | no `Deposit / Contribution / Withdrawal / Maturity / Charge` |
| Cost basis | weighted average over Buys only | no FIFO lots, no realized PnL, no tax report |
| Concurrency | none | SIP job + manual edit can lost-update holding |
| Idempotency | none | SIP Hangfire retry can double-buy |
| Symbol uniqueness | global | PPF "PPF" collides with FD "PPF-Bank" |
| Profile ownership check | duplicated 12+ places | drift risk |
| Currency | normalized to upper-case but never converted | aggregates lie across USD/INR |
| DTO `Type` | `string`, parsed each call | boilerplate, weak typing |

## 4. Target architecture

### 4.1 Polymorphism via JSONB metadata

Add to `Instrument`:

```csharp
public class Instrument {
    public Guid Id { get; set; }
    public Guid AssetTypeId { get; set; }
    public AssetCategory Category { get; set; }      // enum drives behavior
    public string Name { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string? Isin { get; set; }
    public string Currency { get; set; } = null!;
    public PriceSource PriceSource { get; set; }     // None|AlphaVantage|AmfiNav|Manual|AccrualFormula
    public string? PriceSourceKey { get; set; }      // AMFI scheme code, AV ticker, etc.
    public JsonDocument? Metadata { get; set; }      // jsonb — asset-specific shape
    public byte[] RowVersion { get; set; } = null!;
    // existing navigations...
}

public enum AssetCategory {
    Equity, MutualFund, FixedDeposit, RecurringDeposit,
    Ppf, Epf, Gold, Bond, Crypto, RealEstate, Cash, Custom
}

public enum PriceSource { None, AlphaVantage, AmfiNav, Manual, AccrualFormula }
```

EF configuration:

```csharp
builder.Property(i => i.Metadata).HasColumnType("jsonb");
builder.Property(i => i.Category).HasConversion<int>();
builder.Property(i => i.PriceSource).HasConversion<int>();
builder.Property(i => i.RowVersion).IsRowVersion();
builder.HasIndex(i => new { i.AssetTypeId, i.Symbol }).IsUnique();
builder.HasIndex(i => i.Isin);
// Postgres GIN for metadata filtering
builder.HasIndex(i => i.Metadata).HasMethod("gin");
```

### 4.2 Strategy registry

```csharp
public interface IAssetStrategy {
    AssetCategory Category { get; }
    Result ValidateInstrumentMetadata(JsonDocument? meta);
    Result ValidateTransaction(Transaction tx, Instrument inst);
    Task<HoldingSnapshot> ComputeHoldingAsync(Guid profileId, Guid instrumentId, DateTime asOfUtc, CancellationToken ct);
    Task<decimal?> FetchCurrentPriceAsync(Instrument inst, CancellationToken ct);
}

public sealed record HoldingSnapshot(
    decimal Quantity,
    decimal AvgPrice,
    decimal CurrentPrice,
    decimal MarketValue,
    decimal UnrealizedPnL,
    decimal RealizedPnL,
    decimal AccruedInterest,
    JsonDocument? Snapshot);

public class AssetStrategyResolver {
    private readonly IReadOnlyDictionary<AssetCategory, IAssetStrategy> _map;
    public AssetStrategyResolver(IEnumerable<IAssetStrategy> strats) =>
        _map = strats.ToDictionary(s => s.Category);
    public IAssetStrategy For(AssetCategory c) =>
        _map.TryGetValue(c, out var s) ? s : throw new NotSupportedException($"No strategy for {c}");
}
```

DI registration (one line per asset, additive forever):

```csharp
services.AddScoped<IAssetStrategy, EquityStrategy>();
services.AddScoped<IAssetStrategy, MutualFundStrategy>();
services.AddScoped<IAssetStrategy, FixedDepositStrategy>();
services.AddScoped<IAssetStrategy, RecurringDepositStrategy>();
services.AddScoped<IAssetStrategy, PpfStrategy>();
services.AddScoped<IAssetStrategy, GoldStrategy>();
services.AddSingleton<AssetStrategyResolver>();
```

### 4.3 Single ingest pipeline

```csharp
public sealed record TransactionCommand(
    Guid ProfileId,
    Guid InstrumentId,
    TransactionType Type,
    decimal Quantity,
    decimal Price,
    decimal Amount,
    DateTime TransactionDateUtc,
    string? Notes,
    string? ClientTxnId);

public enum TransactionSource { Manual, Sip, Import, PriceJob }

public interface ITransactionIngestService {
    Task<Result<TransactionResponse>> IngestAsync(
        Guid userId, TransactionCommand cmd, TransactionSource source, CancellationToken ct);
}
```

Pipeline order inside `IngestAsync`:

1. `IProfileAccessGuard.EnsureOwner(userId, profileId)` → `Result`.
2. Load `Instrument` + `Category`.
3. `strategy.ValidateTransaction(...)` — strategy enforces type/qty/price rules.
4. Idempotency probe: if `ClientTxnId` set and `(ProfileId, ClientTxnId)` row exists → return existing response.
5. `BeginTransactionAsync()`.
6. Insert `Transaction`.
7. `strategy.ComputeHoldingAsync(...)` → upsert `Holding`.
8. Append `OutboxMessage` for `TransactionCommittedEvent`.
9. `CommitAsync()`.
10. Return mapped DTO.

Manual controller path:

```csharp
[HttpPost]
public async Task<IActionResult> Create(Guid profileId, [FromBody] CreateTransactionRequest req, CancellationToken ct) {
    var userId = User.RequireUserId();
    var cmd = req.ToCommand(profileId);
    var result = await _ingest.IngestAsync(userId, cmd, TransactionSource.Manual, ct);
    return result.ToActionResult();
}
```

SIP background job path (Hangfire recurring, see §4.5).

### 4.4 Transaction expansion

```csharp
public enum TransactionType {
    Buy, Sell,
    Dividend, Interest,
    Deposit,        // FD/RD/PPF principal in
    Contribution,   // recurring PPF/RD installment
    Withdrawal,     // partial PPF, FD break
    Maturity,       // FD/RD payout
    BonusUnits, Split, Merger,
    Charge,         // making charge, exit load, advisory fee
    Tax             // TDS, STT
}
```

```csharp
public class Transaction {
    // existing core...
    public string? ClientTxnId { get; set; }
    public TransactionSource Source { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
```

Indexes:

```csharp
builder.HasIndex(t => new { t.ProfileId, t.ClientTxnId })
       .IsUnique()
       .HasFilter("\"ClientTxnId\" IS NOT NULL");
builder.HasIndex(t => new { t.ProfileId, t.TransactionDate })
       .IsDescending(false, true);
builder.HasQueryFilter(t => !t.IsDeleted);
```

### 4.5 SIP execution (future-ready hook)

```csharp
public class SipExecutionJob {
    public async Task RunDueInstallmentsAsync(DateTime today, CancellationToken ct) {
        var due = await _db.SIPPlans
            .AsNoTracking()
            .Where(s => s.IsActive && s.SIPDay == today.Day && s.StartDate <= today && s.EndDate >= today)
            .ToListAsync(ct);

        foreach (var sip in due) {
            var inst = await _db.Instruments.FirstAsync(i => i.Id == sip.InstrumentId, ct);
            var price = await _strategies.For(inst.Category).FetchCurrentPriceAsync(inst, ct)
                        ?? throw new InvalidOperationException($"No price for {inst.Id}");
            var qty = sip.Amount / price;

            await _ingest.IngestAsync(sip.Profile.UserId, new TransactionCommand(
                ProfileId: sip.ProfileId,
                InstrumentId: sip.InstrumentId,
                Type: TransactionType.Buy,
                Quantity: qty,
                Price: price,
                Amount: sip.Amount,
                TransactionDateUtc: today.ToUniversalTime(),
                Notes: $"SIP installment {today:yyyy-MM-dd}",
                ClientTxnId: $"sip:{sip.Id}:{today:yyyyMMdd}"
            ), TransactionSource.Sip, ct);
        }
    }
}
```

`ClientTxnId = sip:{planId}:{yyyyMMdd}` makes Hangfire retries idempotent at the DB level.

### 4.6 Holding projection

```csharp
public class Holding {
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public Guid InstrumentId { get; set; }
    public decimal Quantity { get; set; }
    public decimal AvgPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal RealizedPnL { get; set; }       // new
    public decimal AccruedInterest { get; set; }   // new (FD/PPF)
    public JsonDocument? Snapshot { get; set; }    // jsonb (lot digest, accrual schedule, etc.)
    public DateTime LastUpdated { get; set; }
    public byte[] RowVersion { get; set; } = null!;
    // navigations...
}
```

`HoldingService.UpsertHoldingAsync` is removed from controller surface. The only writers are:

- `TransactionIngestService` (after each ingest)
- `MarketDataPollingJob` (price refresh → recompute `CurrentPrice / MarketValue / UnrealizedPnL` only)

### 4.7 Tax lots (cost basis)

```csharp
public class TaxLot {
    public Guid Id { get; set; }
    public Guid HoldingId { get; set; }
    public Guid BuyTransactionId { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal CostPerUnit { get; set; }
    public DateTime AcquiredOnUtc { get; set; }
}

public enum CostBasisMethod { WeightedAverage, Fifo, Lifo }
```

Equity / MF / Gold strategies populate lots; Sell consumes per `Profile.CostBasisMethod` and writes `RealizedPnL`. FD / PPF strategies skip lots entirely.

### 4.8 Price ingestion (unchanged contract, decoupled wiring)

```csharp
public class PriceIngestService {
    public async Task RecordPriceAsync(Guid instrumentId, decimal price, DateTime asOfUtc) {
        _db.PriceHistories.Add(new PriceHistory { InstrumentId = instrumentId, Price = price, Date = asOfUtc });
        await _db.SaveChangesAsync();
        await _holdingService.UpdateCurrentPriceAsync(instrumentId, price);
    }
}
```

`MarketDataPollingJob` iterates `Instruments` where `PriceSource ∈ { AlphaVantage, AmfiNav }`, calls `strategy.FetchCurrentPriceAsync`, persists via `RecordPriceAsync`. FD/PPF use `PriceSource = AccrualFormula` → strategy computes from metadata, no external API.

### 4.9 Domain events + outbox

```csharp
public record TransactionCommittedEvent(
    Guid ProfileId, Guid InstrumentId, Guid TransactionId, TransactionType Type, DateTime AtUtc);

public class OutboxMessage {
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!; // JSON
    public DateTime OccurredOnUtc { get; set; }
    public DateTime? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
}
```

Outbox row written in same DB transaction as the `Transaction` insert. A Hangfire publisher (`OutboxDispatcherJob`) drains pending rows and invokes `INotificationHandler<TransactionCommittedEvent>` handlers (MediatR). Handlers wired:

- `GoalProgressHandler` (later)
- `PerformanceSnapshotHandler`
- `SipNextDueRecalcHandler`

### 4.10 Profile-ownership guard

```csharp
public interface IProfileAccessGuard {
    Task<Result<Profile>> EnsureOwnerAsync(Guid userId, Guid profileId, CancellationToken ct = default);
}
```

Replaces the duplicated three-line check in 12+ service methods.

### 4.11 API surface

Generic (canonical):

```
POST   /api/profiles/{profileId}/transactions
PUT    /api/profiles/{profileId}/transactions/{txId}
DELETE /api/profiles/{profileId}/transactions/{txId}
GET    /api/profiles/{profileId}/transactions?from=&to=&type=&instrumentId=&page=&pageSize=
```

Convenience (calls `IngestAsync` under hood, validates asset metadata up front):

```
POST /api/profiles/{profileId}/instruments/mutual-fund     { schemeCode|isin, plan, option, units, navOnDate, date }
POST /api/profiles/{profileId}/instruments/stock           { exchange, symbol|isin, quantity, price, date }
POST /api/profiles/{profileId}/instruments/fixed-deposit   { bank, accountNo, principal, rate, compounding, payoutFreq, startDate, maturityDate }
POST /api/profiles/{profileId}/instruments/recurring-deposit { bank, accountNo, monthly, rate, startDate, tenureMonths }
POST /api/profiles/{profileId}/instruments/ppf             { accountNo, openedOn, currentRate }
POST /api/profiles/{profileId}/instruments/ppf/contributions { ppfInstrumentId, amount, date }
POST /api/profiles/{profileId}/instruments/gold            { form, purity, weightGrams, ratePerGram, makingCharges, date }
```

### 4.12 Asset-specific metadata shapes

```jsonc
// MutualFund
{ "schemeCode": "120503", "isin": "INF...", "plan": "Direct", "option": "Growth", "amfiCode": "120503", "folio": "..." }

// FixedDeposit
{ "bank": "HDFC", "accountNo": "FD123", "principal": 100000, "rate": 7.1,
  "compounding": "Quarterly", "payoutFreq": "OnMaturity",
  "startDate": "2026-01-15", "maturityDate": "2027-01-15", "prematurePenaltyPct": 1.0 }

// RecurringDeposit
{ "bank": "SBI", "accountNo": "RD555", "monthly": 5000, "rate": 6.7,
  "startDate": "2026-02-01", "tenureMonths": 36 }

// PPF
{ "accountNo": "PPF...", "openedOn": "2020-04-01", "lockInEndsOn": "2035-04-01", "currentRate": 7.1 }

// Gold
{ "form": "Coin", "purity": "24K", "weightGrams": 10, "makingChargesInr": 500,
  "hallmark": "BIS-916", "locker": "SBI-Andheri" }

// Stocks
{ "exchange": "NSE", "isin": "INE...", "sector": "IT", "lotSize": 1 }
```

## 5. Migration sequence (zero downtime, preserves existing rows)

| # | Step | Reversible? | Notes |
|---|------|-------------|-------|
| 1 | Add nullable columns: `Instrument.Category, Isin, PriceSource, PriceSourceKey, Metadata, RowVersion` | yes | backfill `Category` from `AssetType.Name` mapping |
| 2 | Add `Transaction.ClientTxnId, Source, IsDeleted, CreatedAtUtc, UpdatedAtUtc, RowVersion` | yes | backfill `Source=Manual`, timestamps=`TransactionDate` |
| 3 | Add `Holding.RealizedPnL, AccruedInterest, Snapshot, RowVersion` | yes | defaults 0 / null |
| 4 | Drop global `Symbol` unique → add `(AssetTypeId, Symbol)` unique | yes | run online with `IF NOT EXISTS` |
| 5 | Introduce `IAssetStrategy` + `EquityStrategy` only; route through `AssetStrategyResolver` | yes | parity tests vs old service |
| 6 | Wrap ingest + recalc in `BeginTransactionAsync` | yes | drops orphan-state risk |
| 7 | Introduce `TransactionIngestService`; controller delegates | yes | old service kept until callers cut over |
| 8 | Add MutualFund/FD/RD/PPF/Gold strategies behind feature flags per category | yes | one merge per asset |
| 9 | Switch SIP execution to `IngestAsync` | yes | ClientTxnId pattern locks idempotency |
| 10 | Remove `UpsertHoldingAsync` POST after frontend cut-over | no | breaking — coordinate with FE release |
| 11 | Tax-lot table + FIFO consumption inside Equity/MF/Gold strategies | yes | additive |
| 12 | Outbox + MediatR domain events | yes | additive |

## 6. Phased delivery

### Phase 1 — Foundations (no schema break, ~1 sprint)

- [x] Write this design.
- [ ] Wrap `CreateTransactionAsync / UpdateTransactionAsync / DeleteTransactionAsync` in `BeginTransactionAsync` (atomicity).
- [ ] Add `IProfileAccessGuard` + replace duplicated checks across `HoldingService`, `TransactionService`, `InstrumentService`, `SIPPlanService`, `PriceHistoryService`, `PortfolioPerformanceService`.
- [ ] DTO change: `CreateTransactionRequest.Type` → `TransactionType` enum with `JsonStringEnumConverter`.
- [ ] Add migration: composite unique `(AssetTypeId, Symbol)`; drop global `Symbol` unique.
- [ ] Add migration: `(ProfileId, TransactionDate DESC)` index.
- [ ] Wrap `GetTransactionsAsync` response in `PagedResult<T>`.

### Phase 2 — Ingest pipeline (~1 sprint)

- [ ] Add nullable `Instrument.Category / PriceSource / PriceSourceKey / Metadata / Isin / RowVersion` + backfill.
- [ ] Add `Transaction.ClientTxnId / Source / IsDeleted / CreatedAtUtc / UpdatedAtUtc / RowVersion` + backfill.
- [ ] Add `Holding.RealizedPnL / AccruedInterest / Snapshot / RowVersion`.
- [ ] Introduce `IAssetStrategy` + `EquityStrategy` (parity with current behavior).
- [ ] Introduce `TransactionIngestService.IngestAsync`; controller delegates.
- [ ] Idempotency probe via `ClientTxnId`.
- [ ] Remove `HoldingController.UpsertHolding` POST surface (frontend coordination required).

### Phase 3 — Asset coverage (~2 sprints, parallelizable)

- [ ] `MutualFundStrategy` + AMFI NAV price source + `/instruments/mutual-fund` endpoint.
- [ ] `FixedDepositStrategy` (`AccrualFormula`) + `/instruments/fixed-deposit`.
- [ ] `RecurringDepositStrategy` (`AccrualFormula`) + `/instruments/recurring-deposit`.
- [ ] `PpfStrategy` (`AccrualFormula`) + `/instruments/ppf` + `/ppf/contributions`.
- [ ] `GoldStrategy` (`Manual` price or future spot-API) + `/instruments/gold`.

### Phase 4 — SIP + outbox (~1 sprint)

- [ ] `SipExecutionJob` Hangfire recurring at 02:00 IST daily.
- [ ] Outbox table + dispatcher + MediatR.
- [ ] `TransactionCommittedEvent` handlers: `PerformanceSnapshotHandler`, `SipNextDueRecalcHandler`.

### Phase 5 — Tax & reports (~1 sprint)

- [ ] `TaxLot` table + FIFO/LIFO consumption.
- [ ] `Profile.CostBasisMethod` field.
- [ ] Realized PnL endpoint, STCG/LTCG split.

## 7. Tests

- xUnit per strategy (`EquityStrategyTests`, `FixedDepositStrategyTests`, ...).
- `TransactionIngestServiceTests`: idempotency, atomicity (rollback on strategy failure), profile guard.
- `SipExecutionJobTests`: idempotent re-run, missing price short-circuits without partial txn.
- Migration parity test: load fixture data pre-migration, run migrations, assert behavior identical.

## 8. Open questions

1. Cost-basis default per profile — Weighted vs FIFO? (Indian retail usually FIFO for tax.)
2. Multi-currency aggregates — do we ship per-currency rollups in Phase 5 or wait?
3. PPF interest calc — apply lowest-balance-between-5th-and-end-of-month rule from day one, or simple monthly compounding?
4. Gold spot price API — manual entry vs subscribe to GoldAPI / IBJA scraper?
5. Should `AssetType` table survive once `AssetCategory` enum exists, or collapse to category-only?

---

**Next action:** Phase 1, item 1 — wrap transaction ingest in `BeginTransactionAsync`.
