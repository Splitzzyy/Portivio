# Portivio

Enterprise-level financial portfolio management application. Monorepo containing an **ASP.NET Core 10 Web API** backend, an **Angular 18** SPA frontend, and a **PostgreSQL 15** database — all runnable with a single `docker compose up`.

---

## Tech Stack

### Backend — `src/backend`
| Layer | Tech |
|---|---|
| Runtime | .NET 10 (`net10.0`) |
| Framework | ASP.NET Core Web API |
| ORM | Entity Framework Core + Npgsql (PostgreSQL) |
| Auth | JWT bearer tokens, refresh token via `HttpOnly` cookie |
| Architecture | Clean Architecture — `Domain` / `Application` / `Infrastructure` / `API` |
| Result pattern | `Result<T>` for expected failure paths (no exceptions) |
| Tests | xUnit + Moq + EF Core InMemory |
| Package mgmt | Central Package Management (`Directory.Packages.props`) |

### Frontend — `src/frontend`
| Layer | Tech |
|---|---|
| Framework | Angular 18 (module-based, lazy-loaded features) |
| Runtime / PM | Bun 1.x (npm fallback supported) |
| UI | Bootstrap 5 + FontAwesome |
| HTTP | Axios + Angular HttpClient + JWT interceptor |
| Notifications | ngx-toastr |
| Served in prod | nginx (alpine) — SPA fallback + `/api/` reverse proxy to backend |

### Database
| Layer | Tech |
|---|---|
| Engine | PostgreSQL 15 (alpine) |
| Migrations | EF Core Code-First (`Portivio.Infrastructure/Migrations`) |

---

## Repository Layout

```
Portivio/
├── docker-compose.yml          # orchestrates db + backend + frontend
├── .dockerignore
├── src/
│   ├── backend/
│   │   ├── Dockerfile          # multi-stage: sdk:10.0 → aspnet:10.0
│   │   ├── .dockerignore
│   │   ├── Portivio.slnx
│   │   ├── Portivio.API/
│   │   ├── Portivio.Application/
│   │   ├── Portivio.Domain/
│   │   ├── Portivio.Infrastructure/
│   │   └── Portivio.Tests/
│   └── frontend/
│       ├── Dockerfile          # multi-stage: bun:1-alpine → nginx:alpine
│       ├── .dockerignore
│       ├── nginx.conf
│       └── src/
└── CLAUDE.md
```

---

## Run with Docker Compose

### Prerequisites
- Docker Engine 24+ and Docker Compose v2 (`docker compose`, not `docker-compose`)

### Start everything
```bash
docker compose up --build
```

First run builds all three images; subsequent runs reuse the cache.

On startup the logs print the bound ports, e.g.:
```
portivio-db        | database system is ready to accept connections
portivio-backend   | ==> [portivio-backend] API listening on http://0.0.0.0:5274
portivio-backend   | Now listening on: http://[::]:5274
portivio-frontend  | ==> [portivio-frontend] nginx serving on http://0.0.0.0:80
```

### Service endpoints (from the host)
| Service | Host URL | Container port |
|---|---|---|
| Frontend (nginx) | <http://localhost:4200> | `80` |
| Backend (ASP.NET) | <http://localhost:5274> | `5274` |
| Swagger UI | <http://localhost:5274/swagger> | — (Development only) |
| Postgres | `localhost:5432` | `5432` |

### Common compose commands
```bash
docker compose up --build          # build + start (foreground, logs streamed)
docker compose up -d               # start detached
docker compose logs -f backend     # follow a single service
docker compose ps                  # list running services
docker compose down                # stop + remove containers (keeps the volume)
docker compose down -v             # also drop the pgdata volume (wipes DB)
```

### Environment variables (set in `docker-compose.yml`)
| Var | Purpose |
|---|---|
| `Postgres__ConnectionString` | Backend → database |
| `Jwt__Key` / `Jwt__Issuer` / `Jwt__Audience` | JWT signing + validation |
| `ASPNETCORE_ENVIRONMENT` | `Development` enables Swagger |
| `ASPNETCORE_URLS` | Kestrel bind address (`http://+:5274`) |
| `API_URL` (frontend build arg) | Baked into the Angular bundle |

> Replace `Jwt__Key` with a real ≥32-char secret before deploying anywhere non-local.

---

## Run Without Docker (local dev)

### Backend
```bash
cd src/backend
dotnet restore
dotnet run --project Portivio.API
# → https://localhost:7241  /  http://localhost:5274
```

EF Core migrations:
```bash
dotnet ef migrations add <Name> --project Portivio.Infrastructure --startup-project Portivio.API
dotnet ef database update     --project Portivio.Infrastructure --startup-project Portivio.API
```

Tests:
```bash
dotnet test Portivio.Tests
```

### Frontend
```bash
cd src/frontend
bun install          # or: npm install
bun start            # dev server → http://localhost:4200
bun run prod         # production build → dist/portivio
bun run test         # Karma
bun run lint
```

---

## Docker Image Details

### Backend — `src/backend/Dockerfile`
- **Build stage:** `mcr.microsoft.com/dotnet/sdk:10.0` — copies `.csproj` + props first to maximise restore layer caching, then publishes `Portivio.API` in Release mode.
- **Runtime stage:** `mcr.microsoft.com/dotnet/aspnet:10.0` — slim runtime-only image.
- **Exposed port:** `5274` (`ASPNETCORE_URLS=http://+:5274`).
- Startup banner prints the listening URL so the mapped port is visible in compose logs.

### Frontend — `src/frontend/Dockerfile`
- **Build stage:** `oven/bun:1-alpine` — installs deps with `--frozen-lockfile`, runs `bun run prod`.
- **Runtime stage:** `nginx:alpine` — serves `dist/portivio` with SPA fallback + `/api/` proxied to `http://backend:5274` (see `nginx.conf`).
- **Exposed port:** `80` (mapped to host `4200`).

### Database
- Uses the upstream `postgres:15-alpine` image directly — no custom Dockerfile.
- Data persisted in the `pgdata` named volume.
- Healthcheck via `pg_isready`; backend waits on `service_healthy` before starting.

---

## License
See [LICENSE](LICENSE).
