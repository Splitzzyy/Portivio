---
name: portivio
description: Use when working in the Portivio repo — financial portfolio manager with .NET 10 backend (Clean Architecture, EF Core + Postgres) and Angular 18 frontend (Bun). Loads conventions for Result pattern, Central Package Management, EF configuration placement, JWT auth, and known version pins that have broken CI when bumped.
---

# Portivio Project Skill

Financial portfolio manager. Two independent projects under `src/`:

- `src/backend/` — ASP.NET Core Web API, .NET 10, EF Core + PostgreSQL
- `src/frontend/` — Angular 18 SPA, Bun runtime/package manager

## Backend

### Commands (run from `src/backend/`)

```bash
dotnet restore
dotnet build                                                          # builds Portivio.slnx
dotnet run --project Portivio.API                                     # https://localhost:7241, http://localhost:5274
dotnet test Portivio.Tests
dotnet test Portivio.Tests --filter "FullyQualifiedName~AuthServiceLoginTests"
dotnet test Portivio.Tests --filter "DisplayName~Login_WithValidCredentials"

# EF migrations — always specify startup + project
dotnet ef migrations add <Name> --project Portivio.Infrastructure --startup-project Portivio.API
dotnet ef database update --project Portivio.Infrastructure --startup-project Portivio.API
```

Swagger at `/swagger` in Development.

### Config (see `Portivio.API/appsettings.example.json`)

- `Postgres:ConnectionString` — required. Missing → throws at startup.
- `Jwt:Key` — required, ≥32 chars. `Jwt:Issuer`, `Jwt:Audience` — optional, validation only kicks in when set.

Do not commit real secrets to `appsettings.Development.json`.

### Architecture (Clean)

Strict deps: `API → Application → Domain`, `Infrastructure → Domain`.

- **Portivio.Domain** — Pure entities (`User`, `Profile`, `AuthToken`, `Transaction`, `Holding`, `Instrument`, `AssetType`, `SIPPlan`, `PortfolioPerformance`, `PriceHistory`, `AuthProvider`, `AuditLog`) + enums. No framework deps.
- **Portivio.Infrastructure** — `PortivioDbContext`, per-entity `IEntityTypeConfiguration` classes under `Data/Configurations/` (auto-discovered via `ApplyConfigurationsFromAssembly`), migrations. New entity = add `DbSet` on context + matching configuration class. No `modelBuilder.Entity<>()` calls in `OnModelCreating`.
- **Portivio.Application** — Business logic. Services return `Result<T>` / `Result` (see `Results/`) instead of throwing for expected failures. Result carries `IsSuccess`, `Message`, `Errors`, HTTP `StatusCode`, plus `Match`, `OnSuccess`, `OnFailure`, `Map`, `Bind`. DTOs under `DTOs/<Feature>/`.
- **Portivio.API** — Thin controllers. Call service → `result.Match(onSuccess, onFailure)` → HTTP response. Controllers map `StatusCode` from failed Result — do not hardcode status codes for failure paths. Auth endpoints set refresh token as `HttpOnly`, `Secure`, `SameSite=Strict` cookie.

### Rules that bite

- `TreatWarningsAsErrors=true` globally via `Directory.Build.props`. Any warning = build fail.
- Central Package Management: versions pinned in `Directory.Packages.props`. Do not add `Version=` to individual `PackageReference` entries. Bump versions there, not per-project.
- `AuthService.VerifyPassword` is placeholder — does NOT use bcrypt/Argon2. Known gap. If touching auth, do not build on current hashing.
- Target framework = `net10.0`. Older SDKs won't build.

### Tests (`Portivio.Tests`)

xUnit + Moq + `EntityFrameworkCore.InMemory`. Pattern: fresh in-memory `PortivioDbContext` per test, mock `IConfiguration` for JWT settings, instantiate service directly. No external deps.

## Frontend

### Commands (run from `src/frontend/`)

```bash
bun install                     # or npm install
bun start                       # ng serve → http://localhost:4200
bun run prod                    # production build → dist/portivio
bun run test                    # Karma/ng test
bun run lint
ng test --include='**/auth.service.spec.ts'   # single spec
```

### Architecture

Classic Angular 18 **module-based** (NOT standalone components). Lazy-loaded features.

- `src/app/core/` — singletons: `AuthService`, `AuthGuard`, `JwtInterceptor` (attaches token, handles 401 → refresh), shared models.
- `src/app/shared/` — cross-feature components.
- `src/app/features/auth/` — login, signup, forgot/reset password. Lazy at `/auth`.
- `src/app/features/home/` — protected layout + dashboard. Lazy at `/home` (default).
- `src/environments/` — `environment.ts` (dev), `environment.prod.ts` (prod). Holds `apiUrl` + OAuth client IDs.

Auth flow: `AuthService` BehaviorSubject → `JwtInterceptor` attaches access token → `AuthGuard` protects feature routes → 401 → refresh via HttpOnly cookie.

Bootstrap 5 + FontAwesome loaded globally via `angular.json` `styles`/`scripts` — no per-component import.

### Known CI landmines

- **Do not let Dependabot jump Angular majors.** PR #11 bumped 18 → 21 without running `ng update`. Angular 21 makes components standalone by default → every NgModule errors NG6008, `@angular/common/http` subpath layout changes. Pin major in Dependabot config or review group PRs before merge.
- **`Microsoft.OpenApi` must stay 2.x** (currently 2.7.2). `Microsoft.AspNetCore.OpenApi` 10.0.6 source generator writes to `IOpenApiMediaType.Example` which became read-only in 3.x → CS0200. Swashbuckle.AspNetCore 10.1.7 floor = 2.4.1.
- **`bun.lock` must match `package.json`** — CI runs `bun install --frozen-lockfile`. Regen locally after dep change + commit lock.

## Cross-cutting

When adding an API endpoint the frontend will call: update both the service in `Portivio.Application/Services/` (returning `Result<T>`) and the Angular service in `src/app/core/services/`. Keep DTO shapes in sync with `Portivio.Application/DTOs/`.
