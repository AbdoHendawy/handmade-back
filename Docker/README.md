# Docker notes for Handmade backend

## Local development (default)

**PostgreSQL** and **MinIO** are containerized by default. Run the API with `dotnet run` on the host for a faster inner loop.

```bash
cp .env.example .env
docker compose up -d
```

- PostgreSQL: `localhost:5432`
- MinIO API: `http://localhost:9000`
- MinIO console: `http://localhost:9001`

```bash
docker compose down
```

Reset volumes (Postgres + MinIO data):

```bash
docker compose down -v
```

## API container (optional)

A multi-stage non-root image is defined in the repo-root `Dockerfile`.

Build:

```bash
docker build -t handmade-api -f Dockerfile .
```

Run with Compose profile `api` (waits for healthy Postgres + MinIO):

```bash
docker compose --profile api up -d --build
```

The Compose `api` profile defaults to `ASPNETCORE_ENVIRONMENT=Development` for **local smoke testing** only. Override to `Production` or `Staging` only when supplying full external configuration (see below).

The API listens on port `8080` (`ASPNETCORE_URLS`).

### Migrations (production rule)

**Do not** rely on application startup to migrate. Production/Staging never call `Database.Migrate()` on boot (Development only).

Apply EF migrations out-of-band before serving traffic:

```bash
dotnet ef database update \
  --project src/Handmade.Infrastructure \
  --startup-project src/Handmade.Api
```

Hangfire uses schema `hangfire`. By default `Hangfire:PrepareSchemaIfNecessary=true` so Hangfire can create its tables at first start. For hardened deploys you may set `Hangfire__PrepareSchemaIfNecessary=false` after the schema exists.

## Production container

The production image contains **only the published API** — no SDK, no source code, no `.env` files, and no baked-in secrets. Configuration must come from environment variables or a secrets manager at deploy time.

### Runtime characteristics

- **Non-root:** runs as the official ASP.NET `$APP_UID` user (unprivileged).
- **Port:** listens on `8080` inside the container (`ASPNETCORE_URLS=http://+:8080`). Map host ports in your orchestrator as needed.
- **Environment:** image default is `ASPNETCORE_ENVIRONMENT=Production`; override only via deploy configuration.
- **Logging:** structured JSON to stdout/stderr (container runtime collects logs). No file logging inside the container.
- **Do not bake secrets** into the Dockerfile, Compose files, or `appsettings.Production.json`.

### Health probes

Configure HTTP probes in your orchestrator (Kubernetes, ECS, Docker Compose with external tooling, etc.):

| Path | Purpose |
|------|---------|
| `/health` | **Liveness** — no credentials required |
| `/health/ready` | **Readiness** — includes PostgreSQL dependency |

The runtime image does not include `curl` or `wget`. Use orchestrator-native HTTP probes rather than in-container shell healthchecks.

### Required Staging / Production environment

When `ASPNETCORE_ENVIRONMENT` is `Staging` or `Production`, startup fails unless all required values are supplied externally. Phase 1 `DeploymentConfigurationGuard` enforces this at application startup.

Set via environment variables (double-underscore nesting):

```
ASPNETCORE_ENVIRONMENT=Production

ConnectionStrings__Default

AllowedHosts

Cors__AllowedOrigins__0
Cors__AllowedOrigins__1   # additional indices as needed

Email__Provider=SMTP
Email__Host
Email__Port
Email__Username
Email__Password
Email__FromAddress
Email__FromName
Email__EnableSsl

FileStorage__Provider=MinIO
FileStorage__Endpoint
FileStorage__AccessKey
FileStorage__SecretKey
FileStorage__Bucket
FileStorage__UseSsl
```

Also required (see `.env.example`):

- `Jwt__SecretKey` (≥32 characters)
- `Jwt__Issuer`, `Jwt__Audience` (as configured for your deployment)
- `FileStorage__PublicBaseUrl` (public URL for stored assets)

Guard rules (not duplicated in Docker):

- `ConnectionStrings__Default` must not use `localhost` / `127.0.0.1` / `::1` or the repository development password
- `AllowedHosts` must be specific host name(s), not `*`
- `Email__Provider` must be `SMTP` (not Console)
- `FileStorage__Provider` must be `MinIO` (not Local)

Do not commit real secrets. Use environment variables or a secrets manager.

## AWS EC2 production deployment

This section documents how to run the Handmade API on an **Amazon Linux 2023** EC2 instance with **Docker Compose**. Production configuration (`.env`) stays on the host and is never committed.

For first-time AWS/GitHub setup before enabling automated deploys, see [docs/cicd-aws-prerequisites.md](../docs/cicd-aws-prerequisites.md).

### EC2 prerequisites

- Amazon Linux 2023
- Docker and **Docker Compose v2** installed and running
- Git installed; repository cloned on the host
- **SSM Agent** running (for GitHub Actions deploy via Run Command)
- Production `.env` in the repository directory on EC2
- Nginx on port **80** proxying to `127.0.0.1:8080`
- API container name: `handmade-api`, internal port **8080**

### Security group (recommended)

Configure inbound rules on the EC2 security group:

| Port | Protocol | Source | Purpose |
|------|----------|--------|---------|
| 80 | TCP | `0.0.0.0/0` (or your CDN/load balancer) | HTTP |
| 443 | TCP | `0.0.0.0/0` (or your CDN/load balancer) | HTTPS |
| 22 | TCP | **Administrator IP only** | SSH administration |

Do **not** open SSH (`22`) to `0.0.0.0/0`.

Do **not** publicly expose PostgreSQL, MinIO, or Hangfire dashboard ports unless your architecture explicitly requires it (the default design keeps these private).

The API process binds to **8080** inside the container. Map host port `8080:8080` for direct exposure, or terminate TLS on a separate edge layer and forward to the container. This repository does not include a reverse proxy in the deployment path.

### Configuration strategy

Supply production configuration from the EC2 host `.env` file:

```text
EC2 .env (not committed)
        ↓
docker compose env_file + interpolation
        ↓
ASP.NET Core configuration
```

Never commit:

```text
.env
.env.production
production.env
*.secret
```

The repository `.env.example` is documentation only. Automated deploys **do not** modify or upload `.env`.

### Complete production configuration matrix

Use the exact configuration keys already present in the codebase. All secret values must be placeholders at deploy time.

#### Core (required)

| Environment variable | Notes |
|----------------------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` (or `Staging`) |
| `ConnectionStrings__Default` | Managed PostgreSQL; not `localhost` / `127.0.0.1` / `::1`; must not contain the repository development password marker `Password=handmade` |
| `AllowedHosts` | Specific host name(s); not `*` |
| `Cors__AllowedOrigins__0` | At least one SPA origin; add `Cors__AllowedOrigins__1`, … as needed |

#### JWT (required)

Validated at startup in `DependencyInjection` (`Jwt:SecretKey` ≥ 32 characters).

| Environment variable | Notes |
|----------------------|-------|
| `Jwt__SecretKey` | Required secret (≥ 32 chars) |
| `Jwt__Issuer` | e.g. `Handmade` |
| `Jwt__Audience` | e.g. `Handmade` |
| `Jwt__AccessTokenExpirationMinutes` | Optional (default 15) |
| `Jwt__RefreshTokenExpirationDays` | Optional (default 14) |

#### Email — SMTP (required in Production/Staging)

| Environment variable | Notes |
|----------------------|-------|
| `Email__Provider` | Must be `SMTP` |
| `Email__Host` | SMTP host |
| `Email__Port` | e.g. `587` |
| `Email__Username` | When authentication is required |
| `Email__Password` | SMTP credential (secret) |
| `Email__FromAddress` | Sender address |
| `Email__FromName` | Display name |
| `Email__EnableSsl` | `true` / `false` |

#### File storage — MinIO (required in Production/Staging)

| Environment variable | Notes |
|----------------------|-------|
| `FileStorage__Provider` | Must be `MinIO` |
| `FileStorage__Endpoint` | Host:port of MinIO API |
| `FileStorage__AccessKey` | MinIO access key (secret) |
| `FileStorage__SecretKey` | MinIO secret key (secret) |
| `FileStorage__Bucket` | Bucket name |
| `FileStorage__UseSsl` | `true` / `false` |
| `FileStorage__PublicBaseUrl` | Public URL prefix for stored assets |

#### Rate limiting (required in Production/Staging)

Phase 2 validation: `Enabled=true`; permit limits 1–10,000; window seconds 1–3,600.

| Environment variable | Notes |
|----------------------|-------|
| `RateLimiting__Enabled` | Must be `true` |
| `RateLimiting__Auth__PermitLimit` | Auth endpoints (register/login/google/refresh) |
| `RateLimiting__Auth__WindowSeconds` | Auth window |
| `RateLimiting__Catalog__PermitLimit` | Public catalog GETs |
| `RateLimiting__Catalog__WindowSeconds` | Catalog window |

#### Hangfire (background jobs)

Uses the same `ConnectionStrings__Default` as the application. Dashboard is **Development-only**.

| Environment variable | Notes |
|----------------------|-------|
| `Hangfire__Enabled` | Default `true` in production appsettings |
| `Hangfire__PrepareSchemaIfNecessary` | Default `true`; set `false` after the `hangfire` schema exists |

#### Optional

| Environment variable | Notes |
|----------------------|-------|
| `GoogleAuth__ClientId` | Required only when Google Sign-In is enabled |
| `AdminSeed__Enabled` / `AdminSeed__Email` / `AdminSeed__Password` | One-time bootstrap only; not for routine production |
| `Logging__LogLevel__*` | Optional production log level overrides (no secrets) |

Startup fail-fast rules are enforced by `DeploymentConfigurationGuard` and `RateLimitingOptions.EnsureValidForDeployment` — do not bypass them in Docker or deployment scripts.

### Database migration policy

**Application startup does not migrate the database in Production or Staging.** `Program.cs` calls `Database.MigrateAsync()` only when `ASPNETCORE_ENVIRONMENT` is Development.

Before serving production traffic, apply EF migrations out-of-band from a trusted host or migrate job:

```bash
dotnet ef database update \
  --project src/Handmade.Infrastructure \
  --startup-project src/Handmade.Api
```

Use a connection string with permission to apply migrations. Starting the API container does **not** replace this step.

### Automated production deploy (CI/CD)

After [AWS prerequisites](../docs/cicd-aws-prerequisites.md) are configured:

| Workflow | Trigger | Action |
|----------|---------|--------|
| [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) | Pull request to `main` | restore → build → test → `docker build` validate |
| [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) | Push to `main` | CI steps → push image to **ECR** (`{git-sha}` tag) → **SSM** runs `scripts/deploy-api.sh` on EC2 |

Deploy flow on EC2 (API only — Postgres and MinIO unchanged):

```bash
export API_IMAGE=<account>.dkr.ecr.<region>.amazonaws.com/handmade-api:<git-sha>
./scripts/deploy-api.sh
```

The script:

1. Logs in to ECR (when `API_IMAGE` is an ECR URI)
2. `docker compose -f docker-compose.yml -f docker-compose.prod.yml --profile api pull api`
3. `docker compose ... up -d --no-deps api`
4. Waits for `GET /health` (default `http://127.0.0.1:8080/health`)
5. On failure, rolls back to the previous image recorded in `.deploy/previous-api-image`

Image tags use the Git commit SHA — **not** `latest`.

### Manual production deploy (Compose)

Use when debugging or before CI/CD is enabled. Requires `API_IMAGE` or a locally built tag.

```bash
cd /path/to/handmade-back
git pull

# Option A — image already in ECR
export API_IMAGE=<account>.dkr.ecr.<region>.amazonaws.com/handmade-api:<tag>
./scripts/deploy-api.sh

# Option B — build on host (not recommended for routine production)
docker build -t handmade-api:release .
export API_IMAGE=handmade-api:release
docker compose -f docker-compose.yml -f docker-compose.prod.yml --profile api up -d --no-deps api
curl -fsS http://127.0.0.1:8080/health
```

Verify after deploy:

```bash
docker logs handmade-api
docker exec handmade-api id -u    # expect 1654 (non-root $APP_UID)
curl -fsS http://127.0.0.1:8080/health
```

Configure HTTP probes against `/health` (liveness) and optionally `/health/ready` (PostgreSQL readiness). The image has no `curl`/`wget` and no Dockerfile `HEALTHCHECK`.

### Legacy manual deploy (`docker run`)

The following `docker run` sequence remains valid for emergencies but is superseded by Compose + `scripts/deploy-api.sh` for routine deploys:

<details>
<summary>docker run (legacy)</summary>

```bash
git pull
docker build -t handmade-api:release .
docker stop handmade-api || true
docker rm handmade-api || true
docker run -d \
  --name handmade-api \
  --restart unless-stopped \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__Default="<POSTGRES_CONNECTION_STRING>" \
  -e AllowedHosts="<API_HOSTNAME>" \
  -e Cors__AllowedOrigins__0="<SPA_ORIGIN_URL>" \
  -e Jwt__SecretKey="<JWT_SECRET>" \
  -e Jwt__Issuer="<JWT_ISSUER>" \
  -e Jwt__Audience="<JWT_AUDIENCE>" \
  -e Email__Provider=SMTP \
  -e Email__Host="<SMTP_HOST>" \
  -e Email__Port="<SMTP_PORT>" \
  -e Email__Username="<SMTP_USERNAME>" \
  -e Email__Password="<SMTP_PASSWORD>" \
  -e Email__FromAddress="<FROM_ADDRESS>" \
  -e Email__FromName="<FROM_NAME>" \
  -e Email__EnableSsl="<SMTP_ENABLE_SSL>" \
  -e FileStorage__Provider=MinIO \
  -e FileStorage__Endpoint="<MINIO_ENDPOINT>" \
  -e FileStorage__AccessKey="<MINIO_ACCESS_KEY>" \
  -e FileStorage__SecretKey="<MINIO_SECRET_KEY>" \
  -e FileStorage__Bucket="<MINIO_BUCKET>" \
  -e FileStorage__UseSsl="<MINIO_USE_SSL>" \
  -e FileStorage__PublicBaseUrl="<MINIO_PUBLIC_BASE_URL>" \
  -e RateLimiting__Enabled=true \
  -e RateLimiting__Auth__PermitLimit="<AUTH_PERMIT_LIMIT>" \
  -e RateLimiting__Auth__WindowSeconds="<AUTH_WINDOW_SECONDS>" \
  -e RateLimiting__Catalog__PermitLimit="<CATALOG_PERMIT_LIMIT>" \
  -e RateLimiting__Catalog__WindowSeconds="<CATALOG_WINDOW_SECONDS>" \
  -e Hangfire__Enabled=true \
  -e Hangfire__PrepareSchemaIfNecessary=true \
  handmade-api:release
```

</details>

### Production logging

Preserve Phase 3 observability:

- Structured **JSON** logs to stdout/stderr (`docker logs handmade-api`)
- `traceId` in log scopes for correlation with ProblemDetails responses
- HTTP request logging does **not** log authorization headers or request bodies
- No file logging, Serilog, OpenTelemetry, or metrics agents required in the container

Optional overrides: `Logging__LogLevel__Default`, `Logging__LogLevel__Microsoft.AspNetCore`, etc.

### Local smoke (unchanged)

The Compose `api` profile remains for **local Development smoke** only:

```bash
docker compose --profile api up -d --build
```

Expect `GET http://localhost:8080/health` → `200`. Do not use Compose dev defaults as a stand-in for production configuration on EC2.
