# Architecture decisions

## ADR-001: Entity identifiers — UUIDv7 (`Guid.CreateVersion7`)

**Decision:** Use `Guid` generated with `Guid.CreateVersion7()` as the primary key strategy.

**Reason:**

- Client-generable and friendly to distributed inserts.
- Time-ordered → better B-tree locality than random UUID v4.
- Not guessable like sequential integers.
- First-class in .NET 9+ / .NET 10 and PostgreSQL (`uuid`).

**Alternatives considered:**

| Option | Why rejected |
|---|---|
| `int` / `bigint` identity | Sequential, leaks volume, harder for offline/client ids |
| UUID v4 | Random → index fragmentation under heavy inserts |
| ULID as string | Extra type surface; Guid v7 covers the same needs |

**Trade-offs:** Slightly larger keys than `bigint`; acceptable for this domain.

---

## ADR-002: Timestamps — `DateTimeOffset` UTC

**Decision:** Audit fields use `DateTimeOffset` and are written as UTC via `IClock` + EF interceptor. PostgreSQL stores `timestamptz`.

**Reason:** Explicit offset semantics at the application boundary; UTC is the source of truth.

**Alternatives:** `DateTime` with `DateTimeKind.Utc` (also valid with Npgsql). Chosen `DateTimeOffset` for clarity in APIs and tests.

---

## ADR-003: Soft delete — not on base entity

**Decision:** Do not put soft delete on `Entity` / `AggregateRoot`.

**Where soft delete helps later:** user accounts, moderated content, recoverable deletes.

**Where it hurts:** unique constraints, queries forgetting filters, GDPR hard-delete needs, analytics complexity.

**Recommendation:** Opt-in per aggregate when a concrete requirement appears.

---

## ADR-004: Transactions — EF Core DbContext as unit of work

**Decision:** No generic `IUnitOfWork` or repository base. `HandmadeDbContext` / `IApplicationDbContext.SaveChangesAsync` is the unit of work. Use `Database.BeginTransactionAsync` only when a use case needs multi-step atomicity beyond a single `SaveChanges`.

**Reason:** EF Core already tracks changes; wrapping it adds ceremony without value at this stage.

---

## ADR-005: MediatR — rejected for foundation

**Decision:** Do not add MediatR.

**Why people add it:** pipeline behaviors, decoupling controllers from handlers.

**Why we skip it:** explicit application services are clearer for a growing modular monolith; FluentValidation is invoked via `ValidationBehavior` helper. Revisit only if cross-cutting pipeline duplication becomes painful.

---

## ADR-006: Result monad — rejected

**Decision:** Prefer exceptions (`DomainException`, FluentValidation `ValidationException`) mapped to ProblemDetails.

**Reason:** Enough for foundation; avoids dual error channels. Can introduce a `Result` type later for expected business failures if needed.

---

## ADR-007: API versioning — URL path `/api/v1`

**Decision:** Version in the URL (`/api/v1/...`).

**Reason:** Obvious for Angular clients, cacheable, visible in Scalar/OpenAPI. Header versioning is harder to discover and debug.

---

## ADR-008: Response envelope — none

**Decision:** No `{ success, data }` wrapper. Use HTTP status codes + resource bodies + RFC 7807 ProblemDetails for errors.

**Reason:** Matches REST/HTTP semantics; Angular `HttpClient` already handles status codes.

---

## ADR-009: Logging — built-in abstractions

**Decision:** Use `Microsoft.Extensions.Logging` only. No Serilog yet.

**Reason:** Sufficient for foundation; can plug Serilog/OpenTelemetry sinks later without rewriting call sites.

---

## ADR-010: Database naming — snake_case

**Decision:** EF Core naming conventions map CLR PascalCase to PostgreSQL `snake_case` (`artworks`, `created_at`).

**Reason:** Idiomatic PostgreSQL; consistent SQL for DBA tooling.

---

## ADR-011: File storage — `IFileStorage` port only

**Decision:** Define `IFileStorage` in Application; Infrastructure registers `NotConfiguredFileStorage` until a real provider (S3/R2/Azure/MinIO) is chosen.

**Reason:** Artwork binaries must not live in PostgreSQL; the port prevents leakage of vendor SDKs into Application.

**Local strategy later:** MinIO via Docker Compose alongside PostgreSQL.

---

## ADR-012: OpenAPI UI — Scalar

**Decision:** `Microsoft.AspNetCore.OpenApi` + Scalar UI. Bearer security scheme is declared but unused until auth sprint.
