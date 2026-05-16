# Portivio

Financial portfolio management application. Track stocks, mutual funds, SIP plans, and portfolio performance across multiple profiles.

## Build Status

| | Backend | Frontend |
|---|:-------:|:--------:|
| Build | [![Build](https://github.com/Splitzzyy/Portivio/actions/workflows/build-on-main.yml/badge.svg?branch=main)](https://github.com/Splitzzyy/Portivio/actions/workflows/build-on-main.yml) | [![Build](https://github.com/Splitzzyy/Portivio/actions/workflows/build-on-main.yml/badge.svg?branch=main)](https://github.com/Splitzzyy/Portivio/actions/workflows/build-on-main.yml) |
| Tests | ![Backend tests](https://img.shields.io/badge/tests-198%2F198%20passed-brightgreen) | ![Frontend tests](https://img.shields.io/badge/frontend%20tests-123%2F123%20passed-brightgreen) |

Both backend and frontend builds/tests run through the single `build-on-main.yml` workflow.

[![Secret Scan](https://github.com/Splitzzyy/Portivio/actions/workflows/secret-scan.yml/badge.svg)](https://github.com/Splitzzyy/Portivio/actions/workflows/secret-scan.yml)

---

## Features

- **Portfolio Tracking:** Manage stocks, mutual funds, FDs, RDs, PPF, and Gold.
- **Multiple Profiles:** Track investments for different family members or entities.
- **Performance Analytics:** Real-time returns, unrealized P/L, and historical snapshots.
- **Email Summaries:** Scheduled and manual investment summaries with customizable frequency.
- **Centralized Auditing:** Security-critical events logged with IP and device metadata.
- **SSO Integration:** Google Login support alongside traditional email/password.
- **Modern UI:** Responsive Angular 18 frontend with rich dashboard visualizations.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core Web API · .NET 10 · Clean Architecture |
| ORM | Entity Framework Core 10 · PostgreSQL 15 |
| Background Jobs | Hangfire (PostgreSQL-backed) |
| Email | MailKit / MimeKit (SMTP) |
| Frontend | Angular 18 · Bootstrap 5 · FontAwesome · ngx-toastr |
| Package Manager | Bun (frontend) |
| Infrastructure | Docker Compose · nginx · Mailpit (dev email) |

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                      Angular 18 SPA                      │
│  Auth · Dashboard · Holdings · Transactions · SIP Plans  │
│              http://localhost:4200 (nginx)                │
└─────────────────────┬───────────────────────────────────┘
                      │ /api/* proxy
┌─────────────────────▼───────────────────────────────────┐
│                  ASP.NET Core API (.NET 10)               │
│                                                          │
│  ┌──────────┐  ┌─────────────┐  ┌──────────────────┐   │
│  │  Domain  │◄─│ Application │◄─│   API Controllers│   │
│  │ Entities │  │  Services   │  │  JWT · RateLimit  │   │
│  └──────────┘  └──────┬──────┘  └──────────────────┘   │
│                        │                                 │
│  ┌─────────────────────▼────────────────────────────┐   │
│  │              Infrastructure                       │   │
│  │  EF Core · Hangfire Jobs · SMTP Email Service    │   │
│  └─────────────────────┬────────────────────────────┘   │
└────────────────────────┼────────────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         ▼               ▼               ▼
    PostgreSQL       Hangfire        Mailpit
    (data + jobs)   (workers)     (dev email UI)
```

### Backend Layer Responsibilities

| Layer | Project | Responsibility |
|-------|---------|---------------|
| Domain | `Portivio.Domain` | Entities, enums — no dependencies |
| Application | `Portivio.Application` | Business logic, `Result<T>` pattern, DTOs |
| Infrastructure | `Portivio.Infrastructure` | EF Core, Hangfire, SMTP email |
| API | `Portivio.API` | Controllers, middleware, DI wiring |


---

## Quick Start (Docker)

```bash
# Clone and start everything
git clone https://github.com/Splitzzyy/Portivio.git
cd Portivio
docker compose up --build
```

| Service | URL |
|---------|-----|
| Frontend | http://localhost:4200 |
| Backend API | http://localhost:5274 |
| Swagger UI | http://localhost:5274/swagger |
| Hangfire Dashboard | http://localhost:5274/hangfire |
| Mailpit (email UI) | http://localhost:8025 |
| PostgreSQL | localhost:5432 |

Database migrations run automatically on backend startup.

---

## Local Development

### Prerequisites

- .NET 10 SDK
- Node.js + Bun (`npm install -g bun`)
- PostgreSQL 15 (or Docker for just the DB)
- Docker (optional, for Mailpit)

### Backend Setup

```bash
# 1. Start PostgreSQL (if using Docker)
docker run -d --name portivio-db -p 5432:5432 \
  -e POSTGRES_PASSWORD=0000 -e POSTGRES_DB=portivio \
  postgres:15-alpine

# 2. Start Mailpit for dev email (optional)
docker run -d --name portivio-mailpit \
  -p 1025:1025 -p 8025:8025 axllent/mailpit

# 3. Configure appsettings.Development.json
cp src/backend/Portivio.API/appsettings.example.json \
   src/backend/Portivio.API/appsettings.Development.json
# Edit with your local values

# 4. Run
cd src/backend
dotnet run --project Portivio.API
```

### Frontend Setup

```bash
cd src/frontend
bun install
bun start          # http://localhost:4200
```

## Background Jobs (Hangfire)

Email sending runs as background jobs via Hangfire with PostgreSQL backing.

| Trigger | Job |
|---------|-----|
| Signup | Verification email + welcome email |
| Resend verification | New verification email |
| Forgot password | Password reset email |
| Google SSO (new user) | Welcome email |

**Dashboard:** http://localhost:5274/hangfire (Development only)

Failed jobs retry automatically with exponential backoff. Monitor via Mailpit at http://localhost:8025 in development.

---

## Running Tests

```bash
# Backend (from src/backend)
dotnet test Portivio.Tests

# Run a specific test class
dotnet test Portivio.Tests --filter "FullyQualifiedName~AuthServiceLoginTests"

# Frontend (from src/frontend)
bun run test
```

---

## CI/CD

| Workflow | Trigger | What it does |
|----------|---------|-------------|
| `pr-validation.yml` | pull_request | **Validation:** Restore · Build · Test for both layers |
| `backend.yml` | workflow_call | **Backend:** Restore · Build · Test · Docker push |
| `frontend.yml` | workflow_call | **Frontend:** Install · Build · Test · Docker push |
| `build-on-main.yml` | push to main | **Deployment:** Runs both · Updates badges |
| `secret-scan.yml` | push/PR | **Security:** Gitleaks secret detection |

Docker images pushed to Docker Hub: `rghvgrv/portifio-api`, `rghvgrv/portifio-frontend`

---

## Project Structure

```
Portivio/
├── src/
│   ├── backend/
│   │   ├── Portivio.Domain/          # Entities, enums
│   │   ├── Portivio.Application/     # Services, DTOs, Result<T>
│   │   ├── Portivio.Infrastructure/  # EF Core, Hangfire, Email
│   │   ├── Portivio.API/             # Controllers, Program.cs
│   │   └── Portivio.Tests/           # xUnit tests
│   └── frontend/
│       └── src/app/
│           ├── core/                 # AuthService, Guards, Interceptors
│           ├── features/auth/        # Login, Signup, Forgot/Reset Password
│           └── features/home/        # Dashboard, Holdings, Transactions, SIP
├── .githooks/
│   └── pre-commit                    # Secret scanner
├── .github/workflows/                # CI/CD pipelines
├── docker-compose.yml
├── MISSING.md                        # Known gaps and roadmap
└── README.md
```

---

## Documentation

- [Frontend Architecture](docs/frontend-architecture.md)
- [Backend Architecture](docs/backend-architecture.md)
- [Missing Features & Gaps](MISSING.md)

---

## Docker Commands

```bash
docker compose up --build          # Build and start all services
docker compose up -d               # Start in background
docker compose down                # Stop services
docker compose down -v             # Stop and remove volumes (deletes DB data)
docker compose up --build backend  # Rebuild backend only
docker compose logs -f backend     # Stream backend logs
```

---
