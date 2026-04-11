# Portivio

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
