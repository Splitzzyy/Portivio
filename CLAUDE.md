# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Current Project Snapshot (May 2026)

Portivio = financial portfolio manager. Two halves under `src/`: **backend** (ASP.NET Core 10 Web API, Clean Architecture) and **frontend** (Angular 18 SPA). Full local stack via `docker-compose.yml` at repo root.

Recent activity this month: Google SSO + atomic transaction + structured logging (`47e02ed`), email verification + password reset (`971c604`).

### Backend (`src/backend/`)

ASP.NET Core 10 Web API. 4 projects, strict dependency direction `API → Application → Domain` and `Infrastructure → Domain`.

| Project | Role |
|---------|------|
| `Portivio.Domain` | Entities (`User`, `Profile`, `AuthToken`, `Holding`, `Transaction`, `Instrument`, `AssetType`, `SIPPlan`, `PortfolioPerformance`, `PriceHistory`, `AuthProvider`, `AuditLog`) and enums. No framework deps. |
| `Portivio.Infrastructure` | `PortivioDbContext`, per-entity `IEntityTypeConfiguration` under `Data/Configurations/` (auto-discovered), EF migrations, SMTP email, Hangfire jobs. |
| `Portivio.Application` | Business services returning `Result<T>` (carries `IsSuccess`, `Data`, `Message`, `Errors`, `StatusCode`, plus `Match`/`OnSuccess`/`Map`/`Bind`). DTOs under `DTOs/<Feature>/`. Options classes in `AppSettingsOptions.cs`. |
| `Portivio.API` | Thin controllers mapping `Result` → HTTP via `result.Match(...)`. DI split into extension methods under `Extensions/`. |

**Key wiring** (`Program.cs` + `Extensions/`):
- DI extensions: `AddDatabase`, `AddJwtAuthentication`, `AddApplicationServices`, `AddHangfireServices`, `AddPortivioHealthChecks`, `AddPortivioRateLimiting`, `AddSwagger`, `AddCorsPolicy`, `AddForwardedHeadersConfiguration`.
- Middleware order: `GlobalExceptionMiddleware` → `ForwardedHeaders` → `StatusCodePages` → Swagger → HTTPS → CORS → AuthN → AuthZ → RateLimiter → `MapControllers` → `/health` → `/hangfire` (dev).
- Auto-migration on startup via `RunWithMigrationAsync()` — process exits if migration fails.
- `TokenCleanupService` (`IHostedService`): deletes expired `AuthToken` rows every 24h.
- Hangfire (PostgreSQL storage) runs email sends via `HangfireEmailJobService`.

**Auth**: JWT HS256, 1h access + 7d refresh, BCrypt.Net-Next passwords, SHA-256 hashed token storage in `AuthToken`. Email verification (24h token) + password reset (1h token) + Google SSO (`GoogleJsonWebSignature`). Refresh token written to `HttpOnly`/`Secure`/`SameSite=Strict` cookie via `IAuthHttpContextService`.

**Rate limiting** (4 named policies): `global` (100/min), `login` (5/min on signup/login/google-login), `per-user` (60/min, IP fallback), `fixed` (5/min).

**Conventions**:
- Always return `Result<T>` for expected failures; reserve exceptions for unexpected failures.
- Controllers map `result.StatusCode` — never hardcode status codes on failure paths.
- New entity = `DbSet<T>` on `PortivioDbContext` **and** matching configuration class in `Data/Configurations/`.
- Pin NuGet versions only in `Directory.Packages.props` (Central Package Management — no per-project `Version=`).
- `TreatWarningsAsErrors=true` is global.

### Frontend (`src/frontend/`)

Angular 18, **module-based** (not standalone components), lazy-loaded feature routing.

- `src/app/core/` — singletons imported once in `AppModule`: services (`AuthService`, `GoogleAuthService`, `HomeService`, `ProfileService`, `HoldingService`, `TransactionService`, `SIPPlanService`, `InstrumentService`, `LoadingService`), guards (`AuthGuard` class + `authGuard` functional + `NoAuthGuard`), interceptors, models.
- `src/app/features/auth/` — login, signup, forgot/reset password (route `/auth`).
- `src/app/features/home/` — protected dashboard (route `/home`, default).
- `src/environments/environment.ts` — `apiUrl` + Google OAuth `clientId`.

**`JwtInterceptor`**: attaches `Authorization: Bearer`. On 401 (non-auth URL), triggers single refresh; concurrent 401s queue on `BehaviorSubject<string|null>` and replay with the new token. Refresh failure → silent logout.

**`ErrorInterceptor`**: auth endpoint 4xx errors handled inline by components; other errors surfaced via `ngx-toastr`. Status 0 logs CORS hint once per session.

**Storage**: `AuthService` uses versioned localStorage keys (`portivio_*_v2`) — bump `STORAGE_VERSION` to invalidate stale client cache on breaking changes. Bootstrap 5 + FontAwesome loaded globally via `angular.json`.

### Infrastructure & Ops

- **Local stack** (`docker-compose.yml`): PostgreSQL 15 (health-checked) → Mailpit (SMTP 1025 / UI 8025) → backend (`:5274`) → frontend (`:4200`). `appsettings.Development.json` bind-mounted into backend container.
- **CI** (`.github/workflows/`): `backend.yml` + `frontend.yml` (restore/build/test/Docker push), `build-on-main.yml` (orchestrator + badge updates), `secret-scan.yml` (Gitleaks).
- **Images**: DockerHub `rghvgrv/portifio-api`, `rghvgrv/portifio-frontend` (tagged `latest` + commit SHA, old tags pruned to last 3).

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core 10 · .NET 10 · Clean Architecture |
| ORM | EF Core 10 · Npgsql · PostgreSQL 15 |
| Auth | JWT (HS256) · BCrypt.Net-Next · Google.Apis.Auth |
| Background Jobs | Hangfire 1.8 · Hangfire.PostgreSql |
| Email | MailKit / MimeKit (SMTP) |
| Logging | Serilog (console + context enrichment) |
| Health | `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` + custom `HangfireHealthCheck` |
| Tests | xUnit · Moq · `EntityFrameworkCore.InMemory` |
| API Docs | Swashbuckle (Swagger UI at `/swagger`, dev only) |
| Frontend | Angular 18 · Bun · Bootstrap 5 · ngx-toastr |
| Infra | Docker Compose · Mailpit (dev SMTP) · GitHub Actions |

## Build and Test

**Backend** (from `src/backend/`):

```bash
dotnet restore
dotnet build
dotnet run --project Portivio.API           # https://localhost:7241 / http://localhost:5274
dotnet test Portivio.Tests
dotnet test Portivio.Tests --filter "FullyQualifiedName~AuthServiceLoginTests"   # single class
dotnet test Portivio.Tests --filter "DisplayName~Login_WithValidCredentials"     # single test

# EF Core migrations — both flags required
dotnet ef migrations add <Name> --project Portivio.Infrastructure --startup-project Portivio.API
dotnet ef database update --project Portivio.Infrastructure --startup-project Portivio.API
```

**Frontend** (from `src/frontend/`):

```bash
bun install
bun start                                   # http://localhost:4200
bun run prod                                # production build → dist/portivio
bun run test                                # Karma unit tests
bun run lint
ng test --include='**/auth.service.spec.ts' # single spec
```

**Docker stack** (from repo root):

```bash
docker compose up --build       # postgres + mailpit + backend + frontend
docker compose logs -f backend
docker compose down -v          # stops + drops volumes (deletes DB)
```

| Service | URL |
|---------|-----|
| Frontend | http://localhost:4200 |
| API | http://localhost:5274 |
| Swagger | http://localhost:5274/swagger |
| Hangfire | http://localhost:5274/hangfire (dev only) |
| Mailpit | http://localhost:8025 |
| Health | http://localhost:5274/health |

**Required config** (copy `appsettings.example.json` → `appsettings.Development.json`):
`Postgres:ConnectionString`, `Jwt:Key` (≥32 chars), `Jwt:Issuer`, `Jwt:Audience`, `GoogleAuth:ClientId`, `Email:*`, `MarketData:AlphaVantage:ApiKey`. Missing `Postgres:ConnectionString` or `Jwt:Key` throws at startup.

**Tests**: each test creates a fresh in-memory `PortivioDbContext` (new `Guid` DB name); JWT options via `Options.Create(new AppSettingsOptions { Key = "...≥32 chars..." })`; `ILogger`/`IEmailJobService` mocked with `Mock.Of<T>()`.

## Additional Documents

- `README.md` — project overview, quick start, CI badges
- `docs/backend-architecture.md` — detailed backend architecture
- `docs/frontend-architecture.md` — detailed frontend architecture
- `MISSING.md` — known gaps and roadmap
- `AGENTS.md` — agent-specific guidance
- `src/backend/Portivio.API/appsettings.example.json` — config template
