# Backend Architecture

ASP.NET Core Web API on .NET 10 + EF Core + PostgreSQL. Clean Architecture, 4 projects.

## Solution Layout (`src/backend/`)

```
Portivio.slnx
├── Portivio.Domain          # Entities + Enums. No deps.
├── Portivio.Application     # Services + DTOs + Result<T>. → Domain + Infrastructure
├── Portivio.Infrastructure  # EF Core + Migrations + Hangfire + SMTP. → Domain
├── Portivio.API             # Controllers + Program.cs. → Application + Infrastructure
└── Portivio.Tests           # xUnit + Moq + EF InMemory
```

Dependency rule: `API → Application → Infrastructure → Domain`. Domain stays pure.

---

## Projects

### Portivio.Domain

Plain C# entities, no framework refs.

**Entities:** `User`, `Profile`, `AuthToken`, `AuthProvider`, `Transaction`, `Holding`, `Instrument`, `AssetType`, `SIPPlan`, `PortfolioPerformance`, `PriceHistory`, `AuditLog`

Notable fields on `User`:
- `EmailVerificationToken` / `EmailVerificationTokenExpiry` — 24h single-use token for email verification
- `PasswordResetToken` / `PasswordResetTokenExpiry` — 1h single-use token for password reset
- `IsVerified` — must be `true` before login is allowed
- `PasswordHash` — BCrypt hash (nullable for SSO-only users)

---

### Portivio.Application

Business logic. Services return `Result<T>` (never throw for expected failures).

**Services** (`Services/`):

| Service | Responsibility |
|---------|---------------|
| `AuthService` | Signup, login, verify email, forgot/reset password, Google SSO, refresh token, logout |
| `ProfileService` | CRUD for user portfolio profiles |
| `HoldingService` | Holdings per profile |
| `TransactionService` | Buy/sell transaction records |
| `InstrumentService` | Financial instruments + asset types |
| `SIPPlanService` | SIP plan management + activate/deactivate |
| `PortfolioPerformanceService` | XIRR + performance snapshot |
| `PriceHistoryService` | Historical price records |
| `HomeService` | Dashboard data aggregation |
| `MarketDataService` | Live prices from AlphaVantage (stocks) + AMFI (mutual fund NAV) |
| `XirrCalculator` | Internal XIRR math utility |

**DTOs** (`DTOs/<Feature>/`): request/response shapes per feature area.

**Result Pattern** (`Results/`):
- `Result` / `Result<T>` — carry `IsSuccess`, `Message`, `Errors[]`, `StatusCode` (200/201/400/401/403/404/409/500/501)
- Factory methods: `Success`, `BadRequest`, `Unauthorized`, `Forbidden`, `NotFound`, `Conflict`, `InternalServerError`
- Functional helpers: `Match`, `MatchAsync`, `OnSuccess`, `OnFailure`, `Map`, `Bind`, `Ensure`

---

### Portivio.Infrastructure

Persistence, background jobs, and email.

**EF Core** (`Data/`):
- `PortivioDbContext` — single context, one `DbSet<T>` per entity
- `Data/Configurations/` — per-entity `IEntityTypeConfiguration<T>`, auto-discovered via `ApplyConfigurationsFromAssembly`. No inline `modelBuilder.Entity<>()` in `OnModelCreating`
- `Migrations/` — generated, run at startup via `db.Database.Migrate()`

**Email Services** (`Services/`):

| Class / Interface | Role |
|-------------------|------|
| `IEmailService` | Interface: send verification / welcome / reset emails |
| `IEmailJobService` | Interface: enqueue email jobs via Hangfire |
| `SmtpEmailService` | MailKit SMTP implementation of `IEmailService` |
| `HangfireEmailJobService` | Enqueues `IEmailService` calls as Hangfire background jobs |
| `EmailTemplates` | Internal static HTML template builder |
| `EmailOptions` | Config POCO bound to `appsettings Email:` section |

**Adding a new entity:** create entity in Domain → add `DbSet<T>` on `PortivioDbContext` → add `IEntityTypeConfiguration<T>` in `Configurations/` → run migration.

---

### Portivio.API

Thin HTTP layer. All business logic stays in Application.

- `Program.cs` — DI registration, JWT bearer, CORS, Swagger, Hangfire, rate limiter, startup migration
- `Controllers/` — map HTTP ↔ `Result<T>` via `result.Match(onSuccess, onFailure)`. Status code comes from `Result.StatusCode`, never hardcoded for failure paths
- `Services/AuthHttpContextService` — injects `IpAddress` and `DeviceInfo` into auth requests; handles mobile vs browser refresh token delivery
- `Filters/TransactionFilter` — wraps EF writes in DB transaction per request

Auth tokens: refresh token set as `HttpOnly`, `Secure`, `SameSite=Strict` cookie for browsers. Mobile clients receive it in the response body.

---

## Request Flow

```
HTTP Request
  → Middleware (Auth, RateLimit, CORS, ForwardedHeaders)
  → Controller (Portivio.API)
      → IService (Portivio.Application)
          → PortivioDbContext (Portivio.Infrastructure) ↔ PostgreSQL
          → IEmailJobService → Hangfire queue → SMTP (background)
      ← Result<T>
  ← Controller: result.Match(onSuccess, onFailure) → HTTP Response
```

---

## Auth Flows

### Email/Password Signup
```
POST /signup
  → create User (IsVerified=false)
  → generate EmailVerificationToken (24h)
  → save User
  → EnqueueVerificationEmail + EnqueueWelcomeEmail (Hangfire)
  ← 201 "Please verify your email"

POST /verify-email { email, token }
  → validate token matches + not expired
  → IsVerified = true, token cleared
  ← 200

POST /forgot-password { email }
  → generate PasswordResetToken (1h)
  → EnqueuePasswordResetEmail (Hangfire)
  ← 200 (always, no email enumeration)

POST /reset-password { email, token, newPassword }
  → validate token matches + not expired
  → BCrypt hash new password, token cleared
  ← 200
```

### Google SSO
```
POST /google-login { googleJwt }
  → validate with Google.Apis.Auth
  → find or create User (IsVerified=true for new SSO users)
  → new user → EnqueueWelcomeEmail (Hangfire)
  → issue JWT + refresh token
  ← 200
```

### Token Lifecycle
- Access token: JWT, 1h, signed HMAC-SHA256
- Refresh token: 64 random bytes → Base64, 7 days, stored as SHA-256 hash in `AuthTokens` table
- On refresh: old token revoked, new pair issued
- On logout: all non-revoked tokens for user set `Revoked=true`

---

## Background Jobs (Hangfire)

Hangfire uses PostgreSQL as backing store. Jobs are persistent — survive restarts.

**Hangfire schema:** created automatically on first run in the same PostgreSQL database.

**Worker:** `AddHangfireServer()` registers a hosted service that processes the queue.

**Email jobs enqueued by `AuthService`:**

| Trigger | Jobs Enqueued |
|---------|--------------|
| Signup | `SendEmailVerificationAsync` + `SendWelcomeEmailAsync` |
| Resend verification | `SendEmailVerificationAsync` |
| Forgot password | `SendPasswordResetAsync` |
| Google SSO (new user) | `SendWelcomeEmailAsync` |

**Dashboard:** `/hangfire` (Development environment only, via `MapHangfireDashboard`).

---

## Rate Limiting

Configured in `Program.cs`. **`app.UseRateLimiter()` must be in the middleware pipeline** for policies to activate.

| Policy | Limit | Applied to |
|--------|-------|-----------|
| `global` | 100 req/min | All controllers (class-level) |
| `login` | 5 req/min | `/signup`, `/google-login` |
| `per-user` | 60 req/min | JWT-identified user or IP fallback |

---

## Configuration

`appsettings.json` (base, empty secrets) + `appsettings.Development.json` (gitignored, local vals). Template: `appsettings.example.json`.

**Required sections — missing values throw at startup:**

| Section | Key | Notes |
|---------|-----|-------|
| `Postgres` | `ConnectionString` | Npgsql format |
| `Jwt` | `Key` (≥32 chars) | HMAC-SHA256 signing key |
| `Jwt` | `Issuer`, `Audience` | Validation active when non-empty |
| `GoogleAuth` | `ClientId` | Required for SSO; startup throws if missing when SSO called |
| `Email` | `Host`, `Port`, `FromAddress` | SMTP settings for Hangfire email jobs |
| `Email` | `FrontendBaseUrl` | Base URL for verification/reset links in emails |

**Docker:** env vars override appsettings using `__` as nested separator (`Email__Host`, `Jwt__Key`, etc.).

---

## Build & Test

Central Package Management in `Directory.Packages.props` — no per-project `Version=`. Global `TreatWarningsAsErrors=true` via `Directory.Build.props`.

```bash
dotnet restore
dotnet build                    # Portivio.slnx
dotnet run --project Portivio.API          # http :5274 / https :7241
dotnet test Portivio.Tests
dotnet test Portivio.Tests --filter "FullyQualifiedName~AuthServiceLoginTests"

# Migrations
dotnet ef migrations add <Name> \
  --project Portivio.Infrastructure \
  --startup-project Portivio.API \
  --output-dir Migrations

dotnet ef database update \
  --project Portivio.Infrastructure \
  --startup-project Portivio.API
```

---

## Conventions

- **Expected failures** → `Result<T>`. **Unexpected** → throw (caught in controller `try/catch`).
- **Status code** owned by service via `Result.StatusCode`, not hardcoded in controller.
- **EF configs** live in `Infrastructure/Data/Configurations/`, one file per entity. `DbContext` picks them up automatically.
- **Password hashing** uses BCrypt (`BCrypt.Net-Next`). Nullable `PasswordHash` on `User` supports SSO-only accounts.
- **Token storage**: verification/reset tokens stored as raw strings (32-byte crypto-random, URL-safe base64, short-lived). Refresh tokens stored as SHA-256 hashes.

---

## Tests (`Portivio.Tests`)

xUnit + Moq + `Microsoft.EntityFrameworkCore.InMemory`. New in-memory `PortivioDbContext` per test. `IEmailJobService` mocked with `Mock.Of<IEmailJobService>()`. No external deps, no network, no real DB.

Pattern for new service tests:
```csharp
var context = new PortivioDbContext(new DbContextOptionsBuilder<PortivioDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
var service = new MyService(context, Mock.Of<ILogger<MyService>>(), ...);
```
