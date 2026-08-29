# Handmade Backend

ASP.NET Core backend for the **Handmade** Art & Crafts Gallery platform.

This repository is a **modular monolith**. Implemented slices: Sprint 1 foundation, Identity, Seller, Notifications, Catalog, Cart, and Orders (Cash on Delivery checkout plus per-seller lifecycle). Online payment is out of scope.

## Architecture

```
Domain ← Application ← Infrastructure
                ↑
               Api  (references Infrastructure for DI bootstrap only)
```

| Project | Responsibility |
|---|---|
| `Handmade.Domain` | Entity bases, Identity, Seller, Catalog, Cart, Orders, Notifications |
| `Handmade.Application` | Use cases, validation, ports |
| `Handmade.Infrastructure` | EF Core, PostgreSQL, Argon2, JWT, Google validator, email, Hangfire |
| `Handmade.Api` | HTTP, auth middleware, OpenAPI, health, CORS, SignalR |

See [docs/architecture.md](docs/architecture.md), [docs/identity.md](docs/identity.md), [docs/seller.md](docs/seller.md), [docs/catalog.md](docs/catalog.md), [docs/cart.md](docs/cart.md), [docs/orders.md](docs/orders.md), [docs/notifications.md](docs/notifications.md), and [docs/architecture-decisions.md](docs/architecture-decisions.md).

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

# Apply migrations (required out-of-band for Production/Staging — API does not auto-migrate outside Development)
dotnet ef database update \
  --project src/Handmade.Infrastructure \
  --startup-project src/Handmade.Api
```

There is no empty schema. Identity, Seller, Notifications, Catalog, Cart, and Orders tables are created by migrations. See [docs/database.md](docs/database.md).

## Environment configuration

| Setting | Source |
|---|---|
| `ConnectionStrings__Default` | env / user secrets (required; empty in base appsettings) |
| `Jwt__SecretKey` | env / user secrets / secrets store |
| `Cors__AllowedOrigins__0` | env / appsettings |
| `AllowedHosts` | env (required outside Development; not `*`) |
| `Email__*` | env (SMTP required outside Development) |
| `FileStorage__*` | env (MinIO required outside Development) |

Development defaults live in `appsettings.Development.json` (local Docker PostgreSQL / MinIO). Do not commit real secrets. Use `.env` locally (gitignored) or `dotnet user-secrets`. See [Docker/README.md](Docker/README.md) for the optional API image (`Dockerfile`, compose profile `api`) and [docs/cicd-aws-prerequisites.md](docs/cicd-aws-prerequisites.md) for production CI/CD setup.

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
- [Catalog](docs/catalog.md)
- [Cart](docs/cart.md)
- [Orders](docs/orders.md)
- [Notifications](docs/notifications.md)
- [Development](docs/development.md)
- [Database](docs/database.md)
- [API guidelines](docs/api-guidelines.md)
- [Foundation review](docs/backend-foundation-review.md)
