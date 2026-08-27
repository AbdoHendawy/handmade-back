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

## ADR-016: Welcome email — provider + flag idempotency

**Decision:** `IEmailSender` with `Email:Provider` selecting Development `ConsoleEmailSender` or production `SmtpEmailSender` (MailKit). Identity welcome-mail idempotency via `User.WelcomeEmailSent`. Seller (and future modules) persist an in-app `Notification` then send email from the Hangfire delivery job. No Outbox table yet. SMTP misconfiguration fails at startup; Production never silently falls back to console.

**Reason:** Avoid broker complexity; Outbox can be added later without redesigning Identity or Notification ports.

---

## ADR-017: Notifications — persist first, Hangfire, SignalR last

**Decision:** Notifications are a first-class module. SignalR is only a delivery adapter (`IRealtimeNotificationSender`). Hangfire is only a background adapter (`IBackgroundJobQueue`). The source of truth is the `notifications` table (read/unread + delivery status + unique idempotency key).

**Lifecycle:** business use case → commit → `INotificationPublisher` (or Identity/Seller wrapper) → `SaveChanges` → enqueue delivery → SignalR (+ optional Seller email). There is no public client `POST /notifications`.

**Reason:** Offline users still have an inbox; retries and failures are durable; other modules (Product, Order, Payment) can publish without referencing hubs or Hangfire.

**Alternatives rejected:** firing SignalR from the HTTP request; treating Hangfire as the notification store; dispatching domain events in this sprint (still raised, still not dispatched); a generic public create endpoint.

---

## ADR-018: Money — `decimal` + ISO 4217 currency

**Decision:** Product and variant prices are `decimal` with precision 18, scale 2, plus a 3-letter currency code. Default marketplace currency is `EGP`. No `float`/`double`. No money value-object type in EF (two columns) until checkout needs a shared `Money` primitive.

**Reason:** Handmade catalog needs exact minor units without a pricing engine. Variants can override the product base price. Future orders must snapshot amount+currency at purchase time.

---

## ADR-019: Cart price snapshot is not the order price

**Decision:** Cart stores `priceSnapshot` + `currency` on each line for display and `priceChanged` detection. `GET /cart` always shows the **live** Catalog price. Checkout/Order must re-read Catalog and persist an immutable commercial snapshot. Cart does not reserve inventory.

**Reason:** The cart is mutable shopping intent. Product price, status, and seller state can change while items sit in a cart. Treating cart data as the source of truth would create incorrect orders.

**Also decided:**

- One cart per user (`carts.user_id` unique), created lazily
- Line identity is `(ProductId, VariantId?)`
- Multi-seller carts are allowed; order splitting belongs to Checkout
- Purchasability lives in Catalog (`IProductPurchaseQuery`): Published + active seller + variant rules
- Concurrent add/update uses unique indexes, `xmin` on `cart_items`, and a single retry — not a second unit of work

---

## ADR-020: Checkout commits with one SaveChanges and classified xmin

**Decision:** Checkout uses `IApplicationDbContext.SaveChangesAsync` as the only persistence boundary. There is no `OrderPersistence`, `IUnitOfWork`, or `BeginTransaction` wrapper.

On `DbUpdateConcurrencyException`, checkout inspects `ex.Entries`:

- Retry once, with a full graph rebuild, only when **every** conflicting entry is a `Product` or `ProductVariant` this checkout mutated for inventory
- That same inventory xmin on attempt 2 maps to Orders `concurrency_conflict`
- Conflicts on Cart, OrderGroup, Order, OrderItem, or unrelated entities are rethrown; GlobalExceptionHandler already returns 409

**Reason:** Cart/Catalog persistence helpers map *all* xmin failures to a module conflict code. Copying that for checkout would hide retry classification and could convert unrelated races into Orders `concurrency_conflict`.

**Also decided:**

- One checkout → one OrderGroup → one Order per `SellerProfile`
- Live Catalog price/stock via `IProductPurchaseQuery` / `IProductInventory`; cart `priceSnapshot` is never the order price
- Notifications run only after successful SaveChanges; publisher failure still returns 201
- `OrderItem` → Product/Variant FKs are Restrict so ordered catalog rows are not hard-deleted
- Current MVP payment method is Cash on Delivery, snapshotted on `OrderGroup` only. Online payment is a separate future module/sprint; individual seller `Order` rows do not have `PaymentMethod`.

---

## ADR-021: Order owns seller lifecycle; OrderGroup stays Placed

**Decision:** After checkout, fulfillment status lives on each seller `Order`. `OrderGroupStatus` remains `Placed` only. There is no OrderGroup confirm/prepare/ship/deliver/cancel API and no automatic group status roll-up.

Valid `Order` flow: `Placed → Confirmed → Preparing → Shipped → Delivered`. Cancellation is `Placed → Cancelled` only. Invalid transitions throw `ConflictException` / `invalid_status_transition` (409).

**Also decided:**

- Seller (`SellerActive`) owns Confirm, Prepare, Ship, Deliver, and Cancel for its own `Order`. Cross-seller and unknown ids return 404.
- Customer may cancel only their own `Order` while it is `Placed`. Customer cannot confirm, prepare, ship, or deliver.
- Sibling Orders in one OrderGroup progress independently.
- Cash on Delivery remains the only `PaymentMethod`. No Payment module, `PaymentStatus`, `PaymentTransaction`, refund, or online gateway.
- Lifecycle notifications (`order.confirmed`, `order.preparing`, `order.shipped`, `order.delivered`, `order.cancelled`) publish through `IOrderNotificationService` after successful `SaveChangesAsync`. Publisher failure is logged and does not roll back the status change.
- Confirm, Prepare, Ship, and Deliver rely on `orders.xmin` and do not use inventory retry. Uncaught `DbUpdateConcurrencyException` maps to 409 `concurrency_conflict`. Cancel of a Placed Order restores Catalog stock in the same `SaveChangesAsync` and classifies inventory xmin with `CheckoutConcurrency.Decide` (retry once). `Order.Cancel()` remains status-only; Catalog owns the stock mutation via `IProductInventory.IncrementAsync`.
- Domain events remain Raise-only. No MediatR, event bus, generic repository, `IUnitOfWork`, or `BeginTransaction`.

**Reason:** Multi-seller checkout already splits one OrderGroup into one Order per seller. Letting a seller mutate OrderGroup status would couple independent shops. Payment state is not OrderStatus.

**Alternatives rejected:** synchronizing OrderGroup from child Orders; allowing cancel after confirmation; a Payment module in this sprint.

---

## ADR-022: Cancelled Placed Orders restore inventory

**Status:** Accepted

**Context:** Checkout decrements Catalog stock through `IProductInventory.DecrementAsync`. Customer and seller cancel APIs already exist and remain `Placed → Cancelled` only. Without restoration, a cancelled Order would permanently consume that stock. A Product can gain or lose variants after checkout, so restoration must not infer Product vs Variant from the live variant count. The restore target is the identity already persisted on `OrderItem`.

**Decision:** On successful `Placed → Cancelled`, restore stock for every `OrderItem` on that Order:

- `VariantId == null` → `Product.IncrementStock(quantity)`
- `VariantId != null` → that `ProductVariant.IncrementStock(quantity)`

Application cancellation calls `IProductInventory.IncrementAsync` after `Order.Cancel()`. `Order.Cancel()` remains status-only. Domain Orders do not own inventory. Catalog owns the mutation. One `SaveChangesAsync` persists Order + stock. Notifications run after a successful save; publisher failure is still swallowed. Inventory-only Product/ProductVariant `xmin` conflicts retry once (`CheckoutConcurrency.Decide`, MaxAttempts = 2). There is no explicit transaction, `IUnitOfWork`, stock ledger, reservation table, or `StockRestored` flag. No database migration.

**Consequences:**

- Cancelled stock becomes available again.
- Order status and inventory persist atomically under existing EF/Npgsql `xmin`.
- Duplicate concurrent cancel cannot restore twice (`orders.xmin` / `invalid_status_transition` on reload).
- Checkout architecture and cancel routes stay unchanged; `OrderResponse` exposes no stock fields.
- Cancellation now participates in inventory concurrency.
- Notification-after-commit keeps the existing crash window.
- There is no historical stock-movement ledger.

**Alternatives rejected:**

1. Save status, then restore stock — split-brain if the second write fails.
2. Reservation model — would change checkout and add a new inventory model.
3. `StockMovement` / ledger — out of Sprint 9 scope; may be considered later.
4. Do not restock on cancellation — leaves cancelled stock consumed.

---


