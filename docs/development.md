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

Development uses `Email:Provider=Console` (no SMTP account required). **Staging/Production** require `Email:Provider=SMTP` plus host/port/from (and username/password when auth is required) via environment variables or secrets — see `.env.example`. Invalid SMTP config fails at startup. Console/empty provider is rejected in Staging/Production only (Development and local test hosts are unchanged).

## File storage

Development defaults to local MinIO via `appsettings.Development.json`. **Staging/Production** require `FileStorage:Provider=MinIO` (endpoint/keys/bucket/public URL) at startup.

## Production / Staging configuration fail-safes

`DeploymentConfigurationGuard` runs at API startup when `ASPNETCORE_ENVIRONMENT` is `Staging` or `Production`:

- `ConnectionStrings__Default` required; must not use `localhost` / `127.0.0.1` / `::1`; must not use the repository development DB password
- `AllowedHosts` required and must not be `*`
- `Cors__AllowedOrigins__N` required (at least one origin)
- `Email__Provider=SMTP` with valid SMTP settings
- `FileStorage__Provider=MinIO` with valid MinIO settings
- JWT remains validated as before (`Jwt__SecretKey` ≥ 32 chars)

Secrets belong in environment variables or a secrets manager — never in the repository.

## Rate limiting

Fixed-window limits protect `POST /api/v1/auth/register|login|google|refresh` and public catalog GETs. Limits are configured under `RateLimiting` (env: `RateLimiting__Auth__PermitLimit`, etc.). Integration tests disable limiting via `RateLimiting:Enabled=false`.

**Staging/Production** require `RateLimiting:Enabled=true` and positive `Auth`/`Catalog` permit limits and window seconds (supplied via environment configuration). Disabling rate limiting or using zero/invalid limits fails at startup.

HTTP **429** means the request was throttled; the endpoint handler is not executed. Responses use the existing ProblemDetails shape (`code=rate_limited`, optional `Retry-After`).

Partitioning is per client IP (`RemoteIpAddress` only). Forwarding headers such as `X-Forwarded-For` are not trusted without explicit trusted-proxy configuration.

## Observability

- Development: console + debug logging
- Staging/Production: JSON console logs with scopes (`traceId` on each request)
- HTTP request logging (method/path/query/status/duration; auth headers and bodies are not logged)
- Correlate client errors with server logs using ProblemDetails `traceId` (same value appears in log scopes)
- `/health` — process liveness; `/health/ready` — PostgreSQL readiness (safe for load balancers; responses must not contain secrets)
- No external telemetry collector is required for local development or CI tests
- Configure production log levels via environment (e.g. `Logging__LogLevel__Default=Information`); do not commit secrets to configuration files

## Health endpoints

- `/health` — process liveness (public; safe for load balancers)
- `/health/ready` — PostgreSQL readiness (public; reveals DB availability only)

## Production / Staging checklist

1. Set `ASPNETCORE_ENVIRONMENT=Production` (or Staging).
2. Supply secrets via environment / secret store (never commit them).
3. Apply EF migrations out-of-band (`dotnet ef database update`) — API does **not** auto-migrate outside Development.
4. Configure JWT, non-local connection string, CORS origins, SMTP, MinIO, AllowedHosts (see fail-safes above).
5. Hangfire dashboard and Swagger/Scalar are Development-only.
6. HSTS is enabled outside Development.

## CI

GitHub Actions (`.github/workflows/ci.yml`) runs on push and pull requests to `main`/`master`:

- Release build and full test suite (Domain, Application, Architecture, Api, then solution)
- Docker must be available on the runner for Testcontainers (PostgreSQL and MinIO integration tests)
- Validates `docker build` against the repo-root `Dockerfile`
- No GitHub secrets are required for the test suite

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
