# Notifications

Reusable persistent notifications for every module. SignalR is a **delivery mechanism**, not the domain.

```
Business event
      │
      ▼
INotificationPublisher (persist)
      │
      ▼
PostgreSQL `notifications`
      │
      ▼
Hangfire job (`INotificationDeliveryService`)
      │
      ▼
SignalR hub  →  connected client
      │
      ▼
Optional email channel (`IEmailSender`)
```

Clients that were offline still see the notification via `GET /api/v1/notifications` after they reconnect.

## Boundaries

| Layer | Owns |
|---|---|
| Domain | `Notification` aggregate, delivery status, idempotency key, read state |
| Application | `INotificationPublisher`, inbox queries, delivery use case, ports |
| Infrastructure | EF mapping, Hangfire PostgreSQL storage + enqueue adapter |
| Api | `NotificationHub`, JWT-for-hubs, SignalR sender, REST inbox |

Domain and Application **must not** reference Hangfire or SignalR.

## Creating a notification (other modules)

```csharp
await _publisher.PublishToUserAsync(new CreateUserNotificationRequest(
    userId,
    type: "order.paid",
    title: "Payment received",
    body: "Order #123 is paid.",
    idempotencyKey: $"order.paid:{orderId:D}",
    dataJson: null));
```

`PublishToRoleAsync` fans out one persisted row per user in that role (`idempotencyPrefix:{userId}`).

Re-publishing the same `idempotencyKey` returns the existing row and does **not** enqueue a second job.

## Delivery

Hangfire runs `INotificationDeliveryService.DeliverAsync`:

1. Skip if already `Delivered` or `Failed` (job idempotency).
2. Push to SignalR group `user:{userId}`.
3. Best-effort email when `Type` maps to a known template (Seller + Welcome).
4. Mark `Delivered`. Failures increment `AttemptCount`; after 5 attempts the row is `Failed` and retries stop.

Email failure does **not** fail the job or the original business transaction.

Tests set `Hangfire:Enabled=false` and use an in-process queue so delivery is synchronous.

## Real-time

- Hub: `/hubs/notifications` (`[Authorize]`)
- JWT via `Authorization: Bearer` **or** query `access_token` (browser WebSockets)
- On connect, the connection joins `user:{userId}` and `role:{roleName}`
- Client method: `notificationReceived`

## Inbox API (`[Authorize]`, own rows only)

| Method | Route |
|---|---|
| GET | `/api/v1/notifications?unreadOnly=&page=&pageSize=` |
| GET | `/api/v1/notifications/unread-count` |
| GET | `/api/v1/notifications/{id}` |
| POST | `/api/v1/notifications` |
| PUT | `/api/v1/notifications/{id}` |
| DELETE | `/api/v1/notifications/{id}` |
| DELETE | `/api/v1/notifications` |
| POST | `/api/v1/notifications/{id}/read` |
| POST | `/api/v1/notifications/read-all` |

`POST` creates for the current user and still goes through persist → Hangfire → SignalR. `Type` and `UserId` are immutable after create. Another user's id returns 404 (no leak).

## Admin API (`[Authorize(Roles = Admin)]`)

| Method | Route |
|---|---|
| GET | `/api/v1/admin/notifications?userId=&unreadOnly=&page=&pageSize=` |
| GET | `/api/v1/admin/notifications/{id}` |
| POST | `/api/v1/admin/notifications` |
| PUT | `/api/v1/admin/notifications/{id}` |
| DELETE | `/api/v1/admin/notifications/{id}` |

Admin `POST` requires **either** `userId` **or** `roleName` (not both). Role create fans out one persisted row per user.

## Database

Table `notifications`:

- FK `user_id` → `users` (Cascade)
- Unique `idempotency_key`
- Index `(user_id, is_read, created_at)`
- `delivery_status` stored as string (`Pending`, `Delivered`, `Failed`)

Hangfire uses a separate PostgreSQL schema `hangfire` on the same database.

Development dashboard: `/hangfire` (not mapped outside Development).

## Future channels

`IRealtimeNotificationSender` is the push port. Add email/push adapters beside SignalR without changing `Notification`. Broadcast can reuse `PublishToRoleAsync` or a later fan-out job.

## Intentionally out of scope

Outbox table, production Hangfire dashboard auth, push providers, notification templates localization, Product/Order publishers (call `INotificationPublisher` when those modules exist).
