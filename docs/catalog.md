# Catalog & Products

Folder slice for marketplace offerings. Identity answers who the user is. Seller answers which business they own. Catalog answers what that business sells.

```
User → SellerProfile → Product
                          ├── ProductImage
                          ├── ProductVariant
                          └── Category
```

`Product.SellerId` is **`SellerProfile.Id`**, never `UserId`.

## Lifecycle

```
Draft → PendingReview → Published → Archived → Draft
                 ↘ Rejected ↗ (edit + submit)
```

Operations (no generic status PUT):

| Actor | Operations |
|---|---|
| Active seller | create, edit, delete draft/rejected, submit, cancel-submit, archive, restore, images, variants |
| Admin | approve, reject, archive, categories |
| Public | published catalog only |

**Editing policy (this sprint):** Draft, Rejected, and Published are editable. PendingReview must `cancel-submit` first. Archived must `restore` first. Published edits stay Published (future re-moderation can be added without changing the aggregate).

**Delete:** only Draft or Rejected. Published uses Archive so order/history IDs stay stable.

Submit requires name, description (≥ 20 chars), active category, price ≥ 0, and at least one image. Variants are optional.

## Money

`decimal` amount, 2 fractional digits, ISO 4217 `currency` (default **EGP**). No `float`/`double`. No promotions in this sprint. Product has a base price; variants may override with their own price (same currency recommended). Future cart/orders must **snapshot** name/price; they must not rely on live product rows.

## Images

`ProductImage` is a child of Product (`StorageKey`, `Url`, `SortOrder`, `IsPrimary`). At most one primary image (partial unique index). Binary upload uses existing `IFileStorage`; this sprint stores **metadata only** because the current provider is `NotConfiguredFileStorage`. Swap in S3/R2 later without changing the domain.

EF does **not** own `Images` / `Variants` as Product collections. They are independent tables loaded in batch for reads. Product has no `xmin` rowversion; SellerProfile does. Child create/update therefore does not mark Product (or the related seller row) as modified.

Future attributes (color/size/material) can be added as columns or a small `ProductVariantOption` table keyed by `ProductVariant.Id`. No EAV in this sprint. `SKU` is globally unique.

## Public catalog

`GET /api/v1/catalog/products` and `/{slug}` query `Status == Published` in SQL. Sort whitelist: `newest`, `priceAsc`, `priceDesc`. Search is PostgreSQL `Contains` on name/description (not Elasticsearch).

Inactive categories stay linked to existing products; they cannot be used for new products.

## Events (raised, not dispatched)

`ProductCreated`, `ProductSubmitted`, `ProductApproved`, `ProductRejected`, `ProductArchived`, `ProductRestored`, `CategoryCreated`, `CategoryActivated`, `CategoryDeactivated`.

Approve/reject/submit also persist in-app notifications.

## Endpoints

Public: `/api/v1/catalog/categories`, `/api/v1/catalog/products`.

Seller (`SellerActive`): `/api/v1/seller/products` and nested `/images`, `/variants`, `/submit`, `/cancel-submit`, `/archive`, `/restore`.

Admin: `/api/v1/admin/categories`, `/api/v1/admin/products`.

## Authorization

| Operation | Customer | Seller | Admin |
|---|---|---|---|
| Browse published | yes | yes | yes |
| Create / edit own | no | yes (Active profile) | only if they also have an Active seller profile |
| Approve / reject | no | no | yes |
| Manage categories | no | no | yes |

Cross-seller access returns **404**. Admin is not a seller.

## Future

Inventory hangs off `ProductVariant`. Cart/orders reference Product/Variant **IDs**, not slugs. Reviews reference Product. Search/cache can subscribe to `ProductApproved` later. Do not add Redis/OpenSearch in this module.

Rejection currently stores the latest reason on Product (`RejectionReason`, `ReviewedBy`, `ReviewedAt`). A `ProductModerationEvent` table can be added later if resubmit history must be fully auditable.
