# Identity & Authentication

Sprint 2 module documentation for Handmade Backend.

## Boundaries

Identity answers:

- Who is the user?
- How did they authenticate?
- What roles do they have?
- Is the session still valid?

Identity does **not** own Seller applications, store profiles, products, or orders. Seller role assignment for trusted internal callers goes through `IIdentityRoleService` (no `SaveChanges`; the caller owns the unit of work). See [seller.md](seller.md).

## Domain model

| Entity | Purpose |
|---|---|
| `User` | Account aggregate (`email`, password hash optional, `security_stamp`, welcome flag) |
| `Role` | `Customer`, `Seller`, `Admin` |
| `UserRole` | Many-to-many |
| `ExternalLogin` | Linked providers (Google now; Apple/Microsoft later) |
| `RefreshToken` | Opaque hashed refresh tokens with rotation |

Registration always assigns **Customer** server-side. Clients cannot choose Admin/Seller. The Seller module assigns the Seller role after admin approval via `IIdentityRoleService`.

## Auth flows

### Email register / login

`POST /api/v1/auth/register` → validate → normalize email → hash password (Argon2id) → Customer role → welcome email attempt → JWT + refresh.

`POST /api/v1/auth/login` → generic invalid credentials (no enumeration).

### Google (SPA id_token)

Angular Google Sign-In → `POST /api/v1/auth/google` with `{ "idToken": "..." }`.

Server validates via `Google.Apis.Auth` (`aud`, `iss`, `exp`, `email_verified`).

Account linking: verified Google email matching an existing user **links** `ExternalLogin` (no duplicate user). Unverified Google email is rejected.

### Refresh / logout

`POST /api/v1/auth/refresh` rotates refresh tokens (old revoked, cannot reuse).

`POST /api/v1/auth/logout` revokes the supplied refresh token.

### Admin force logout

`POST /api/v1/admin/users/{userId}/revoke-sessions` (Admin):

1. Revokes all refresh tokens
2. Increments `User.SecurityStamp`
3. JWT Bearer `OnTokenValidated` rejects tokens whose `sst` claim mismatches → immediate kick

## JWT

Claims: `sub`, `email`, `jti`, `role`(s), `sst` (security stamp).

Configure `Jwt` section (secret min 32 chars). Startup fails if missing.

## Welcome email

`IEmailSender` + `WelcomeEmailTemplate`. Development uses `ConsoleEmailSender` (logs only).

Sent once on first registration (email or Google). Idempotent via `User.WelcomeEmailSent`. Failure does not roll back user creation. No Outbox broker yet (design is Outbox-compatible).

Registration also publishes an in-app `identity.welcome` notification after the user row commits (`IIdentityNotificationService`). That inbox row is delivered over SignalR when the user is connected. The Hangfire delivery job does **not** send a second welcome email.

## Endpoints

| Method | Route | Auth |
|---|---|---|
| POST | `/api/v1/auth/register` | Anonymous |
| POST | `/api/v1/auth/login` | Anonymous |
| POST | `/api/v1/auth/google` | Anonymous |
| POST | `/api/v1/auth/refresh` | Anonymous |
| POST | `/api/v1/auth/logout` | Anonymous (refresh body) |
| GET | `/api/v1/auth/me` | Bearer |
| GET | `/api/v1/admin/ping` | Admin |
| POST | `/api/v1/admin/users/{id}/revoke-sessions` | Admin |

## Configuration

```json
"Jwt": {
  "SecretKey": "...",
  "Issuer": "Handmade",
  "Audience": "Handmade",
  "AccessTokenExpirationMinutes": 15,
  "RefreshTokenExpirationDays": 14
},
"GoogleAuth": {
  "ClientId": "....apps.googleusercontent.com"
}
```

Use user-secrets / environment variables in non-Development environments. Never commit real secrets.

## Migrations

```bash
dotnet ef database update --project src/Handmade.Infrastructure --startup-project src/Handmade.Api
```

Development auto-migrates on API startup, then seeds roles idempotently.

## Security review (Sprint 2)

| Area | Status |
|---|---|
| Passwords | Argon2id; never logged |
| JWT | Validated issuer/audience/signature; startup requires strong secret |
| Refresh | Opaque, hashed at rest, rotated, revocable |
| Google | Official library; requires `email_verified` for link/create |
| Roles | Server-side Customer only; Admin/Seller not self-assigned |
| Force logout | SecurityStamp + revoke all refresh |
| Enumeration | Generic login errors |
| Secrets | Config/env; `.env` gitignored |
| Logging | No password/token/hash logging in auth paths |

## Intentionally out of scope

Seller application **business rules** live in the Seller module. Identity only exposes role assignment. Admin user bootstrap UI, production email provider, Outbox table, Redis token blacklist remain out of scope.
