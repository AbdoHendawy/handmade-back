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

The API listens on port `8080` (`ASPNETCORE_URLS`). Probes: `/health` (liveness), `/health/ready` (Postgres).

### Migrations (production rule)

**Do not** rely on application startup to migrate. Production/Staging never call `Database.Migrate()` on boot (Development only).

Apply EF migrations out-of-band before serving traffic:

```bash
dotnet ef database update \
  --project src/Handmade.Infrastructure \
  --startup-project src/Handmade.Api
```

Hangfire uses schema `hangfire`. By default `Hangfire:PrepareSchemaIfNecessary=true` so Hangfire can create its tables at first start. For hardened deploys you may set `Hangfire__PrepareSchemaIfNecessary=false` after the schema exists.

### Required Staging / Production environment

See `.env.example`. When `ASPNETCORE_ENVIRONMENT` is `Staging` or `Production`, startup fails unless:

- `ConnectionStrings__Default` is set to a non-localhost host (not `localhost` / `127.0.0.1` / `::1`) and not the repository development DB password
- `Jwt__SecretKey` (≥32 chars)
- `AllowedHosts` is set to specific host(s) (not `*`)
- `Cors__AllowedOrigins__N` has at least one SPA origin
- `Email__Provider=SMTP` with valid SMTP settings
- `FileStorage__Provider=MinIO` with endpoint/keys/bucket/public URL

Do not commit real secrets. Use environment variables or a secrets manager.
