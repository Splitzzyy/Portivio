# Portivio — Missing Features & Gaps

> Full audit of backend, frontend, infrastructure, CI/CD, and security.  
> Stack: .NET 10 · Angular 18 · PostgreSQL · Docker · Hangfire

---

## 🔴 Critical

### Backend
- [ ] **B1** `app.UseRateLimiter()` missing from middleware pipeline — rate limiting is configured but completely inactive (`Program.cs`)
- [ ] **B2** `DELETE /api/auth/cleanup-tokens` callable by any authenticated user — should be admin-only or a background job (`AuthController.cs`)

### Frontend
- [ ] **F1** Email verification page missing — `/api/auth/verify-email` endpoint exists but no frontend route/component; users who click email link land on dead URL

### Infrastructure
- [ ] **I1** No `/health` endpoint — no `AddHealthChecks()` / `MapHealthChecks()`; Docker/K8s probes will fail (`Program.cs`)
- [ ] **I2** No `appsettings.Production.json` — base config has `AllowedHosts: "*"`, Swagger always on, no prod overrides
- [ ] **I3** JWT key and DB password hardcoded in `docker-compose.yml` — should use `.env` file or Docker secrets

---

## 🟠 High

### Backend
- [ ] **B3** No DTO validation attributes — no `[Required]`, `[EmailAddress]`, `[Range]` on any DTOs; invalid input reaches service layer before rejection
- [ ] **B4** No password strength enforcement — signup and reset-password accept any password; no min length or complexity rules (`AuthService.cs`)
- [ ] **B5** Google SSO throws raw exception instead of returning `Result` — breaks Result pattern consistency (`AuthService.cs` ~line 418)
- [ ] **B6** `AssetTypeController` missing UPDATE — has GET/POST/DELETE but no PUT/PATCH; asset types cannot be edited after creation

### Frontend
- [ ] **F2** Profile and Settings pages missing — dropdown nav items call `goToProfile()` / `goToSettings()` but routes and components don't exist (`home.component.ts`)
- [ ] **F3** No Angular services for 15+ backend endpoints — `MarketDataController`, `PriceHistoryController`, `PortfolioPerformanceController` have no corresponding frontend services

### Infrastructure
- [ ] **I4** No security headers — missing `Strict-Transport-Security`, `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy` (`Program.cs`)
- [ ] **I5** Swagger exposed in all environments — `UseSwagger()` / `UseSwaggerUI()` run unconditionally; should be `IsDevelopment()` only (`Program.cs`)
- [ ] **I6** Docker containers run as root — no `USER` directive in either `Dockerfile`
- [ ] **I7** No gzip / caching in nginx — missing `gzip on`, `Cache-Control` headers for static assets (`nginx.conf`)
- [ ] **I8** No `restart: unless-stopped` on Docker services (`docker-compose.yml`)

---

## 🟡 Medium

### Backend
- [ ] **B7** Token expiry values hardcoded — email verify: 24h, password reset: 1h, access token: 1h — should be in `AppSettingsOptions` / appsettings
- [ ] **B8** MarketData services have zero test coverage — `MarketDataService`, `StandardRateService`, `AlphaVantageStockProvider`, `AmfiNavProvider` all untested
- [ ] **B9** CLAUDE.md outdated — states BCrypt not implemented; BCrypt IS fully implemented; misleads contributors

### Frontend
- [ ] **F4** Forgot-password uses weak email validator — uses Angular's `Validators.email` instead of custom `emailFormatValidator()` used in login/signup
- [ ] **F5** Unused `authGuard` functional export — defined but never used in routing; dead code (`auth.guard.ts`)
- [ ] **F6** `console.log` calls not stripped in production builds
- [ ] **F7** Commented-out navigation code — `// this.router.navigate(['/profile'])` etc. should be cleaned up (`home.component.ts`)

### Infrastructure
- [ ] **I9** Serilog has no sinks configured — used for JWT logging but no file/cloud sink; logs only go to console (`Program.cs`)
- [ ] **I10** No SAST / security scanning in CI — no CodeQL, Snyk, or Dependabot (`.github/workflows/`)
- [ ] **I11** No linting in CI pipelines — no ESLint (frontend) or StyleCop (backend) step
- [ ] **I12** No code coverage reporting in CI — tests run but coverage not collected or threshold-gated
- [ ] **I13** CORS allows any header and method — `AllowAnyHeader()` + `AllowAnyMethod()` too permissive; should whitelist specific headers/methods (`Program.cs`)
- [ ] **I14** README incomplete — missing: local setup guide, appsettings/env config, migration steps, architecture overview
- [ ] **I15** No `.env.example` file — no documented template for environment variables

---

## 🔵 Cross-Cutting

- [ ] **X1** `AllowedHosts: "*"` in base `appsettings.json` — restrict to specific domains in production
- [ ] **X2** No health checks for external services — Hangfire, SMTP, AlphaVantage not checked on startup
- [ ] **X3** Rate limiting is in-memory only — multiple backend instances would have independent counters (no distributed rate limiting)
- [ ] **X4** No correlation IDs — impossible to trace a single request across log lines
- [ ] **X5** No pagination on list endpoints — holdings, transactions, instruments return full lists with no skip/take

---

## ⚡ Quick Wins (< 30 min each)

- [ ] Add `app.UseRateLimiter()` after `app.UseAuthorization()` in `Program.cs` — 1 line, activates all rate limiting
- [ ] Gate `UseSwagger()` / `UseSwaggerUI()` behind `if (env.IsDevelopment())` in `Program.cs`
- [ ] Replace `Validators.email` with `emailFormatValidator()` in `forgot-password.component.ts`
- [ ] Remove unused functional `authGuard` export from `auth.guard.ts`
- [ ] Update CLAUDE.md line 58 — mark BCrypt as implemented
- [ ] Add `restart: unless-stopped` to all services in `docker-compose.yml`
- [ ] Add PUT endpoint for AssetType in `AssetTypeController.cs`
