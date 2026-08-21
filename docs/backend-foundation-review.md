# Backend foundation review

**Status:** Complete — Sprint 1 foundation only. No business features.

**Validated:** 2026-08-21

## 1. Final architecture

Clean Architecture modular monolith on **.NET 10**:

- `Handmade.Domain` — pure domain primitives
- `Handmade.Application` — ports, validation, use-case prep
- `Handmade.Infrastructure` — EF Core / PostgreSQL / storage stub
- `Handmade.Api` — HTTP composition root

## 2. Project structure

```
handmade-back/
├── src/
│   ├── Handmade.Api/
│   ├── Handmade.Application/
│   ├── Handmade.Domain/
│   └── Handmade.Infrastructure/
├── tests/
│   ├── Handmade.Api.Tests/
│   ├── Handmade.Application.Tests/
│   ├── Handmade.Architecture.Tests/
│   └── Handmade.Domain.Tests/
├── Docker/
├── docs/
├── .editorconfig
├── .gitignore
├── .env.example
├── docker-compose.yml
├── Directory.Build.props
├── Directory.Packages.props
├── Handmade.sln
└── README.md
```

Workspace root is the backend (no nested `backend/` folder).

## 3. Dependency graph

```
Domain ← Application ← Infrastructure
              ↑
             Api ·····> Infrastructure (DI bootstrap only)
```

Enforced by `Handmade.Architecture.Tests` (NetArchTest).

## 4–5. NuGet packages and why

| Package | Why |
|---|---|
| Microsoft.AspNetCore.OpenApi | OpenAPI document |
| Microsoft.OpenApi (pinned 2.12.2) | Security fix vs transitive 2.0.0 |
| Scalar.AspNetCore | Dev API UI |
| AspNetCore.HealthChecks.NpgSql | DB readiness |
| FluentValidation (+ DI) | Application validation |
| EF Core + Relational + Design | Persistence / migrations |
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL provider |
| EFCore.NamingConventions | snake_case |
| Microsoft.Extensions.Configuration.* | Design-time factory + options |
| xUnit + Test SDK + coverlet | Tests |
| Microsoft.AspNetCore.Mvc.Testing | API integration tests |
| Testcontainers.PostgreSql | Real PostgreSQL in tests |
| SSH.NET (pinned 2026.0.0) | Transitive vuln fix for Testcontainers |
| NetArchTest.Rules | Architecture dependency tests |

**Not added:** MediatR, Serilog, AutoMapper, generic repos, Redis, buses, CQRS frameworks.

## 6. Database strategy

PostgreSQL 16 via Docker Compose. EF Core + Npgsql. snake_case naming. Empty business schema by design. Baseline migration `FoundationBaseline` (empty Up/Down) proves the pipeline.

## 7. ID strategy

UUIDv7 via `Guid.CreateVersion7()` — see ADR-001.

## 8. Timestamp strategy

`DateTimeOffset` UTC + `IAuditable` + `AuditableInterceptor` — see ADR-002.

## 9. Transaction strategy

DbContext as unit of work; no generic UoW/repository — see ADR-004.

## 10. Error handling strategy

`IExceptionHandler` → ProblemDetails for validation, domain, not found, conflict, forbidden, unauthorized, 500. No stack traces/secrets in Production responses.

## 11. Validation strategy

FluentValidation in Application; `ValidationBehavior.ValidateAndThrowAsync` helper (no MediatR pipeline).

## 12. Logging strategy

`Microsoft.Extensions.Logging` (console/debug). No Serilog yet — see ADR-009.

## 13. Configuration strategy

`appsettings.json` + `appsettings.Development.json` + env vars / user secrets. CORS and connection strings are configuration-driven.

## 14. Security decisions

- HTTPS redirection
- Security headers middleware
- CORS allow-list (never `AllowAnyOrigin` for prod)
- Request body size limit (20 MB)
- Safe ProblemDetails
- Secrets not committed (`.env` gitignored)
- Bearer scheme declared in OpenAPI; auth not implemented

## 15. Docker setup

`docker-compose.yml` runs PostgreSQL only with named volume `handmade_pgdata`. `.env.example` documents credentials.

## 16. Testing strategy

| Project | Coverage |
|---|---|
| Domain.Tests | Entity Id/equality, exceptions |
| Application.Tests | ValidationBehavior |
| Architecture.Tests | Layer dependency rules |
| Api.Tests | Health, status, OpenAPI, DB connect via Testcontainers |

## 17. API conventions

URL versioning `/api/v1`, no response envelope, ProblemDetails errors, future pagination contract documented in `docs/api-guidelines.md`.

## 18. File storage strategy

`IFileStorage` port + `NotConfiguredFileStorage` stub. Binaries must not go in PostgreSQL. Provider (S3/R2/Azure/MinIO) in a later sprint.

## 19. Decisions made

Documented in `docs/architecture-decisions.md` (ADR-001 … ADR-012).

## 20. Alternatives rejected

MediatR, Result monad, soft-delete base, response wrappers, Serilog-now, header API versioning, UUID v4, int PKs, generic UoW/repos, microservices.

## 21. Potential risks

- Docker must be running for compose + Api integration tests.
- Empty baseline migration is a no-op; first real schema change will be Sprint 2.
- `EFCore.NamingConventions` lagged EF Relational briefly; Relational is pinned to 10.0.11.
- Global `TreatWarningsAsErrors` is strict (good) but requires Migrations IDE0161 suppression.
- `dotnet-ef` global tool (10.0.5) lags runtime 10.0.11 — upgrade recommended.

## 22. Technical debt

- No real file storage provider
- No OpenTelemetry / centralized logging sinks
- No Staging/Production appsettings beyond pattern
- Status controller is a smoke endpoint only
- Architecture tests reference Api (loads all assemblies) — acceptable

## 23. Intentionally postponed

Auth/JWT, users, makers, artworks, categories, tags, likes, saves, collections, search, marketplace, payments, notifications, soft delete, MediatR, Serilog, MinIO compose service.

## 24. Recommended next sprint

Sprint 2 — Identity & Auth foundation (users entity, registration/login contracts, JWT, refresh tokens) **or** Artworks/Makers domain modeling — product call. Do not mix both in one sprint.

Suggested order:

1. User aggregate + EF configuration + migration
2. Auth use cases + FluentValidation
3. JWT issuance (still no marketplace)

## 25. Commands to run

```bash
cp .env.example .env
docker compose up -d
dotnet restore
dotnet build
dotnet test
dotnet ef database update --project src/Handmade.Infrastructure --startup-project src/Handmade.Api
dotnet run --project src/Handmade.Api --launch-profile https
```

URLs: `/scalar`, `/health`, `/health/ready`, `/api/v1/status`

## Validation results (this review)

| Check | Result |
|---|---|
| `dotnet restore` / `build` | Pass |
| `dotnet test` | Pass (16 tests: 5+2+4+5) |
| Docker PostgreSQL | Healthy |
| `GET /health` | Healthy |
| `GET /health/ready` | Healthy |
| `GET /api/v1/status` | 200 |
| OpenAPI `/openapi/v1.json` | 200 |
| Scalar `/scalar` | 200 |
| Migrations create/apply | Pass (`FoundationBaseline`) |
| Architecture tests | Pass |
