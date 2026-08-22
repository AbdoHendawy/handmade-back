# Notifications

Reusable persistent notifications for every module. SignalR is a **delivery mechanism**, not the domain.

```
Business action
      │
      ▼
Database transaction
      │
      ▼
COMMIT
      │
      ▼
INotificationPublisher / ISellerNotificationService / IIdentityNotificationService
      │
      ▼
PostgreSQL `notifications` (source of truth)
      │
      ▼
Hangfire job (`INotificationDeliveryService`)
      │
      ▼
SignalR hub  →  connected client(s)
      │
      ▼
Optional email channel (`IEmailSender`) for Seller types
```

Clients that were offline still see the notification via `GET /api/v1/notifications` after they reconnect.

## Boundaries

| Layer | Owns |
|---|---|
| Domain | `Notification` aggregate, delivery status, idempotency key, read state |
| Application | `INotificationPublisher`, inbox queries, delivery use case, Identity/Seller publishers, ports |
| Infrastructure | EF mapping, Hangfire PostgreSQL storage + enqueue adapter |
| Api | `NotificationHub`, JWT-for-hubs, SignalR sender, REST inbox |

Domain and Application **must not** reference Hangfire or SignalR. Business modules depend on `INotificationPublisher` (or a thin module wrapper such as `ISellerNotificationService`). They must not depend on `IHubContext`, hubs, or groups.

## Creating a notification (other modules)

Trusted application workflows only. There is **no** public `POST /api/v1/notifications`.

```csharp
await _publisher.PublishToUserAsync(new CreateUserNotificationRequest(
    userId,
    type: "order.paid",
    title: "Payment received",
    body: "Order #123 is paid.",
    idempotencyKey: $"order.paid:{orderId:D}",
    dataJson: """{"orderId":"..."}"""));
```

`PublishToRoleAsync` fans out one persisted row per user in that role (`idempotencyPrefix:{userId}`).

Re-publishing the same `idempotencyKey` returns the existing row and does **not** enqueue a second job. Uniqueness is enforced with a unique index plus `DbUpdateException` recovery (not check-then-create alone).

Publisher / module wrappers are called **after** the business `SaveChanges`. Failures to persist or enqueue a notification are logged and must not roll back the business operation.

## Delivery

Hangfire runs `INotificationDeliveryService.DeliverAsync`:

1. Skip if already `Delivered` or `Failed` (job idempotency — retries never insert another row).
2. Push to SignalR group `user:{userId}`.
3. Best-effort email when `Type` maps to a Seller template. Identity welcome email stays on `IEmailSender` + `User.WelcomeEmailSent` (ADR-016) and is **not** sent again here.
4. Mark `Delivered`. Failures increment `AttemptCount`; after 5 attempts the row is `Failed` and retries stop.

Hangfire is configured with `AutomaticRetryAttribute { Attempts = 5 }`. After exhaustion the in-app row remains; there is no separate dead-letter table.

Email or SignalR failure does **not** fail the original business transaction. Offline users keep the persisted inbox row.

Tests set `Hangfire:Enabled=false` and use an in-process queue so delivery is synchronous.

## Real-time

- Hub: `/hubs/notifications` (`[Authorize]`)
- JWT via `Authorization: Bearer` **or** query `access_token` **only** when the path starts with `/hubs/notifications`
- On connect, the connection joins `user:{userId}` (from the JWT `sub` claim) and `role:{roleName}`
- The client must not send `userId` or a group name
- Multiple tabs/devices: every connection for the same user joins the same group
- Client method: `notification.received`

### Delivery payload

```json
{
  "id": "019...",
  "type": "seller.application.approved",
  "title": "Congratulations! Your Seller Account Is Approved",
  "message": "Your seller application has been approved. You can now manage your seller profile.",
  "data": { "applicationId": "...", "sellerId": "..." },
  "createdAt": "2026-08-22T02:00:00+00:00"
}
```

This is a delivery DTO, not the persistence model. REST inbox responses still use `body` / `dataJson` / read state.

### Frontend

```ts
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${apiBaseUrl}/hubs/notifications`, {
    accessTokenFactory: () => getAccessToken()
  })
  .withAutomaticReconnect()
  .build();

connection.on("notification.received", (payload) => {
  // payload.id, type, title, message, data, createdAt
});

await connection.start();
```

Do not hard-code tokens. Missed events while offline are loaded from `GET /api/v1/notifications`.

## Inbox API (`[Authorize]`, own rows only)

`UserId` is always taken from `CurrentUser`. Another user's id returns 404 (no leak).

| Method | Route |
|---|---|
| GET | `/api/v1/notifications?unreadOnly=&page=&pageSize=` |
| GET | `/api/v1/notifications/unread-count` |
| GET | `/api/v1/notifications/{id}` |
| POST | `/api/v1/notifications/{id}/read` |
| POST | `/api/v1/notifications/read-all` |

Mark-all-read is a single database `UPDATE` (`ExecuteUpdate`), not a load/loop.

## Admin API (`[Authorize(Roles = Admin)]`)

Trusted create/list/update/delete for support workflows. Admin `POST` requires **either** `userId` **or** `roleName` (not both). Role create fans out one persisted row per user.

| Method | Route |
|---|---|
| GET | `/api/v1/admin/notifications?userId=&unreadOnly=&page=&pageSize=` |
| GET | `/api/v1/admin/notifications/{id}` |
| POST | `/api/v1/admin/notifications` |
| PUT | `/api/v1/admin/notifications/{id}` |
| DELETE | `/api/v1/admin/notifications/{id}` |

## Current publishers

| Source | Types | Notes |
|---|---|---|
| Identity | `identity.welcome` | After register / first Google create. Email remains ADR-016 (`WelcomeEmailSent`). |
| Seller | `seller.application.submitted`, `.approved`, `.rejected`, `seller.suspended`, `seller.reactivated` | After business commit. `data` includes `applicationId` / `sellerId`. Rejection/suspension **reason** is included; reviewer ids are not. |
| Catalog | `catalog.product.submitted`, `.approved`, `.rejected` | After product review. |
| Orders | `order.placed`, `order.received` | After successful checkout SaveChanges. Failure still returns 201. |

## Database

Table `notifications`:

- FK `user_id` → `users` (Cascade)
- Unique `idempotency_key`
- Index `(user_id, is_read, created_at)`
- `delivery_status` stored as string (`Pending`, `Delivered`, `Failed`)
- `data_json` varchar(4000)

Hangfire uses a separate PostgreSQL schema `hangfire` on the same database.

Development dashboard: `/hangfire` (not mapped outside Development).

## Authentication and CORS

JWT Bearer:

1. Reads `Authorization: Bearer` for API and hubs
2. Reads `access_token` query **only** for `/hubs/notifications`
3. Still validates issuer, audience, lifetime, and `sst` security stamp

CORS uses `Cors:AllowedOrigins` (Development default `http://localhost:4200`) with `AllowCredentials()`. `AllowAnyOrigin()` is not used.

## Failure behavior

| Step | Failure | Result |
|---|---|---|
| Business `SaveChanges` | Error | Request fails; no notification |
| Notification persist / enqueue | Error | Logged; business commit stands; HTTP still succeeds for Seller/Identity wrappers |
| SignalR | User offline or push fails | Row stays; Hangfire retries; inbox API still returns it |
| Email channel | SMTP/console error | Logged; in-app + SignalR still proceed |

## Future channels

`IRealtimeNotificationSender` is the push port. Add email/push adapters beside SignalR without changing `Notification`. Broadcast can reuse `PublishToRoleAsync` or a later fan-out job.

## Intentionally out of scope

Outbox table, production Hangfire dashboard auth, push providers, notification templates localization.
