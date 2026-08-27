# Seller module

Sprint 3 module documentation for Handmade Backend.

Identity answers **who you are**. Seller answers **whether you are a seller and what the seller business is**.

## Boundaries

Seller owns:

- Seller applications and their review workflow
- Seller profiles and profile status (Active / Suspended / Deactivated)
- Seller business rules, audit fields, and seller notifications

Seller does **not** own User, passwords, JWT, refresh tokens, Google login, or role definitions. Those remain in Identity.

A User is not a Seller. A Seller profile is a business record owned by a User (`UserId`, unique).

## Domain model

| Entity | Purpose |
|---|---|
| `SellerApplication` | Application history. Status: Pending, Approved, Rejected, Cancelled |
| `SellerProfile` | One per user, created only from an approved application. Status: Active, Suspended, Deactivated |

Application status is never overwritten to represent later profile suspension. An approved application stays Approved.

## Workflow

```
Customer
   │ Apply
   ▼
Seller Application (Pending)
   ├── Approve → SellerProfile (Active) + Seller role
   ├── Reject  → history retained; user may reapply
   └── Cancel  → own pending only; user may reapply
```

```
SellerProfile
   Active ──suspend──► Suspended ──reactivate──► Active
```

`Deactivated` is reserved for a later admin operation. There is no deactivate API in this sprint.

## Application rules

- Only authenticated users can apply. `UserId` always comes from `ICurrentUser`.
- A user cannot submit another application while one is Pending (domain check + partial unique index).
- An approved seller (existing `SellerProfile`) cannot apply again.
- Rejected or cancelled applications may be followed by a new application.
- Only Pending applications can be approved, rejected, or cancelled.
- A reviewer cannot approve or reject their own application.
- A `SellerProfile` can exist only once per user (`seller_profiles.user_id` UNIQUE).
- A profile can only be created from an approved application.

## Approval transaction

Identity and Seller share `HandmadeDbContext`. Approval is one unit of work:

1. Load pending application
2. `Approve` (domain)
3. `SellerProfile.CreateFromApproval`
4. `IIdentityRoleService.AssignRole(userId, Seller)` — mutates tracked `User`, does **not** save
5. Single `SaveChangesAsync`
6. Best-effort email (failure does not roll back)

Concurrent approve/reject uses PostgreSQL `xmin` as a concurrency token. The second writer receives 409 Conflict.

## Role vs status

Approval assigns the Identity **Seller** role. Suspension does **not** remove it.

Seller-only business operations must use policy `SellerActive`:

- Authenticated
- `SellerProfile` exists for the current user
- `Status == Active`

JWT `role=Seller` is not sufficient (tokens stay valid after suspend until expiry). After approval, clients should refresh so the access token includes the Seller role. Own profile GET/PUT authorize by `UserId` and work immediately without a refresh.

## Events

Raised on aggregates (not dispatched; no MediatR/Outbox in this sprint):

| Event | Payload |
|---|---|
| `SellerApplicationSubmitted` | ApplicationId, UserId |
| `SellerApplicationApproved` | ApplicationId, SellerId, UserId, ApprovedBy, ApprovedAt |
| `SellerApplicationRejected` | ApplicationId, UserId, RejectedBy |
| `SellerSuspended` | SellerId, UserId, SuspendedBy |
| `SellerReactivated` | SellerId, UserId |

Notifications persist an in-app row then enqueue Hangfire delivery (SignalR + email). See [notifications.md](notifications.md).

## Endpoints

Customer / seller (`[Authorize]`, UserId from token):

| Method | Route |
|---|---|
| POST | `/api/v1/seller/applications` |
| GET | `/api/v1/seller/applications/me` |
| POST | `/api/v1/seller/applications/{id}/cancel` |
| GET | `/api/v1/seller/profile` |
| PUT | `/api/v1/seller/profile` |
| GET | `/api/v1/seller/access` (`SellerActive` policy smoke check) |

Admin (`[Authorize(Roles = Admin)]`):

| Method | Route |
|---|---|
| GET | `/api/v1/admin/seller-applications?status=&page=&pageSize=` |
| GET | `/api/v1/admin/seller-applications/{id}` |
| POST | `/api/v1/admin/seller-applications/{id}/approve` |
| POST | `/api/v1/admin/seller-applications/{id}/reject` |
| GET | `/api/v1/admin/sellers?status=&page=&pageSize=` |
| GET | `/api/v1/admin/sellers/{id}` |
| POST | `/api/v1/admin/sellers/{id}/suspend` |
| POST | `/api/v1/admin/sellers/{id}/reactivate` |

There is no generic status update endpoint.

## Authorization matrix

| Endpoint class | Anonymous | Customer | Seller | Admin |
|---|---|---|---|---|
| Apply / my applications | 401 | yes (rules apply) | yes (rules apply) | yes (cannot approve self) |
| Own profile GET/PUT | 401 | 404 if no profile | own profile | own profile if present |
| `GET /seller/access` | 401 | 403 | 200 if Active; 403 if Suspended | 403 unless they have an Active profile |
| Admin seller APIs | 401 | 403 | 403 | yes |

## Database

Tables: `seller_applications`, `seller_profiles`.

- FKs to `users` use Restrict
- `seller_profiles.user_id` UNIQUE
- Partial unique: one Pending application per user
- `xmin` mapped as EF rowversion (PostgreSQL system column; not a real user column)

Future Product entities should reference `SellerProfile.Id`, not `UserId`. See [catalog.md](catalog.md).

## Intentionally out of scope

Inventory, orders, payments, storefront, logo/cover upload, tax/compliance fields, Outbox.
