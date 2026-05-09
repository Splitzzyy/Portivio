# Technical Requirements — Background Holding Recalculation

> Stack: ASP.NET Core 10 (Clean Architecture) · EF Core 10 · Hangfire 1.8 · PostgreSQL 15 · Angular 18 (NgModules) · Bootstrap 5 · ngx-toastr
> Captured: 2026-05-08

---

## Problem statement

When a user adds a holding (Holdings page → manual upsert) or any other instrument, the row is persisted with the values the user typed (`Quantity`, `AvgPrice`, `CurrentPrice`). The next day — and every day after — the Holdings table still shows those original values. There is no scheduled job that refreshes external prices, recomputes derived fields (`MarketValue`, `UnrealizedPnL`), or accrues interest for FD/RD/PPF. Users perceive the table as stale.

Expected behaviour: every morning, all holdings show fresh `CurrentPrice` / `MarketValue` / `UnrealizedPnL` relative to that day's market data and accrual; users can also trigger a manual recompute on demand.

---

## Goal

Introduce a daily background recalculation pipeline plus a manual per-profile refresh, so Holdings always reflect current price (or current accrual) without requiring users to edit each row.

---

## Scope

### In scope

1. New Hangfire `RecurringJob` that runs daily at 06:00 IST and refreshes prices + recomputes holdings for every category.
2. New gold rate provider sourced from `appsettings`.
3. New manual refresh endpoint + button on the Holdings page (recompute only, no external calls).
4. Holdings table UI: "Last Updated" column + "Refresh prices" button.
5. Misfire handling so a missed scheduled run is replayed on startup.

### Out of scope

- New UI screens beyond the Holdings page changes.
- Realised P&L computation overhaul, % return columns, holding-period columns.
- New external providers for stocks/MFs (AlphaVantage + AMFI providers already exist).
- Real-time push (SignalR / WebSockets).
- A staleness banner on the Holdings page (deferred — only the column + button were chosen).

---

## Decisions

| # | Decision | Choice |
|---|----------|--------|
| 1 | Bug-affected flow | Holdings page manual upsert (and all other holdings, by extension). |
| 2 | Symptom | After a day, table still shows the values the user typed; needs auto-recompute. |
| 3 | Asset categories covered | All — Equity, Mutual Fund, Gold, FD, RD, PPF. |
| 4 | Schedule | Daily at 06:00 IST. |
| 5 | Fields refreshed per holding | `CurrentPrice`, `MarketValue`, `UnrealizedPnL`, `LastUpdated` (+ `AccruedInterest`/`RealizedPnL` written from strategy snapshot for FD/RD/PPF). |
| 6 | Gold rate source | Configurable rate in `appsettings.json` (no external API). |
| 7 | Additional triggers | Manual "Refresh prices" button on the Holdings page, per profile. |
| 8 | UI changes | Add "Refresh prices" button + "Last Updated" column with relative time (e.g. "2h ago"). |
| 9 | Failure mode (daily job) | Per-instrument try/catch, log error, continue; job overall succeeds with an error count. |
| 10 | AlphaVantage throttling | 12-second delay between calls (≤5/min); skip after 500 calls/day. |
| 11 | Manual refresh scope | Per-profile only. Rate-limited to **1 call/min** per user (in addition to the existing `per-user` policy). |
| 12 | Manual refresh API shape | Synchronous: `POST /api/profiles/{profileId}/holdings/refresh` runs recalc inline and returns the refreshed `Holding[]` (200). |
| 13 | Manual refresh depth | Recompute only — uses latest `PriceHistory` + accrual; **no external provider calls**. |
| 14 | Scheduling mechanism | Hangfire `RecurringJob.AddOrUpdate` registered in `HangfireExtensions.cs` at startup. |
| 15 | Misfire handling | `RecurringJobOptions.MisfireHandling = Strict` so missed runs are picked up when the server starts back up. Idempotency comes from existing `PriceHistory` per-day uniqueness. |

---

## Backend design

### New service: `HoldingRecalculationService`

File: `src/backend/Portivio.Application/Services/HoldingRecalculationService.cs`

```csharp
public interface IHoldingRecalculationService
{
    // Daily Hangfire entry-point. Iterates every instrument, fetches
    // external prices where applicable, then recomputes every Holding row.
    Task<Result<RecalculationSummary>> RunDailyRefreshAsync(CancellationToken ct);

    // Manual entry-point. Recomputes holdings of one profile from
    // already-cached PriceHistory + accrual; no external calls.
    Task<Result<List<HoldingResponse>>> RefreshProfileAsync(
        Guid userId, Guid profileId, CancellationToken ct);
}

public sealed record RecalculationSummary(
    int InstrumentsAttempted,
    int PricesUpdated,
    int PricesSkipped,
    int HoldingsRecomputed,
    int Errors,
    List<string> ErrorMessages);
```

Responsibilities:

- For every instrument (daily job only), fetch a fresh price using its `PriceSource`:
  - `AlphaVantage` → `MarketDataService.SyncStockPriceAsync`. Apply 12-second throttle between calls. Stop after 500 calls/day (in-memory counter scoped to the run).
  - `AmfiNav` → reuse `MarketDataService.SyncAllNavsAsync` (single bulk call).
  - `AccrualFormula` (FD/RD/PPF) → no external call; per-strategy `ComputeHoldingAsync` accrues from metadata.
  - `Manual` → only Gold today. Read `MarketData:Gold:RatePerGram24K` from `MarketDataOptions`; derive 22K via `Purity22KMultiplier`; write a new `PriceHistory` row per gold instrument keyed off the purity stored in `Instrument.Metadata.purity`.
- For every holding (daily and manual paths), call `_strategies.For(instrument.Category).ComputeHoldingAsync(profileId, instrumentId, DateTime.UtcNow, ct)` to get a fresh `HoldingSnapshot`, then patch `CurrentPrice`, `MarketValue`, `UnrealizedPnL`, `RealizedPnL`, `AccruedInterest`, `Snapshot`, `LastUpdated`. Persist with one `SaveChangesAsync` per profile to avoid long open transactions.
- Wrap every per-instrument and per-holding step in a `try/catch`; log structured Serilog error with `InstrumentId` / `HoldingId` / exception message; increment summary counters; do not bubble.
- The manual path skips the external-fetch loop entirely and only runs the holding recompute step for the requested profile.

### Gold rate provider

Extend `MarketDataOptions` with a nested `GoldOptions`:

```jsonc
"MarketData": {
  "AlphaVantage": { "ApiKey": "..." },
  "Gold": {
    "RatePerGram24K": 7480.00,
    "Purity22KMultiplier": 0.9167
  }
}
```

New file: `src/backend/Portivio.Application/Services/MarketData/GoldRateProvider.cs`.

```csharp
public interface IGoldRateProvider
{
    Task<decimal?> GetRatePerGramAsync(string purity, CancellationToken ct);
}
```

Implementation reads `IOptions<MarketDataOptions>` and returns the configured rate (24K direct; 22K = 24K × multiplier; other purities → `null`). Registered as a singleton in `ApplicationServicesExtensions.AddApplicationServices`.

The daily job upserts a `PriceHistory` row per gold instrument using the purity read from `Instrument.Metadata.purity`. The manual path doesn't need this — it just reads the latest `PriceHistory` row.

### Hangfire wiring (`HangfireExtensions.cs`)

```csharp
RecurringJob.AddOrUpdate<IHoldingRecalculationService>(
    recurringJobId: "refresh-holdings-daily",
    methodCall: svc => svc.RunDailyRefreshAsync(CancellationToken.None),
    cronExpression: "0 6 * * *",
    options: new RecurringJobOptions
    {
        TimeZone        = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"),
        MisfireHandling = MisfireHandlingMode.Strict
    });
```

`MisfireHandlingMode.Strict` ensures that if the API container is down at 06:00 IST and starts back up at 09:00, Hangfire enqueues the missed run automatically. Idempotency:

- `MarketDataService.UpsertPriceAsync` already enforces "one `PriceHistory` row per (`InstrumentId`, `Date`)" — re-running on the same day is a no-op for prices.
- The holding patch is a write of computed values; safe to repeat.

### New manual-refresh endpoint

`HoldingController.cs` (existing file `src/backend/Portivio.API/Controllers/HoldingController.cs`):

```csharp
[HttpPost("refresh")]
[EnableRateLimiting("manual-refresh")]      // new policy, 1/min per user
public async Task<IActionResult> RefreshHoldings(Guid profileId, CancellationToken ct)
{
    if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
    var result = await _recalc.RefreshProfileAsync(userId, profileId, ct);
    return result.Match(
        onSuccess: () => Ok(result.Data),
        onFailure: e => StatusCode(e.StatusCode ?? 400,
            new { success = false, message = e.Message, errors = e.Errors }));
}
```

- Constructor gains `IHoldingRecalculationService _recalc`.
- New named rate-limit policy `"manual-refresh"` in `RateLimitingExtensions` — fixed window, 1 request / 60s, partitioned by user id. Route is `[Authorize]`, so user id is always present.
- `IProfileAccessGuard.EnsureOwnerAsync` is invoked inside the service to reject cross-profile access.
- Service performs the recompute synchronously and returns the same shape as `GET /api/profiles/{profileId}/holdings` (`List<HoldingResponse>`) so the front-end can replace the table directly.

### DI registration

`ApplicationServicesExtensions.AddApplicationServices`:

```csharp
services.AddScoped<IHoldingRecalculationService, HoldingRecalculationService>();
services.AddSingleton<IGoldRateProvider, GoldRateProvider>();
// MarketDataOptions is already registered today; only the new nested Gold section is added.
```

### Domain / schema impact

**No new migration required.** `Holding` already has `CurrentPrice`, `MarketValue`, `UnrealizedPnL`, `RealizedPnL`, `AccruedInterest`, `LastUpdated`, `Snapshot`. `PriceHistory` already enforces the per-(`InstrumentId`, `Date`) uniqueness. The Gold change is configuration-only.

---

## Frontend design

### `holding.service.ts`

Add a `refresh` method:

```ts
refresh(profileId: string): Observable<Holding[]> {
  return this.http.post<Holding[]>(`${this.base(profileId)}/refresh`, {});
}
```

### `holdings.component.ts`

1. Add `refreshing = false`.
2. Add:

   ```ts
   refreshPrices(): void {
     if (!this.selectedProfileId || this.refreshing) return;
     this.refreshing = true;
     this.holdingService.refresh(this.selectedProfileId)
       .pipe(takeUntil(this.destroy$), finalize(() => this.refreshing = false))
       .subscribe({
         next: (data) => {
           this.holdings = data ?? [];
           this.toastr.success('Holdings refreshed');
         },
         error: (err) => this.toastr.error(err?.error?.message || 'Refresh failed')
       });
   }
   ```

3. Add a relative-time helper:

   ```ts
   formatRelative(iso: string): string { /* '2h ago' / '3 days ago' / 'just now' */ }
   ```

### `holdings.component.html`

- Inside `.page-header`, beside the existing `Add / Update Holding` button, add:

  ```html
  <button class="btn btn-secondary"
          *ngIf="selectedProfileId && holdings.length"
          [disabled]="refreshing"
          (click)="refreshPrices()">
    <i class="fas fa-sync" [class.fa-spin]="refreshing"></i>
    {{ refreshing ? 'Refreshing…' : 'Refresh prices' }}
  </button>
  ```

- Add column header `<th class="num">Updated</th>` after the P&L column and a body cell `<td class="muted">{{ formatRelative(h.lastUpdated) }}</td>`. `h.lastUpdated` already exists on the `HoldingResponse` model.

---

## Logging & observability

- Daily job: one `LogInformation` at start ("Daily holdings refresh started"), one at end with the `RecalculationSummary` fields ("Daily holdings refresh complete. Attempted={InstrumentsAttempted} Updated={PricesUpdated} Skipped={PricesSkipped} Recomputed={HoldingsRecomputed} Errors={Errors}"). Per-instrument failures: `LogWarning` with `InstrumentId`, `Symbol`, `Source`, exception message.
- Manual endpoint: `LogInformation` with `UserId`, `ProfileId`, `HoldingsRecomputed`.
- Errors thrown from the service path remain handled by `GlobalExceptionMiddleware`.

---

## Configuration changes

`appsettings.example.json` (and `appsettings.json` / `appsettings.Development.json` where applicable):

```jsonc
"MarketData": {
  "AlphaVantage": { "ApiKey": "..." },
  "Gold": {
    "RatePerGram24K": 7480.00,
    "Purity22KMultiplier": 0.9167
  }
}
```

`RateLimitingExtensions` — add `manual-refresh` policy: fixed window, 1 request / 60s, partition by user id.

---

## Testing requirements

Match existing pattern: xUnit, in-memory `PortivioDbContext` per test (fresh `Guid` DB name), `Mock.Of<T>()` collaborators.

New file: `src/backend/Portivio.Tests/Services/HoldingRecalculationServiceTests.cs`. Cases:

1. `RefreshProfileAsync_RecomputesHoldings_UsingLatestPriceHistory` — seed an MF holding + a newer `PriceHistory` entry; assert `CurrentPrice` / `MarketValue` updated, `LastUpdated` advanced, no provider mock invoked.
2. `RefreshProfileAsync_PrunesClosedPositions` — net qty 0 case removes holding (via strategy snapshot).
3. `RefreshProfileAsync_RejectsNonOwnerProfile` — `IProfileAccessGuard` returns failure → service returns failure.
4. `RunDailyRefreshAsync_ContinuesOnPerInstrumentFailure` — mock `IStockPriceProvider` to throw on one symbol; assert other instruments still update; assert summary `Errors == 1`.
5. `RunDailyRefreshAsync_ThrottlesAlphaVantageCalls` — assert the throttling primitive (delay / `TimeProvider`) is invoked with 12 000 ms between calls.
6. `RunDailyRefreshAsync_AppliesGoldRateFromOptions` — seed gold instrument; configure `RatePerGram24K = 7480`; assert new `PriceHistory` row + holding `CurrentPrice` updated.
7. `RunDailyRefreshAsync_IsIdempotent_OnSameDay` — second run does not insert duplicate `PriceHistory` rows.

Frontend (Karma): extend `holdings.component.spec.ts` (or add it if missing) with one case asserting `refreshPrices()` swaps `holdings` with the response payload and toggles `refreshing` correctly.

---

## Files changed

### Backend (new)

- `src/backend/Portivio.Application/Services/HoldingRecalculationService.cs`
- `src/backend/Portivio.Application/Services/MarketData/GoldRateProvider.cs`
- `src/backend/Portivio.Tests/Services/HoldingRecalculationServiceTests.cs`

### Backend (modified)

- `src/backend/Portivio.API/Extensions/HangfireExtensions.cs` — register the daily recurring job with IST timezone + `MisfireHandlingMode.Strict`.
- `src/backend/Portivio.API/Extensions/ApplicationServicesExtensions.cs` — register `IHoldingRecalculationService`, `IGoldRateProvider`.
- `src/backend/Portivio.API/Extensions/RateLimitingExtensions.cs` — add `"manual-refresh"` policy (1/60s per user).
- `src/backend/Portivio.API/Controllers/HoldingController.cs` — add `POST refresh` action.
- `src/backend/Portivio.Application/Services/MarketData/MarketDataOptions.cs` — add nested `GoldOptions { RatePerGram24K, Purity22KMultiplier }`.
- `src/backend/Portivio.API/appsettings.example.json` — add `MarketData:Gold` section.
- `src/backend/Portivio.API/appsettings.json` / `appsettings.Development.json` — same.

### Frontend (modified)

- `src/frontend/src/app/core/services/holding.service.ts` — add `refresh(profileId)` method.
- `src/frontend/src/app/features/home/pages/holdings/holdings.component.ts` — add `refreshing`, `refreshPrices()`, `formatRelative()`.
- `src/frontend/src/app/features/home/pages/holdings/holdings.component.html` — add "Refresh prices" button + "Updated" column.

---

## Acceptance criteria

1. With the API running, after waiting through 06:00 IST (or by triggering the recurring job manually from the Hangfire dashboard at `/hangfire`), every Holding row has `LastUpdated` advanced to today, and `MarketValue` / `UnrealizedPnL` reflect the latest available `PriceHistory` for that instrument.
2. If the API is down at 06:00 IST and started at 09:00 IST, the recurring job fires automatically once the server is healthy (verified via Hangfire dashboard + Serilog "Daily holdings refresh started" log line).
3. The Holdings page shows a "Refresh prices" button. Clicking it returns the recomputed holdings within ~1s for a typical profile (≤20 holdings) and shows a success toast.
4. Clicking the button twice within 60s returns HTTP 429 from the rate limiter and shows the error toast.
5. The Holdings table has a new "Updated" column showing relative time (e.g. "2h ago"). The relative time updates after a successful manual refresh.
6. A failing AlphaVantage call during the daily job does not abort the job: the holding for that one symbol is left as-is, every other holding still gets refreshed, and the error count appears in the summary log.
7. For a profile with one Gold holding (24K Digital), changing `MarketData:Gold:RatePerGram24K` in config + restart + triggering the daily job updates that holding's `CurrentPrice` and `MarketValue` accordingly.
8. All existing tests still pass; the seven new backend tests + the frontend test pass; backend `dotnet build` produces zero warnings (`TreatWarningsAsErrors=true`).

---

## Open follow-ups (deferred — NOT in scope here)

- Top-of-page banner when `LastUpdated` is older than 24h.
- An external gold-rate provider (GoldAPI.io / IBJA) replacing the configured rate.
- Per-instrument refresh from the Holdings table row.
- Audit-log table for job runs (the structured Serilog summary covers the immediate need).
