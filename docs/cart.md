# Cart

Folder slice for the buyer's mutable shopping intent. Catalog remains the source of truth for product data. Checkout stores an immutable commercial snapshot on OrderGroup / Order / OrderItem. See [orders.md](orders.md).

```
User → Cart → CartItem → Product / ProductVariant
```

A user has **at most one cart**. Carts are created lazily on the first successful add. Guest carts are not supported.

## Lifecycle

```
No row
  → POST /cart/items (lazy create)
  → GET /cart (live prices, availability)
  → PUT /cart/items/{productId} (set quantity)
  → DELETE /cart/items/{productId}
  → DELETE /cart (clear items, keep cart row)
```

Checkout revalidates product, seller, price, and stock before creating an order. Cart `priceSnapshot` is display-only and must not become the order price. Successful checkout deletes cart items and keeps the cart row.

## Ownership

All operations use `ICurrentUser.UserId`. Clients cannot submit another user's id. Cross-user access is impossible because carts are loaded only by the authenticated user.

## Line identity

A cart line is `(ProductId, VariantId?)`.

- Products **with** variants require `variantId`.
- Products **without** variants must omit `variantId`.
- Adding the same product/variant again increments quantity (max `99` per line).

Multi-seller carts are allowed. Checkout splits them into one Order per seller.

## Price

| Moment | Source |
|---|---|
| Add / update | Current product or variant price, stored as `priceSnapshot` |
| `GET /cart` | Live Catalog price; `priceChanged` when live ≠ snapshot |
| Checkout | Always re-read Catalog; cart `priceSnapshot` is never the order price |

Money follows ADR-018: `decimal(18,2)` + ISO 4217 currency (default EGP). Mixed currencies in one cart are rejected. No tax, shipping, coupons, or promotions.

`CartCalculator` is the only total source:

- line `subtotal` = live `unitPrice × quantity`
- cart `subtotal` = sum of all line subtotals
- cart `total` = sum of **available** line subtotals
- `itemCount` = sum of quantities
- `distinctItemCount` = number of lines

## Inventory

Catalog owns stock on `Product` / `ProductVariant`. Cart does not reserve, decrement, or restore inventory. Quantity is only an intention.

Checkout decrements Catalog stock when it places an order. If that Order is later cancelled while **Placed**, Application cancellation restores the stock that Order took. Restoration is not a Cart responsibility. See [orders.md](orders.md).

## Purchasability

Catalog-owned `IProductPurchaseQuery` (Cart does not duplicate product rules):

Add/update require:

- Product exists
- `ProductStatus.Published`
- `SellerProfile.IsActive`
- Valid price
- Variant rules above

`GET /cart` does **not** delete stale lines. Unpublished, archived, or suspended-seller items return `isAvailable: false` and an `unavailabilityReason`.

Public catalog listing is unchanged (Published only; it still does not hide suspended sellers).

## Concurrency

- Unique `carts.user_id` — one cart per user
- Filtered unique indexes on cart lines
- PostgreSQL `xmin` rowversion on `cart_items`
- Unique/xmin conflicts retry **once** so concurrent adds become quantity 2, not 409

Product and variant inventory rows use PostgreSQL `xmin`; checkout (and Placed cancellation restore) classify those conflicts. Cart itself only retries `cart_items` unique/xmin races.

## Endpoints

All require `[Authorize]`. User id is never accepted from the client.

| Method | Path | Status |
|---|---|---|
| GET | `/api/v1/cart` | 200 (empty cart if none) |
| POST | `/api/v1/cart/items` | 200 cart (upsert) |
| PUT | `/api/v1/cart/items/{productId}?variantId=` | 200 cart |
| DELETE | `/api/v1/cart/items/{productId}?variantId=` | 204; missing line 404 |
| DELETE | `/api/v1/cart` | 204 (idempotent) |

## Out of scope

Notifications, Hangfire, domain-event dispatch, Payment, Shipping, coupons, tax, inventory reservation.

Checkout and Placed-order cancellation live in Orders, not Cart. See [orders.md](orders.md).
