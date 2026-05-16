# Portivio — Missing Features & Gaps

> Full audit of backend, frontend, infrastructure, CI/CD, and security.
> Stack: .NET 10 · Angular 18 · PostgreSQL · Docker · Hangfire
> Last audited: 2026-05-07

---

## ✅ Recently Implemented (since last audit)

- **X7** **Centralized Audit Logging** — `IAuditService` + `AuditService` live; all security events (login, signup, reset, verify) recorded with IP/UA metadata.
- **I5** **Swagger Gated** — `UsePortivioSwagger()` now conditionally mapped only in `Development` environment.
- **I6** **Non-root Docker Containers** — Backend `Dockerfile` updated to use `USER $APP_UID`.
- **I4** **Security Headers** — `SecurityHeadersMiddleware` implemented with HSTS, CSP, X-Frame-Options, and nosniff.
- **X4** **Correlation IDs** — `AuditService` captures `X-Correlation-ID` or `TraceIdentifier`.
- **B11** **Global Exception Middleware** — Enhanced to handle `UnauthorizedAccessException`, `ArgumentException`, etc., with environment-aware detail levels.
- **B1** `app.UseRateLimiter()` — wired in `Program.cs` after `UseAuthorization()`
- **B2** `DELETE /api/auth/cleanup-tokens` removed; replaced by `TokenCleanupService` (`IHostedService`, 24h cycle)
- **B9** CLAUDE.md rewritten — BCrypt + auth flows now documented accurately
- **I1** `/health` endpoint — `MapHealthChecks("/health").AllowAnonymous()` mapped; `AddDbContextCheck<PortivioDbContext>()` + custom `HangfireHealthCheck` registered
- **X2** Health checks for external services — Hangfire health check live (DB + Hangfire covered)
- **B5 (partial)** Google SSO returns `Result<AuthResponse>` for token validation failures (`InvalidJwtException` → `Unauthorized`); only remaining throw is `InvalidOperationException` for missing `GoogleAuth:ClientId` (startup-config error)
- **B3** DTO validation attributes — `[Required]`/`[EmailAddress]`/`[StringLength]`/`[Compare]` added across all Auth DTOs + `CreateAssetTypeRequest`/`UpdateAssetTypeRequest`; ASP.NET `[ApiController]` triggers automatic 400 `ValidationProblemDetails` on invalid ModelState
- **B4** Password strength enforcement — new `StrongPasswordAttribute` (`Portivio.Application/Validation/`) requires ≥8 chars + upper + lower + digit; applied to `SignupRequest.Password` and `ResetPasswordRequest.NewPassword`
- **B5b** `GoogleLoginAsync` `InvalidOperationException` for missing `GoogleAuth:ClientId` → `Result<AuthResponse>.InternalServerError(...)` — full Result-pattern consistency
- **B6** `PUT /api/asset-types/{id}` — `IInstrumentService.UpdateAssetTypeAsync` + controller route added
- **X1 (partial)** `AllowedHosts` changed from `"*"` → `""` in base `appsettings.json`; still no production override
- **I10 (partial)** Dependabot configured (`.github/dependabot.yml`) for nuget/npm/github-actions; no SAST (CodeQL/Snyk) yet

---

## 🔴 Critical

### Frontend
- [ ] **F1** Email verification page missing — `/api/auth/verify-email` exists but no Angular route/component; clicking email link → dead URL

### Infrastructure
- [ ] **I2** No `appsettings.Production.json` — Swagger always on, CORS origins empty in prod, no prod overrides
- [ ] **I3** JWT key + DB password hardcoded in `docker-compose.yml` — move to `.env` / Docker secrets


---

## 🟠 High

### Backend
- [ ] **B4b** No password breach check — strength rules now enforced (length + upper/lower/digit); HIBP/k-anonymity check still TODO

### Frontend
- [ ] **F2** Settings page + nav wiring — `goToProfile()` and `goToSettings()` in `home.component.ts` still have commented-out `router.navigate(...)` calls; `ProfilesComponent` route exists at `/home/profiles` but not linked; no `SettingsComponent` at all
- [ ] **F3** Angular services missing for `MarketDataController`, `PriceHistoryController`, `PortfolioPerformanceController` — no UI yet calls these endpoints

### Infrastructure
- [ ] **I7** No gzip / caching in nginx — missing `gzip on`, `Cache-Control` for static assets in `nginx.conf`
- [ ] **I8** No `restart: unless-stopped` on services in `docker-compose.yml`


---

## 🟡 Medium

### Backend
- [ ] **B7** Token expiry hardcoded — email verify 24h, password reset 1h, access 1h, refresh 7d — push into `AppSettingsOptions`
- [ ] **B8** MarketData services have zero test coverage — `MarketDataService`, `StandardRateService`, `AlphaVantageStockProvider`, `AmfiNavProvider` untested
- [ ] **B10** `TokenCleanupService` untested — new `IHostedService` has no test (lifecycle + cleanup query)
- [ ] **B11** `GlobalExceptionMiddleware` untested — exception → response mapping has no coverage
- [ ] **B12** `TransactionFilter` untested — `IAsyncActionFilter` wraps every controller action in a DB transaction; no rollback/commit test
- [ ] **B13** No HTTP resilience (Polly) on `AlphaVantageStockProvider` / `AmfiNavProvider` — single-shot HttpClient call; one network blip → request fails. Add retry + circuit breaker

### Frontend
- [ ] **F4** `forgot-password.component.ts` still uses `Validators.email` instead of `emailFormatValidator()` (line 34)
- [ ] **F5** Unused `authGuard` functional export in `auth.guard.ts` line 23 — defined but never referenced; dead code
- [ ] **F6** `console.log` calls not stripped in production builds
- [ ] **F7** Commented-out `// this.router.navigate(['/profile'])` etc. in `home.component.ts` lines 72, 77

### Infrastructure
- [ ] **I9** Serilog has only Console sink — `appsettings.json` `WriteTo: [{ Name: Console }]` only; no file/Seq/cloud sink for production log retention
- [ ] **I10b** No SAST scanning — Dependabot live, but no CodeQL or Snyk; add CodeQL workflow for C# + JavaScript
- [ ] **I11** No linting in CI — no ESLint (frontend) or `dotnet format --verify-no-changes` (backend)
- [ ] **I12** No code coverage reporting — tests run, results not collected or threshold-gated
- [ ] **I13** CORS allows any header + method — `.AllowAnyHeader().AllowAnyMethod()` in `InfrastructureExtensions.cs` line 34-35; whitelist actual headers/methods
- [ ] **I14** README incomplete — missing local setup, appsettings/env config, migration steps, architecture summary
- [ ] **I15** No `.env.example` — no documented template for environment variables
- [ ] **I16** Hangfire dashboard `/hangfire` mounted dev-only by `MapHangfireDashboardIfDevelopment` — fine; but no auth filter declared, so if it ever flips on in prod it's wide open. Add `IDashboardAuthorizationFilter` defensively

---

## 🔵 Cross-Cutting

- [ ] **X3** Rate limiting in-memory only — multi-instance deployment = independent counters per pod. Use Redis-backed limiter for distributed enforcement
- [ ] **X4** No correlation IDs — only `Enrich.FromLogContext` is set; no middleware injects `X-Correlation-ID` into response or `LogContext`. Single request not traceable across log lines
- [ ] **X5** No pagination on list endpoints — `HoldingController`, `TransactionController`, `InstrumentController` return full lists with no `skip`/`take`/cursor
- [ ] **X6** No structured request/response logging — only ad-hoc `_logger.LogInformation` inside services; no per-request log scope with method/path/status/duration
- [ ] **X7** No audit log writes — `AuditLog` entity exists in domain, but no service writes to it; security-relevant events (login, password reset, token revoke) not recorded

---

## ⚡ Quick Wins (< 30 min each)

- [ ] Replace `Validators.email` → `emailFormatValidator()` in `forgot-password.component.ts:34`
- [ ] Delete unused functional `authGuard` export from `auth.guard.ts:23`
- [ ] Add `restart: unless-stopped` to all services in `docker-compose.yml`
- [ ] Uncomment + wire `goToProfile()` → `/home/profiles` in `home.component.ts:72`
- [ ] Add `gzip on; gzip_types text/css application/javascript application/json;` to `nginx.conf`
