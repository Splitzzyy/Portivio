# Portivio

## Build & Test Status

### CI/CD Workflows

[![Backend CI](https://github.com/Splitzzyy/Portivio/actions/workflows/backend.yml/badge.svg?branch=main)](https://github.com/Splitzzyy/Portivio/actions/workflows/backend.yml)
[![Frontend CI](https://github.com/Splitzzyy/Portivio/actions/workflows/frontend.yml/badge.svg?branch=main)](https://github.com/Splitzzyy/Portivio/actions/workflows/frontend.yml)
[![Build & Push on Main](https://github.com/Splitzzyy/Portivio/actions/workflows/build-on-main.yml/badge.svg?branch=main)](https://github.com/Splitzzyy/Portivio/actions/workflows/build-on-main.yml)

### Test Results

[![Backend Tests](docs/badges/backend-status.svg)](https://github.com/Splitzzyy/Portivio/actions/workflows/backend.yml)
[![Frontend Tests](docs/badges/frontend-status.svg)](https://github.com/Splitzzyy/Portivio/actions/workflows/frontend.yml)

## Documentation

- [Frontend Architecture](docs/frontend-architecture.md)
- [Backend Architecture](docs/backend-architecture.md)

## Tech Stack

- Backend: ASP.NET Core Web API, .NET 10, Entity Framework Core, PostgreSQL
- Frontend: Angular 18, Bun, Bootstrap, Font Awesome, ngx-toastr
- Infrastructure: Docker Compose, nginx, PostgreSQL 15

## Run with Docker

```bash
docker compose up --build
```

```bash
docker compose up -d
```

```bash
docker compose down
```

```bash
docker compose down -v
```

## Ports

- Frontend: `http://localhost:4200`
- Backend: `http://localhost:5274`
- Swagger: `http://localhost:5274/swagger`
- Postgres: `localhost:5432`
