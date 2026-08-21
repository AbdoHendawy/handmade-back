# Database

## Engine

PostgreSQL 16 (Docker image `postgres:16-alpine`).

## EF Core

- Context: `Handmade.Infrastructure.Persistence.HandmadeDbContext`
- Implements: `IApplicationDbContext`
- Provider: Npgsql
- Naming: snake_case via `EFCore.NamingConventions`
- Configurations: `ApplyConfigurationsFromAssembly` under `Persistence/Configurations`
- Auditing: `AuditableInterceptor` sets `CreatedAt` / `UpdatedAt` for `IAuditable`

## Conventions (apply when entities arrive)

| Concern | Convention |
|---|---|
| Table names | Plural snake_case (`artworks`) |
| Column names | snake_case (`created_at`) |
| Primary keys | `id` uuid (UUIDv7) |
| Foreign keys | `{table}_id` (e.g. `maker_id`) |
| Timestamps | `created_at`, `updated_at` (`timestamptz`) |
| Strings | Explicit max lengths in configurations |
| Decimals | Explicit precision/scale (money later) |
| Deletes | Prefer `Restrict`/`NoAction` for important FKs; cascade only when ownership is clear |
| Soft delete | Not global — opt-in later |

## Connection string

```
Host=localhost;Port=5432;Database=handmade;Username=handmade;Password=handmade
```

Override with `ConnectionStrings__Default`.

## Migrations

From repository root:

```bash
dotnet ef migrations add <Name> \
  --project src/Handmade.Infrastructure \
  --startup-project src/Handmade.Api \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/Handmade.Infrastructure \
  --startup-project src/Handmade.Api

dotnet ef migrations list \
  --project src/Handmade.Infrastructure \
  --startup-project src/Handmade.Api
```

### Development reset

```bash
docker compose down -v
docker compose up -d
dotnet ef database update --project src/Handmade.Infrastructure --startup-project src/Handmade.Api
```

## Current schema

No business tables yet. An empty model is intentional until Sprint 2. Verify tooling with:

```bash
dotnet ef migrations add FoundationBaseline --project src/Handmade.Infrastructure --startup-project src/Handmade.Api --output-dir Persistence/Migrations
```

If the migration is empty, it can be removed; the important part is that the pipeline works.
