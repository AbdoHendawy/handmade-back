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

## ADR-012: OpenAPI UI — Swagger + Scalar

**Decision:** `Microsoft.AspNetCore.OpenApi` generates `/openapi/v1.json`. Development UIs:

- Swagger UI at `/swagger` (JWT Authorize, Try it out)
- Scalar at `/scalar`

Bearer JWT is a global security scheme so authenticated endpoints can be called from the UI after pasting an access token.

---

## ADR-013: Password hashing — Argon2id

**Decision:** `IPasswordHasher` implemented with Argon2id (`Konscious.Security.Cryptography.Argon2`).

**Reason:** Memory-hard, modern default for password storage. Never log passwords or hashes.

---

## ADR-014: Google auth — SPA id_token

**Decision:** Angular obtains a Google ID token; API validates it at `POST /api/v1/auth/google` using `Google.Apis.Auth`.

**Reason:** Fits SPA architecture; avoids server-side redirect OAuth complexity for v1.

---

## ADR-015: Session revocation — SecurityStamp

**Decision:** JWT includes `sst` claim; admin `revoke-sessions` increments `User.SecurityStamp` and revokes all refresh tokens. Bearer `OnTokenValidated` enforces stamp match.

**Reason:** Immediate kick without Redis JWT blacklist.

---

## ADR-016: Welcome email — console + flag idempotency

**Decision:** `IEmailSender` with Development `ConsoleEmailSender`; Identity welcome-mail idempotency via `User.WelcomeEmailSent`. Seller (and future modules) persist an in-app `Notification` then send email from the Hangfire delivery job. No Outbox table yet.

**Reason:** Avoid broker complexity; Outbox can be added later without redesigning Identity or Notification ports.

---

## ADR-017: Notifications — persist first, Hangfire, SignalR last

**Decision:** Notifications are a first-class module. SignalR is only a delivery adapter (`IRealtimeNotificationSender`). Hangfire is only a background adapter (`IBackgroundJobQueue`). The source of truth is the `notifications` table (read/unread + delivery status + unique idempotency key).

**Lifecycle:** business use case → `INotificationPublisher` → `SaveChanges` → enqueue delivery → SignalR (+ optional email).

**Reason:** Offline users still have an inbox; retries and failures are durable; other modules (Product, Order, Payment) can publish without referencing hubs or Hangfire.

**Alternatives rejected:** firing SignalR from the HTTP request; treating Hangfire as the notification store; dispatching domain events in this sprint (still raised, still not dispatched).

---

## ADR-018: Money — `decimal` + ISO 4217 currency

**Decision:** Product and variant prices are `decimal` with precision 18, scale 2, plus a 3-letter currency code. Default marketplace currency is `EGP`. No `float`/`double`. No money value-object type in EF (two columns) until checkout needs a shared `Money` primitive.

**Reason:** Handmade catalog needs exact minor units without a pricing engine. Variants can override the product base price. Future orders must snapshot amount+currency at purchase time.


