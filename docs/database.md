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

Identity tables from Sprint 2 (`users`, `roles`, `user_roles`, `external_logins`, `refresh_tokens`).

Seller tables from Sprint 3:

- `seller_applications` — application history; partial unique index one Pending per user; FKs to `users` (Restrict)
- `seller_profiles` — at most one per user (`user_id` UNIQUE); FK to the source application (Restrict)

Notification tables from Sprint 4.5:

- `notifications` — per-user inbox; unique `idempotency_key`; FK to `users` (Cascade)

Hangfire tables live in schema `hangfire` (created by Hangfire.PostgreSql, not EF).

Catalog tables from Sprint 4:

- `categories` — unique `slug`; self-FK `parent_category_id` Restrict; index on parent and `is_active`
- `products` — unique `slug`; FK `seller_id` → `seller_profiles` Restrict; FK `category_id` → `categories` Restrict; indexes on seller, category, status, created_at, `(status, published_at)` for public lists
- `product_images` — FK cascade from product; partial unique one primary image per product
- `product_variants` — unique `sku`; FK cascade from product

Optimistic concurrency uses PostgreSQL `xmin` (EF rowversion) on categories. Product lifecycle races (double approve) fail via invalid state transitions; child collections (images/variants) do not use `xmin` because it conflicts with aggregate updates.

Cart tables from Sprint 6:

- `carts` — at most one per user (`user_id` UNIQUE); FK to `users` Restrict
- `cart_items` — FK cascade from cart; FKs to `products` / `product_variants` Restrict; filtered unique indexes one line per product (no variant) and one line per product+variant; `xmin` rowversion; quantity CHECKs `> 0` and `<= 99`

Order tables from Sprint 7 (`AddOrderModule`), plus `order_groups.payment_method` (`AddOrderGroupPaymentMethod`). Sprint 8 added no new columns: lifecycle statuses reuse `orders.status` (`varchar(32)` string conversion). There is no CHECK that restricts `orders.status` to `Placed`. No PaymentStatus, PaymentTransaction, gateway, or lifecycle timestamp columns.

- `order_groups` — identity `number`; `status` (`Placed` only); `payment_method` (CashOnDelivery snapshot); customer/delivery snapshot fields; FK `customer_id` → `users` Restrict; `xmin`
- `orders` — identity `number`; `status` (Placed / Confirmed / Preparing / Shipped / Delivered / Cancelled); seller/customer/delivery snapshot fields; FK `order_group_id` Cascade; FKs customer/seller Restrict; `xmin`
- `order_items` — product/variant/seller snapshot fields; FK `order_id` Cascade; FKs product/variant/seller Restrict; `xmin`

Optimistic concurrency uses PostgreSQL `xmin` on products and product_variants for **checkout inventory retry**. Status writes on `orders` also use `xmin`; stale seller/customer updates rethrow `DbUpdateConcurrencyException` (GlobalExceptionHandler 409 `concurrency_conflict`). That path is not the checkout inventory retry.

See [seller.md](seller.md), [notifications.md](notifications.md), [catalog.md](catalog.md), [cart.md](cart.md), and [orders.md](orders.md).
