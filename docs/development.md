# Development guide

## First-time setup

1. Install .NET 10 SDK.
2. Install Docker Desktop.
3. Clone the repository.
4. Copy environment template:

   ```bash
   cp .env.example .env
   ```

5. Start PostgreSQL:

   ```bash
   docker compose up -d
   ```

6. Restore and build:

   ```bash
   dotnet restore
   dotnet build
   ```

7. Run the API:

   ```bash
   dotnet run --project src/Handmade.Api --launch-profile https
   ```

## Useful URLs

| URL | Purpose |
|---|---|
| `/swagger` | Swagger UI (JWT Authorize) |
| `/scalar` | Scalar OpenAPI UI |
| `/openapi/v1.json` | OpenAPI document |
| `/health` | Process liveness |
| `/health/ready` | PostgreSQL readiness |
| `/api/v1/status` | Version smoke check |

## User secrets (optional)

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=handmade;Username=handmade;Password=YOUR_PASSWORD" --project src/Handmade.Api
```

## Where to put new code

| Kind of change | Location |
|---|---|
| Domain entity / rule | `Handmade.Domain` (Identity or Seller folders) |
| Use case / validator / port | `Handmade.Application` |
| EF config / external IO | `Handmade.Infrastructure` |
| HTTP endpoint / middleware | `Handmade.Api` |
| Unit test | matching `tests/*` project |
| Architecture rule | `Handmade.Architecture.Tests` |

## Coding norms

- Async all the way; accept `CancellationToken` on public async APIs.
- Do not put business validation in controllers.
- Do not reference Infrastructure types from Application/Domain.
- Prefer small focused classes over large services.
