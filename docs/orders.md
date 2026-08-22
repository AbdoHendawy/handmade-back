# Orders

Folder slice for commercial checkout history. Cart stays mutable shopping intent. Catalog stays the source of live price, purchasability, and stock.

```
Cart
  → live Catalog revalidation
  → one OrderGroup (PaymentMethod = CashOnDelivery)
  → one Order per SellerProfile
    → OrderItem snapshots
```

Online payment is intentionally out of scope. The current MVP records Cash on Delivery only. There is no payment processing during checkout. Coupons, tax, shipping fees, guest checkout, address book, and admin order APIs remain out of scope.

## Aggregates

`OrderGroup` and `Order` are **separate** aggregate roots. `OrderItem` is a child of `Order`. There is no `OrderGroup.Orders` EF-owned collection.

| Entity | Identity | Number |
|---|---|---|
| OrderGroup | UUIDv7 PK | PostgreSQL `bigint` identity |
| Order | UUIDv7 PK | PostgreSQL `bigint` identity |
| OrderItem | UUIDv7 PK | — |

`Number` is assigned by the database. Do not set it in domain code.

## Payment method

`PaymentMethod` belongs to `OrderGroup`, not to individual seller `Order` rows. One checkout selects one method for the whole group.

Current value:

- `CashOnDelivery = 0`

Checkout does not accept a client-supplied payment method. The server always persists `PaymentMethod.CashOnDelivery`. No payment is collected, authorized, or captured during checkout.

There is no `PaymentStatus`, `PaymentTransaction`, refund, webhook, or Payment module. Online card / wallet / Paymob / Stripe remain a separate future sprint. Do not add speculative enum values until that sprint.

## Order lifecycle

Lifecycle belongs to **Order**, not OrderGroup.

`OrderStatus`:

| Value | Numeric |
|---|---|
| Placed | 0 |
| Confirmed | 1 |
| Preparing | 2 |
| Shipped | 3 |
| Delivered | 4 |
| Cancelled | 5 |

Valid flow:

```
Placed → Confirmed → Preparing → Shipped → Delivered
```

Cancellation:

```
Placed → Cancelled
```

Cancellation after confirmation is not allowed. Status has no public setter. Domain methods `Confirm()`, `Prepare()`, `Ship()`, `Deliver()`, and `Cancel()` own the transitions.

Invalid transitions throw `ConflictException` with `invalid_status_transition` (**409 Conflict**).

## OrderGroup status

`OrderGroupStatus` contains only `Placed = 0`.

- There are no `Confirm` / `Prepare` / `Ship` / `Deliver` / `Cancel` methods on `OrderGroup`.
- Seller and customer mutations never change `OrderGroup.Status`.
- Sibling seller `Order` rows in the same group progress independently (for example Seller A `Shipped` and Seller B `Preparing`).
- There is no automatic OrderGroup status roll-up.

## Responsibility

| Actor | Allowed |
|---|---|
| Seller (`SellerActive`) | Confirm, Prepare, Ship, Deliver, Cancel own `Order` |
| Customer (authenticated) | Cancel own `Order` only while `Placed` |

The customer cannot confirm, prepare, ship, or deliver. Those routes do not exist on the customer API. Cross-owner access returns **404** `order_not_found` (no existence leak).

## Snapshots

Orders are immutable commercial history. After place:

- Catalog name, price, image, variant name, SKU, and seller name changes do **not** rewrite order rows
- `OrderItem.UnitPrice` comes from live `IProductPurchaseQuery`, never from cart `priceSnapshot`
- SKU: variant line → `ProductVariant.Sku`; non-variant line → `SkuSnapshot = null`
- `ProductId` / `VariantId` are traceability only (`Restrict` FKs)

## Delivery

Checkout body is delivery only. Identity comes from `ICurrentUser`. Delivery fields are copied onto both the group and each order. There is no Address entity or address book.

## Totals

`OrderCalculator` is the only totals source (`CatalogMoney` decimal 18,2):

- LineTotal = UnitPrice × Quantity
- Order subtotal = sum of line totals; order total = subtotal
- Group subtotal = sum of order subtotals; group total = sum of order totals

## Checkout persistence

`ICheckoutService.PlaceAsync` is the use case. **Do not create `OrderPersistence`.**

1. Validate delivery
2. `ClearTrackedEntities()`
3. Load cart + live Catalog + tracked inventory
4. Reject the whole checkout if any line is empty/unpurchasable/out of stock/currency-mismatched (nothing persisted, cart unchanged)
5. Build a new OrderGroup graph (new UUIDv7s), decrement Catalog stock, delete cart items (keep the cart row)
6. One `_db.SaveChangesAsync()` per attempt
7. On `DbUpdateConcurrencyException`, inspect `ex.Entries`:
   - Retry (attempt 1 only) if **every** conflicting entry is a `Product` or `ProductVariant` this checkout mutated
   - That case on attempt 2 → Orders `concurrency_conflict`
   - Cart / Order* / unrelated entities → **rethrow** (GlobalExceptionHandler 409, not Orders `concurrency_conflict`)
8. Attempt 2 is a full rebuild (new IDs, reload cart, live Catalog, new graph). Never a third attempt
9. After successful SaveChanges, `IOrderNotificationService` publishes. Notification failure still returns **201**

## Status-write concurrency

`orders` and `order_groups` have PostgreSQL `xmin` as an EF rowversion.

Seller and customer status writes load a tracked `Order`, call the domain method, and `SaveChangesAsync` once. They do **not** use checkout inventory retry and do **not** catch `DbUpdateConcurrencyException`. A stale `xmin` becomes `DbUpdateConcurrencyException`; `GlobalExceptionHandler` maps it to **409** `concurrency_conflict`.

This is separate from checkout inventory xmin retry.

## Inventory

Stock is Catalog-owned (`IProductInventory.DecrementAsync`, no SaveChanges). Default stock is 0. Orders do not implement stock rules and do not own `insufficient_stock`.

A published product that has been ordered **cannot be hard-deleted** while `order_items` rows exist (`Restrict`). Archive remains the public-catalog removal path. Restore/archive/delete lifecycle is unchanged.

## APIs

| Method | Path | Auth |
|---|---|---|
| POST | `/api/v1/checkout` | authenticated customer |
| GET | `/api/v1/orders` | owner; paged OrderGroups |
| GET | `/api/v1/orders/{orderGroupId}` | owner; 404 otherwise |
| POST | `/api/v1/orders/{orderId}/cancel` | owner; Placed only; 404 otherwise |
| GET | `/api/v1/seller/orders` | `SellerActive`; paged Orders |
| GET | `/api/v1/seller/orders/{orderId}` | owner seller; 404 otherwise |
| POST | `/api/v1/seller/orders/{orderId}/confirm` | `SellerActive`; owner |
| POST | `/api/v1/seller/orders/{orderId}/prepare` | `SellerActive`; owner |
| POST | `/api/v1/seller/orders/{orderId}/ship` | `SellerActive`; owner |
| POST | `/api/v1/seller/orders/{orderId}/deliver` | `SellerActive`; owner |
| POST | `/api/v1/seller/orders/{orderId}/cancel` | `SellerActive`; owner |

`POST /checkout` returns **201** with `CreatedAtAction` → `GET /api/v1/orders/{orderGroupId}`. Never 201 before SaveChanges succeeds.

Status mutations return **200** and the updated `OrderResponse`. There is no PATCH/PUT status endpoint.

## Error ownership

Orders: `cart_empty`, `line_not_purchasable`, `invalid_price`, `currency_mismatch`, `order_not_found`, `concurrency_conflict`, `invalid_status_transition`.

Catalog: `insufficient_stock` and purchase codes (`product_not_purchasable`, `seller_not_active`, `variant_required`, …).

## Notifications

`IOrderNotificationService` publishes **after** successful `SaveChangesAsync`. Invalid transitions and 404s do not notify. Publisher failure is logged and swallowed; the HTTP status still succeeds.

| Type | Recipient | Idempotency |
|---|---|---|
| `order.placed` | Customer | `order.placed:{orderGroupId}` |
| `order.received` | Seller (`SellerProfile.UserId`) | `order.received:{orderId}` |
| `order.confirmed` | Customer | `order.confirmed:{orderId}` |
| `order.preparing` | Customer | `order.preparing:{orderId}` |
| `order.shipped` | Customer | `order.shipped:{orderId}` |
| `order.delivered` | Customer | `order.delivered:{orderId}` |
| `order.cancelled` | Customer when the seller cancels; seller when the customer cancels | `order.cancelled:{orderId}` |

There is no `order.paid`. Domain events (`OrderGroupPlaced`, `OrderPlaced`, `OrderConfirmed`, `OrderPreparing`, `OrderShipped`, `OrderDelivered`, `OrderCancelled`) are **raised only**, not dispatched. No MediatR or event bus.

## Tables

- `order_groups` — FK `customer_id` → `users` Restrict; unique identity `number`; `status` string (`Placed`); `payment_method`; `xmin`
- `orders` — FK `order_group_id` Cascade; FKs customer/seller Restrict; unique identity `number`; `status` string; `xmin`
- `order_items` — FK `order_id` Cascade; FKs product/variant/seller Restrict; `xmin`

Status is stored as `varchar(32)` via EF string conversion. There is no database CHECK that restricts `orders.status` to `Placed`.
