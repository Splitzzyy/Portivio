# syntax=docker/dockerfile:1.7
# Portivio backend — ASP.NET Core 10 Web API (multi-stage)

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer: copy only project + props files first for better caching
COPY Directory.Build.props Directory.Packages.props Portivio.slnx ./
COPY Portivio.API/Portivio.API.csproj                 Portivio.API/
COPY Portivio.Application/Portivio.Application.csproj Portivio.Application/
COPY Portivio.Domain/Portivio.Domain.csproj           Portivio.Domain/
COPY Portivio.Infrastructure/Portivio.Infrastructure.csproj Portivio.Infrastructure/
COPY Portivio.Tests/Portivio.Tests.csproj             Portivio.Tests/
RUN dotnet restore Portivio.slnx

# Copy rest of the source and publish the API
COPY . .
RUN dotnet publish Portivio.API/Portivio.API.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as non-root user
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5274 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 5274

# Banner log on startup so the mapped port is visible in `docker compose up`
CMD ["sh", "-c", "echo '==> [portivio-backend] API listening on http://0.0.0.0:5274'; exec dotnet Portivio.API.dll"]
