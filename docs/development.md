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
| `/hubs/notifications` | SignalR (JWT `access_token` query or Bearer) |
| `/hangfire` | Hangfire dashboard (Development only; not mapped outside Development) |
| MinIO console | `http://localhost:9001` (local object storage) |

## Email (outbound)

Development uses `Email:Provider=Console` (no SMTP account required). Outside Development, Console is rejected at startup — set `Email:Provider=SMTP` plus host/port/from (and username/password when auth is required) via environment variables or user secrets — see `.env.example`. Invalid SMTP config fails at startup.

## File storage

Development defaults to local MinIO via `appsettings.Development.json`. Outside Development, `FileStorage:Provider=MinIO` (with endpoint/keys/bucket/public URL) is required at startup.

## Rate limiting

Fixed-window limits protect `POST /api/v1/auth/register|login|google|refresh` and public catalog GETs. Limits are configured under `RateLimiting` (env: `RateLimiting__Auth__PermitLimit`, etc.). Integration tests disable limiting via `RateLimiting:Enabled=false`.

## Observability

- Development: console + debug logging
- Non-Development: JSON console logs with scopes
- HTTP request logging (method/path/status/duration; bodies and auth headers not logged)
- ProblemDetails `traceId` matches log scope `traceId`

## Health endpoints

- `/health` — process liveness (public; safe for load balancers)
- `/health/ready` — PostgreSQL readiness (public; reveals DB availability only)

## Production / Staging checklist

1. Set `ASPNETCORE_ENVIRONMENT=Production` (or Staging).
2. Supply secrets via environment / secret store (never commit them).
3. Apply EF migrations out-of-band (`dotnet ef database update`) — API does **not** auto-migrate outside Development.
4. Configure JWT, connection string, CORS, SMTP, MinIO, AllowedHosts.
5. Hangfire dashboard and Swagger/Scalar are Development-only.
6. HSTS is enabled outside Development.

## User secrets (optional)

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=handmade;Username=handmade;Password=YOUR_PASSWORD" --project src/Handmade.Api
```

## Where to put new code

| Kind of change | Location |
|---|---|
| Domain entity / rule | `Handmade.Domain` (Identity, Seller, Notifications, Catalog, Cart, or Orders folders) |
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
