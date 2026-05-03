# Manual Investment & Asset-Flexible Backend — Progress Tracker

> **Purpose:** survives chat dismissal / token exhaustion. Single source of truth for what's done, what's queued, and where each implementation lives. Update after every completed step.
>
> **Design doc:** `docs/manual-investment-design.md`
> **Current branch:** `main`
> **Last updated:** 2026-05-03 (Phase 1 + Phase 2.1–2.6 + Phase 3 complete; 2.7 + Phase 4–5 queued)

---

## Phase 1 — Foundations (no schema break)

| # | Task | Status | Files touched | Notes |
|---|------|--------|--------------|-------|
| 0 | Write `docs/manual-investment-design.md` | ✅ Done | `docs/manual-investment-design.md` | full architecture + phased plan |
| 1 | Wrap `TransactionService.Create/Update/Delete` in DB transaction | ✅ Done | `Portivio.Application/Services/TransactionService.cs` | `ExecuteInTransactionAsync` helpers; no-op on non-relational provider so InMemory tests still pass |
| 2 | Extract `IProfileAccessGuard`; replace 14 duplicated owner-checks | ✅ Done | new `Portivio.Application/Services/Authorization/IProfileAccessGuard.cs`; updated `TransactionService`, `HoldingService`, `SIPPlanService`, `PortfolioPerformanceService`; DI in `Portivio.API/Extensions/ApplicationServicesExtensions.cs`; `Result.ToFailure()` / `Result.ToFailure<T>()` extensions added in `ResultExtensions.cs`; tests updated to construct `new ProfileAccessGuard(context)` | `ProfileService` + `PriceHistoryService` skipped (different patterns) |
| 3 | DTO `Type` → `TransactionType` enum + global `JsonStringEnumConverter` | ✅ Done | `Portivio.Application/DTOs/Transaction/TransactionRequests.cs`; `Portivio.Application/Services/TransactionService.cs`; `Portivio.API/Program.cs` (AddJsonOptions); tests updated | wire format unchanged ("Buy" string) |
| 4 | Composite unique `(AssetTypeId, Symbol)` on `Instruments` | ✅ Done | `Portivio.Infrastructure/Data/Configurations/InstrumentConfiguration.cs`; `InstrumentService.Create/Update` scope `symbolExists` by AssetTypeId; migration `20260502210804_AddInstrumentAssetTypeSymbolUnique` | DB had no prior unique on Symbol → no DROP needed. Pre-flight before deploy: `SELECT "AssetTypeId", LOWER("Symbol"), COUNT(*) FROM "Instruments" GROUP BY 1,2 HAVING COUNT(*)>1;` |
| 5 | Index `(ProfileId, TransactionDate DESC)` for paginated list | ✅ Done | `Portivio.Infrastructure/Data/Configurations/TransactionConfiguration.cs`; migration `20260502211049_AddTransactionProfileDateIndex` | speeds up `GetTransactionsAsync` ORDER BY TransactionDate DESC |
| 6 | `PagedResult<T>` wrap on `GetTransactionsAsync` | ✅ Done | `Portivio.Application/Results/PagedResult.cs` (new); `Portivio.Application/Services/TransactionService.cs`; `Portivio.Tests/Services/TransactionServiceTests.cs` (asserts on `Items`/`Total`/`Page`/`PageSize`/`HasMore`) | Service clamps `page>=1`, `1<=pageSize<=200`. Controller already passes `result.Data` through → JSON `{ items, page, pageSize, total, hasMore }` via default camelCase. Single `CountAsync` + paged `Select` query. |

### Phase 1 build/test status
- All 99 tests pass after each step.
- `dotnet build Portivio.slnx` clean (0 warnings, 0 errors).
- 2 new EF migrations pending DB apply (auto-applied on next API startup via `RunWithMigrationAsync()` in `Program.cs`).

---

## Phase 2 — Ingest pipeline (schema-additive)

| # | Task | Status |
|---|------|--------|
| 2.1 | Add nullable `Instrument.Category, PriceSource, PriceSourceKey, Metadata (jsonb), Isin` + backfill `Category` from AssetType.Name | ✅ Done | New enums `AssetCategory`, `PriceSource` in `Portivio.Domain/Enums/`. Fields added to `Instrument` entity. `InstrumentConfiguration`: int conversions for enums, string↔JsonDocument value converter for InMemory compat, `HasColumnType("jsonb")`, Isin index. Migration `20260503070704_AddInstrumentCategoryPriceSourceMetadata`: backfill CASE on `AssetType.Name`, GIN index via raw SQL. RowVersion deferred — xmin concurrency handled per-entity when needed. |
| 2.2 | Add `Transaction.ClientTxnId, Source, IsDeleted, CreatedAtUtc, UpdatedAtUtc` + backfill | ✅ Done | New enum `TransactionSource`. Fields added to `Transaction`. `TransactionConfiguration`: `Source` int conversion, `(ProfileId, ClientTxnId)` partial unique index (filtered on NOT NULL), `HasQueryFilter(!IsDeleted)`. Migration `20260503070918_AddTransactionIngestFields` backfills `Source=0, timestamps=TransactionDate`. `TransactionService`: `Create` sets `Source=Manual + timestamps`; `Update` sets `UpdatedAtUtc`; `Delete` → soft-delete (`IsDeleted=true`). RowVersion deferred. |
| 2.3 | Add `Holding.RealizedPnL, AccruedInterest, Snapshot (jsonb)` | ✅ Done | Fields added to `Holding`. `HoldingConfiguration`: precision 18,4 for new decimals, string↔JsonDocument value converter for `Snapshot` (InMemory compat), `HasColumnType("jsonb")`. Migration `20260503_AddHoldingPnLSnapshot`. RowVersion deferred. |
| 2.4 | Introduce `IAssetStrategy` + `EquityStrategy` + `AssetStrategyResolver` | ✅ Done | New files under `Portivio.Application/Services/Strategies/`: `IAssetStrategy.cs` (interface + `HoldingSnapshot` record), `EquityStrategy.cs` (Buy/Sell weighted-avg, Dividend/Interest validation, price from PriceHistory), `AssetStrategyResolver.cs`. DI: `AddScoped<IAssetStrategy, EquityStrategy>` + `AddScoped<AssetStrategyResolver>` in `ApplicationServicesExtensions`. |
| 2.5 | `TransactionIngestService.IngestAsync(userId, cmd, source, ct)` — controller delegates | ✅ Done | New `Portivio.Application/Services/TransactionIngestService.cs`: `TransactionCommand` record + `ITransactionIngestService` + impl. Pipeline: profile guard → load instrument → `strategy.ValidateTransaction` → idempotency probe (ClientTxnId) → `BeginTransactionAsync` → insert txn → `SaveChangesAsync` → `strategy.ComputeHoldingAsync` → upsert holding → `SaveChangesAsync` → commit. `TransactionController.CreateTransaction` delegates to `_ingestService.IngestAsync(TransactionSource.Manual)`. |
| 2.6 | Idempotency probe via `(ProfileId, ClientTxnId)` unique index | ✅ Done | DB-level: `ux_transactions_profile_clienttxnid` partial unique index (added in 2.2). App-level: `IngestAsync` probes for existing row by `(ProfileId, ClientTxnId)` before inserting — returns original `TransactionResponse` on match. Tests in `TransactionIngestServiceTests.cs`: idempotent repeat returns same id, count stays 1. |
| 2.7 | Remove `HoldingController.UpsertHolding` POST surface (coordinate with frontend) | ⏳ Queued — breaking |

---

## Phase 3 — Asset coverage (parallelizable per asset)

| # | Task | Status |
|---|------|--------|
| 3.0 | Expand `TransactionType` enum | ✅ Done | Added `Deposit/Contribution/Withdrawal/Maturity/BonusUnits/Split/Merger/Charge/Tax` (int values 4–12). Backward-compatible. |
| 3.1 | `MutualFundStrategy` + `POST /api/profiles/{id}/assets/mutual-fund` | ✅ Done | `MutualFundStrategy`: Buy/Sell/Dividend/BonusUnits/Split/Merger validation; weighted avg NAV; `FetchCurrentPriceAsync` from PriceHistory. |
| 3.2 | `FixedDepositStrategy` (`AccrualFormula`) + `POST /assets/fixed-deposit` | ✅ Done | Compound interest from metadata (principal, rate, compounding, startDate, maturityDate). Deposit/Maturity/Withdrawal/Interest validation. |
| 3.3 | `RecurringDepositStrategy` (`AccrualFormula`) + `POST /assets/recurring-deposit` | ✅ Done | Simple interest on accumulated installments. Contribution/Maturity/Withdrawal validation. |
| 3.4 | `PpfStrategy` (`AccrualFormula`) + `POST /assets/ppf` + `POST /assets/ppf/contributions` | ✅ Done | Annual compound interest from metadata `currentRate`; `FetchCurrentPriceAsync` from PriceHistory (StandardRateService syncs). |
| 3.5 | `GoldStrategy` (Manual price) + `POST /assets/gold` | ✅ Done | Weight-in-grams as quantity; price per gram; Buy/Sell/Dividend(SGB)/Maturity. |
| — | `AssetInstrumentService` + `AssetController` | ✅ Done | New `IAssetInstrumentService`/`AssetInstrumentService` in Application. `AssetController` at `api/profiles/{profileId}/assets/{type}` — find-or-create instrument with Category/PriceSource/Metadata + call `IngestAsync`. All 5 asset types + PPF contributions. 16 new tests; 120/120 pass. |

---

## Phase 4 — SIP + outbox

| # | Task | Status |
|---|------|--------|
| 4.1 | `SipExecutionJob` Hangfire recurring (daily 02:00 IST) — calls `IngestAsync` with `ClientTxnId = sip:{planId}:{yyyyMMdd}` | ⏳ Queued |
| 4.2 | `OutboxMessage` table + `OutboxDispatcherJob` Hangfire | ⏳ Queued |
| 4.3 | MediatR domain events: `TransactionCommittedEvent` → `PerformanceSnapshotHandler`, `SipNextDueRecalcHandler` | ⏳ Queued |

---

## Phase 5 — Tax & reports

| # | Task | Status |
|---|------|--------|
| 5.1 | `TaxLot` table + FIFO/LIFO consumption inside Equity/MF/Gold strategies | ⏳ Queued |
| 5.2 | `Profile.CostBasisMethod` enum field (WeightedAverage/FIFO/LIFO) | ⏳ Queued |
| 5.3 | Realized PnL endpoint + STCG/LTCG split report | ⏳ Queued |

---

## Migrations created so far

| Filename | Up summary |
|----------|-----------|
| `20260502210804_AddInstrumentAssetTypeSymbolUnique.cs` | `CREATE UNIQUE INDEX ux_instruments_assettype_symbol ON "Instruments" ("AssetTypeId", "Symbol")` |
| `20260502211049_AddTransactionProfileDateIndex.cs` | `CREATE INDEX idx_transactions_profile_date_desc ON "Transactions" ("ProfileId", "TransactionDate" DESC)` |

Apply manually: `dotnet ef database update --project Portivio.Infrastructure --startup-project Portivio.API` (or rely on startup `RunWithMigrationAsync()`).

---

## Resume protocol (if conversation lost)

1. Read `docs/manual-investment-design.md` for architecture context.
2. Read this file for status.
3. Find first row marked `⏳ Queued` — that's the next task.
4. Run `dotnet test Portivio.Tests/Portivio.Tests.csproj` to confirm baseline (should be 99/99 pass).
5. Run `git log --oneline -5` to confirm latest commit.
6. Continue.

## Conventions reminder (from CLAUDE.md)

- Result pattern returned from services — controllers map via `result.Match(...)`.
- Central Package Management — pin versions only in `Directory.Packages.props`.
- New entity = `DbSet<T>` on `PortivioDbContext` AND configuration class in `Data/Configurations/`.
- EF migrations require both `--project Portivio.Infrastructure --startup-project Portivio.API` flags.
- Tests use `EntityFrameworkCore.InMemory` — relational features (transactions, raw SQL) must be guarded by `_context.Database.IsRelational()`.
