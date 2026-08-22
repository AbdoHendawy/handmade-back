# Architecture

## Style

Handmade backend is a **modular monolith** using **Clean Architecture**.

```
┌─────────────────────────────────────────────┐
│                  Handmade.Api               │
│  Controllers, middleware, OpenAPI, health   │
└─────────────────────┬───────────────────────┘
                      │ depends on Application
                      │ (+ Infrastructure for DI only)
┌─────────────────────▼───────────────────────┐
│             Handmade.Application            │
│  Abstractions ports, validation, use cases   │
└─────────────────────┬───────────────────────┘
                      │ depends on Domain
┌─────────────────────▼───────────────────────┐
│               Handmade.Domain               │
│  Entity bases, domain exceptions            │
└─────────────────────────────────────────────┘
                      ▲
┌─────────────────────┴───────────────────────┐
│           Handmade.Infrastructure           │
│  EF Core, PostgreSQL, file storage stubs    │
└─────────────────────────────────────────────┘
```

## Dependency rules

- Domain depends on nothing framework-related.
- Application depends only on Domain.
- Infrastructure depends on Application and Domain.
- Api depends on Application; references Infrastructure solely to call `AddInfrastructure`.

Architecture tests under `tests/Handmade.Architecture.Tests` enforce these rules.

## Modular monolith (modules)

Features are vertical slices / folders inside the existing projects:

| Module | Examples |
|---|---|
| Identity | User, roles, register, login, JWT, refresh tokens |
| Seller | Applications, admin review, seller profile, Active/Suspended |
| Notifications | Persistent inbox, Hangfire delivery, SignalR |
| Catalog / Products | Categories, products, images, variants, publishing |
| Cart | Authenticated shopping cart, live price display, no inventory reservation |
| Orders | Checkout, OrderGroup, one Order per seller, immutable snapshots |
| Payments | Future |
| Social / Collections | Future |

Do not extract microservices until a concrete scaling or team boundary requires it.

See [identity.md](identity.md), [seller.md](seller.md), [notifications.md](notifications.md), [catalog.md](catalog.md), [cart.md](cart.md), and [orders.md](orders.md).

## Request flow (future)

```
HTTP → Controller → Application service / use case
                 → FluentValidation
                 → Domain rules
                 → IApplicationDbContext (EF)
                 → ProblemDetails on failure
```

MediatR is not used. Prefer explicit application services until a pipeline abstraction is justified.
