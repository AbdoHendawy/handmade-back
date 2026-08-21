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

## Modular monolith (future modules)

Features will be added as vertical slices / folders inside the existing projects, for example:

| Module | Examples |
|---|---|
| Auth | Register, login, tokens |
| Users | Profiles |
| Makers | Maker profiles |
| Artworks | CRUD, publish |
| Categories / Tags | Taxonomy |
| Social | Likes, follows |
| Collections | Saves, collections |
| Moderation / Admin | Review workflows |

Do not extract microservices until a concrete scaling or team boundary requires it.

## Request flow (future)

```
HTTP → Controller → Application service / use case
                 → FluentValidation
                 → Domain rules
                 → IApplicationDbContext (EF)
                 → ProblemDetails on failure
```

MediatR is not used. Prefer explicit application services until a pipeline abstraction is justified.
