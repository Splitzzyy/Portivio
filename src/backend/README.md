# Portivio Backend

[![Backend CI](https://github.com/Splitzzyy/Portivio/actions/workflows/backend.yml/badge.svg?branch=main)](https://github.com/Splitzzyy/Portivio/actions/workflows/backend.yml)

ASP.NET Core Web API (.NET 10) + Entity Framework Core + PostgreSQL. Clean Architecture: `API → Application → Domain`, `Infrastructure → Domain`.

## Commands

```bash
dotnet restore
dotnet build
dotnet run --project Portivio.API
dotnet test Portivio.Tests
```

## EF Migrations

```bash
dotnet ef migrations add <Name> --project Portivio.Infrastructure --startup-project Portivio.API
dotnet ef database update --project Portivio.Infrastructure --startup-project Portivio.API
```

## Ports

- HTTP: `http://localhost:5274`
- HTTPS: `https://localhost:7241`
- Swagger (Dev): `/swagger`

## Config

`appsettings.Development.json` (see `appsettings.example.json`):
- `Postgres:ConnectionString` — required
- `Jwt:Key` (≥32 chars), `Jwt:Issuer`, `Jwt:Audience`
