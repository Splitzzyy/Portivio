# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Layout

Portivio is a financial portfolio management application with two independent projects under `src/`:

- `src/backend/` — ASP.NET Core Web API (.NET 10) using Entity Framework Core + PostgreSQL
- `src/frontend/` — Angular 18 SPA, using Bun as the runtime/package manager

Each side has its own build, test, and dependency management. They communicate over HTTP; the frontend expects the API at a URL configured in `src/frontend/src/environments/environment.ts`.

## Backend (`src/backend`)

### Common commands

All commands run from `src/backend/`.

```bash
dotnet restore                              # restore packages (Central Package Management)
dotnet build                                # build whole solution (Portivio.slnx)
dotnet run --project Portivio.API           # run API (https://localhost:7241, http://localhost:5274)
dotnet test Portivio.Tests                  # run the full xUnit suite
dotnet test Portivio.Tests --filter "FullyQualifiedName~AuthServiceLoginTests"   # run a single class
dotnet test Portivio.Tests --filter "DisplayName~Login_WithValidCredentials"     # run a single test

# EF Core migrations — must specify startup + project
dotnet ef migrations add <Name> --project Portivio.Infrastructure --startup-project Portivio.API
dotnet ef database update --project Portivio.Infrastructure --startup-project Portivio.API
```

Swagger UI is served at `/swagger` in Development. The `https` launch profile opens it automatically.

### Configuration

`Portivio.API` requires two config sections (see `appsettings.example.json`):

- `Postgres:ConnectionString` — Npgsql connection string. Missing value throws at startup.
- `Jwt:Key` (required, ≥32 chars), `Jwt:Issuer`, `Jwt:Audience` — JWT bearer validation. Issuer/Audience validation is only enabled when those values are non-empty.

`appsettings.Development.json` holds local values and is gitignored-adjacent to `appsettings.example.json`. Do not commit real secrets here.

### Solution architecture (Clean Architecture)

The solution is split into four projects with a strict dependency direction `API → Application → Domain` and `Infrastructure → Domain`. `TreatWarningsAsErrors=true` is set globally via `Directory.Build.props`, and package versions are pinned centrally in `Directory.Packages.props` (Central Package Management — do not add `Version=` to individual `PackageReference` entries).

- **Portivio.Domain** — Pure entity classes (`User`, `Profile`, `AuthToken`, `Transaction`, `Holding`, `Instrument`, `AssetType`, `SIPPlan`, `PortfolioPerformance`, `PriceHistory`, `AuthProvider`, `AuditLog`) and enums. No framework dependencies.
- **Portivio.Infrastructure** — EF Core `PortivioDbContext`, per-entity `IEntityTypeConfiguration` classes under `Data/Configurations/` (auto-discovered via `ApplyConfigurationsFromAssembly`), and generated migrations. When adding a new entity, add both a `DbSet` on the context and a matching configuration class.
- **Portivio.Application** — Business logic. Services return a `Result<T>` / `Result` type (see `Results/`) instead of throwing for expected failures. The Result pattern carries `IsSuccess`, `Message`, `Errors`, and an HTTP `StatusCode` (200/201/400/401/403/404/409/500/501), plus functional helpers (`Match`, `OnSuccess`, `OnFailure`, `Map`, `Bind`). DTOs live under `DTOs/<Feature>/`.
- **Portivio.API** — Thin controllers that call services and translate results into HTTP responses via `result.Match(onSuccess, onFailure)`. Auth endpoints set the refresh token as an `HttpOnly`, `Secure`, `SameSite=Strict` cookie.

### Conventions that matter

- **Always return `Result<T>`** from Application services for expected error paths; reserve exceptions for unexpected failures (caught in the controller `try/catch`).
- **Controllers map `StatusCode` from the failed Result** — do not hardcode status codes in the controller for failure paths.
- **EF configurations live in `Infrastructure/Data/Configurations/`**, one file per entity. The `DbContext` picks them up automatically — no manual `modelBuilder.Entity<>()` calls inside `OnModelCreating`.
- **Password handling in `AuthService` is placeholder** (`VerifyPassword` does not use bcrypt/Argon2 yet). Treat this as a known gap — if you touch auth, do not build on the current hashing approach.

### Tests (`Portivio.Tests`)

xUnit + Moq + `Microsoft.EntityFrameworkCore.InMemory`. Tests instantiate `AuthService` directly against an in-memory `PortivioDbContext`, so they have no external dependencies. When adding new service tests, follow the same pattern: new in-memory context per test, mock `IConfiguration` for JWT settings.

## Frontend (`src/frontend`)

### Common commands

All commands run from `src/frontend/`. Bun is preferred (see `bunfig.toml`, `bun.lock`) but npm works as a fallback.

```bash
bun install                     # or: npm install
bun start                       # dev server at http://localhost:4200 (= ng serve)
bun run prod                    # production build to dist/portivio
bun run test                    # Karma unit tests (ng test)
bun run lint                    # ng lint

# Run a single test spec
ng test --include='**/auth.service.spec.ts'
```

### Architecture

Classic Angular 18 module-based app (not standalone components). Routing is lazy-loaded at the feature level:

- `src/app/core/` — Singletons loaded once: `AuthService`, route guards (`AuthGuard`), HTTP interceptors (`JwtInterceptor` handles token attach + 401 refresh), and shared models.
- `src/app/shared/` — Cross-feature components and utilities.
- `src/app/features/auth/` — Login, signup, forgot/reset password pages. Lazy-loaded at `/auth`.
- `src/app/features/home/` — Protected layout + dashboard. Lazy-loaded at `/home` (default route).
- `src/environments/` — `environment.ts` (dev) and `environment.prod.ts` (prod) hold `apiUrl` and OAuth client IDs.

Auth state flows through `AuthService` (BehaviorSubject) → `JwtInterceptor` attaches access tokens → `AuthGuard` protects feature routes → 401 triggers refresh via the refresh token stored by the backend as an HttpOnly cookie.

Global styles pull in Bootstrap 5 and FontAwesome via `angular.json` (`styles`/`scripts` arrays), so those are available app-wide without per-component imports.

## Cross-cutting notes

- When adding a new API endpoint that the frontend will call, update both the service method in `Portivio.Application/Services/` (returning `Result<T>`) and a matching method on the Angular side in `src/app/core/services/` — keep DTO shapes in sync with `Portivio.Application/DTOs/`.
- The backend targets **.NET 10** (`net10.0`) — older SDKs will not build the solution.
- When editing `Directory.Packages.props`, bump versions there rather than per-project to avoid CPM violations.
