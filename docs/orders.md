# Orders

Folder slice for commercial checkout history. Cart stays mutable shopping intent. Catalog stays the source of live price, purchasability, and stock.

```
Cart
  → live Catalog revalidation
  → one OrderGroup
  → one Order per SellerProfile
  → OrderItem snapshots
```

Payment, fulfillment, coupons, tax, shipping fees, guest checkout, address book, and admin order APIs are out of scope.

## Aggregates

`OrderGroup` and `Order` are **separate** aggregate roots. `OrderItem` is a child of `Order`. There is no `OrderGroup.Orders` EF-owned collection.

| Entity | Identity | Number |
|---|---|---|
| OrderGroup | UUIDv7 PK | PostgreSQL `bigint` identity |
| Order | UUIDv7 PK | PostgreSQL `bigint` identity |
| OrderItem | UUIDv7 PK | — |

`Number` is assigned by the database. Do not set it in domain code.

## Status

`OrderStatus` and `OrderGroupStatus` contain only `Placed = 0`. Paid / shipped / cancelled belong to later sprints.

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

## Inventory

Stock is Catalog-owned (`IProductInventory.DecrementAsync`, no SaveChanges). Default stock is 0. Orders do not implement stock rules and do not own `insufficient_stock`.

A published product that has been ordered **cannot be hard-deleted** while `order_items` rows exist (`Restrict`). Archive remains the public-catalog removal path. Restore/archive/delete lifecycle is unchanged.

## APIs

| Method | Path | Auth |
|---|---|---|
| POST | `/api/v1/checkout` | authenticated customer |
| GET | `/api/v1/orders` | owner; paged OrderGroups |
| GET | `/api/v1/orders/{orderGroupId}` | owner; 404 otherwise |
| GET | `/api/v1/seller/orders` | `SellerActive`; paged Orders |
| GET | `/api/v1/seller/orders/{orderId}` | owner seller; 404 otherwise |

`POST /checkout` returns **201** with `CreatedAtAction` → `GET /api/v1/orders/{orderGroupId}`. Never 201 before SaveChanges succeeds.

## Error ownership

Orders: `cart_empty`, `line_not_purchasable`, `invalid_price`, `currency_mismatch`, `order_not_found`, `concurrency_conflict`.

Catalog: `insufficient_stock` and purchase codes (`product_not_purchasable`, `seller_not_active`, `variant_required`, …).

## Notifications

After commit:

- Customer: `order.placed` / idempotency `order.placed:{orderGroupId}`
- Seller (`SellerProfile.UserId`): `order.received` / idempotency `order.received:{orderId}`

Domain events `OrderGroupPlaced` / `OrderPlaced` are **raised only**, not dispatched.

## Tables

- `order_groups` — FK `customer_id` → `users` Restrict; unique identity `number`; `xmin`
- `orders` — FK `order_group_id` Cascade; FKs customer/seller Restrict; unique identity `number`; `xmin`
- `order_items` — FK `order_id` Cascade; FKs product/variant/seller Restrict; `xmin`
