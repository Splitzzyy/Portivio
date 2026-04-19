# Backend Architecture

ASP.NET Core Web API on .NET 10 + EF Core + PostgreSQL. Clean Architecture, 4 projects.

## Solution Layout (`src/backend/`)

```
Portivio.slnx
├── Portivio.Domain          # Entities + Enums. No deps.
├── Portivio.Application     # Services + DTOs + Result<T>. → Domain
├── Portivio.Infrastructure  # EF Core DbContext + Migrations. → Domain
├── Portivio.API             # Controllers + Program.cs. → Application, Infrastructure
└── Portivio.Tests           # xUnit + Moq + EF InMemory
```

Dependency rule: `API → Application → Domain` and `Infrastructure → Domain`. Domain stays pure.

## Projects

### Portivio.Domain

Plain C# entities, no framework refs.

Entities: `User`, `Profile`, `AuthToken`, `AuthProvider`, `Transaction`, `Holding`, `Instrument`, `AssetType`, `SIPPlan`, `PortfolioPerformance`, `PriceHistory`, `AuditLog`.

Enums: role types, transaction types, asset categories.

### Portivio.Application

Business logic layer. Services return `Result<T>` (never throw for expected failures).

**Services** (`Services/`):
- `AuthService` — signup, login, refresh, logout. JWT issuance.
- `ProfileService`, `HoldingService`, `TransactionService`, `InstrumentService`
- `SIPPlanService`, `PortfolioPerformanceService`, `PriceHistoryService`
- `HomeService` — dashboard aggregation
- `XirrCalculator` — XIRR math util

**DTOs** (`DTOs/<Feature>/`): request/response shapes, one folder per feature.

**Results** (`Results/`):
- `Result` / `Result<T>` — carry `IsSuccess`, `Message`, `Errors`, `StatusCode` (200/201/400/401/403/404/409/500/501).
- Functional helpers: `Match`, `OnSuccess`, `OnFailure`, `Map`, `Bind`.

### Portivio.Infrastructure

EF Core persistence.

- `Data/PortivioDbContext.cs` — single `DbContext`, one `DbSet` per entity.
- `Data/Configurations/` — per-entity `IEntityTypeConfiguration<T>`. Auto-picked via `ApplyConfigurationsFromAssembly`. No inline `modelBuilder.Entity<>()` calls.
- `Migrations/` — generated, run via CLI (`dotnet ef migrations add/database update`).

Add new entity: create entity in Domain → add `DbSet` + configuration class here.

### Portivio.API

Thin HTTP layer.

- `Program.cs` — DI wiring, JWT bearer setup, CORS, Swagger.
- `Controllers/` — one per feature, mirror service methods. Failure mapping via `result.Match(onSuccess, onFailure)`, status comes from `Result.StatusCode`. Never hardcode failure codes.
- `Services/` — API-only helpers (e.g. cookie writer for refresh token).
- `OpenApi/` — Swagger configuration.

Auth refresh token set as `HttpOnly`, `Secure`, `SameSite=Strict` cookie.

## Request Flow

```
HTTP req
  → Controller (Portivio.API)
  → Service (Portivio.Application) → Result<T>
  → DbContext (Portivio.Infrastructure) ↔ PostgreSQL
  ← Result<T>
  ← Controller maps via Match → HTTP res
```

## Configuration

`appsettings.json` + `appsettings.Development.json` (gitignored vals), template at `appsettings.example.json`.

Required sections:
- `Postgres:ConnectionString` — Npgsql. Missing → startup throw.
- `Jwt:Key` (≥32 chars), `Jwt:Issuer`, `Jwt:Audience` — Issuer/Audience validation active only when set.

## Build + Test

Central Package Management in `Directory.Packages.props` — no per-project `Version=`. Global `TreatWarningsAsErrors=true` via `Directory.Build.props`.

```bash
dotnet restore
dotnet build                                      # Portivio.slnx
dotnet run --project Portivio.API                 # :5274 / :7241
dotnet test Portivio.Tests

# Migrations
dotnet ef migrations add <Name> --project Portivio.Infrastructure --startup-project Portivio.API
dotnet ef database update       --project Portivio.Infrastructure --startup-project Portivio.API
```

## Conventions

- Expected failures → `Result<T>`. Unexpected → throw, caught in controller `try/catch`.
- Status code owned by service, not controller (for failure paths).
- EF configs live in `Infrastructure/Data/Configurations/`, one file per entity.
- Password hashing in `AuthService` is placeholder (not bcrypt/Argon2) — known gap.

## Tests (`Portivio.Tests`)

xUnit + Moq + `Microsoft.EntityFrameworkCore.InMemory`. New in-memory `PortivioDbContext` per test, JWT `IConfiguration` mocked. No external deps.
