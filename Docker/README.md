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
