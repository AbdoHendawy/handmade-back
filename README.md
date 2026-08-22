# Handmade Backend

ASP.NET Core backend for the **Handmade** Art & Crafts Gallery platform.

This repository is a **modular monolith** with Sprint 1 foundation, **Sprint 2 Identity & Authentication**, and **Sprint 3 Seller** (applications, admin review, seller profile, suspension).

## Architecture

```
Domain ← Application ← Infrastructure
                ↑
               Api  (references Infrastructure for DI bootstrap only)
```

| Project | Responsibility |
|---|---|
| `Handmade.Domain` | Entity bases, Identity and Seller aggregates, domain exceptions |
| `Handmade.Application` | Auth and Seller use cases, validation, ports |
| `Handmade.Infrastructure` | EF Core, PostgreSQL, Argon2, JWT, Google validator, email |
| `Handmade.Api` | HTTP, auth middleware, OpenAPI, health, CORS |

See [docs/architecture.md](docs/architecture.md), [docs/identity.md](docs/identity.md), [docs/seller.md](docs/seller.md), and [docs/architecture-decisions.md](docs/architecture-decisions.md).

## Prerequisites

- .NET SDK 10+
- Docker Desktop (for PostgreSQL)
- (Optional) EF Core CLI: `dotnet tool install --global dotnet-ef`

## Quick start

### 1. Clone and restore

```bash
dotnet restore
```

### 2. Start PostgreSQL

```bash
cp .env.example .env
docker compose up -d
```

### 3. Run the API

```bash
dotnet run --project src/Handmade.Api
```

- API (HTTP): http://localhost:5159
- API (HTTPS): https://localhost:7152
- Swagger UI: http://localhost:5159/swagger
- Scalar: http://localhost:5159/scalar
- OpenAPI document: http://localhost:5159/openapi/v1.json
- Liveness: http://localhost:5159/health
- Readiness (DB): http://localhost:5159/health/ready
- Status: http://localhost:5159/api/v1/status

### 4. Run tests

```bash
dotnet test
```

Integration tests use Testcontainers PostgreSQL (Docker required).

## Migrations

```bash
# Create a migration (from repo root)
dotnet ef migrations add <Name> \
  --project src/Handmade.Infrastructure \
  --startup-project src/Handmade.Api \
  --output-dir Persistence/Migrations

# Apply migrations
dotnet ef database update \
  --project src/Handmade.Infrastructure \
  --startup-project src/Handmade.Api
```

There is no empty schema. Sprint 2 added Identity; Sprint 3 added Seller tables. See [docs/database.md](docs/database.md).

## Environment configuration

| Setting | Source |
|---|---|
| `ConnectionStrings__Default` | env / user secrets / appsettings |
| `Cors__AllowedOrigins__0` | env / appsettings |

Development defaults point at local Docker PostgreSQL (`handmade` / `handmade`). Do not commit real secrets. Use `.env` locally (gitignored) or `dotnet user-secrets`.

## Project structure

```
src/
  Handmade.Api/
  Handmade.Application/
  Handmade.Domain/
  Handmade.Infrastructure/
tests/
  Handmade.Api.Tests/
  Handmade.Application.Tests/
  Handmade.Architecture.Tests/
  Handmade.Domain.Tests/
Docker/
docs/
docker-compose.yml
Directory.Build.props
Directory.Packages.props
Handmade.sln
```

## Documentation

- [Architecture](docs/architecture.md)
- [Architecture decisions](docs/architecture-decisions.md)
- [Identity](docs/identity.md)
- [Seller](docs/seller.md)
- [Development](docs/development.md)
- [Database](docs/database.md)
- [API guidelines](docs/api-guidelines.md)
- [Foundation review](docs/backend-foundation-review.md)
