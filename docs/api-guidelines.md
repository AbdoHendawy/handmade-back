# API guidelines

## Base URL

```
/api/v1/
```

Future resources (not all implemented yet):

- `/api/v1/auth`
- `/api/v1/seller/applications`
- `/api/v1/seller/profile`
- `/api/v1/admin/seller-applications`
- `/api/v1/admin/sellers`
- `/api/v1/notifications`
- `/api/v1/admin/notifications`
- `/api/v1/artworks`
- `/api/v1/categories`

## Versioning

URL path versioning. Breaking changes require `/api/v2`.

## HTTP methods

| Method | Use |
|---|---|
| GET | Read |
| POST | Create / non-idempotent actions |
| PUT | Full replace |
| PATCH | Partial update |
| DELETE | Remove |

## Status codes

| Code | When |
|---|---|
| 200 | Success with body |
| 201 | Created |
| 204 | Success without body |
| 400 | Validation / domain rule |
| 401 | Unauthenticated |
| 403 | Forbidden |
| 404 | Not found |
| 409 | Conflict |
| 429 | Rate limited (future) |
| 500 | Unexpected |

Errors use **RFC 7807 ProblemDetails** (`application/problem+json` style payload). No `{ success, data }` envelope.

## Resource naming

- Plural nouns: `artworks`, `makers`
- Kebab-case only if multi-word path segments are needed
- IDs are UUIDs in path segments

## Dates

- Serialize as ISO-8601
- Store/treat as UTC (`DateTimeOffset`)

## Nullability

- Prefer omitting nulls in JSON (`WhenWritingNull`)
- Required fields enforced by FluentValidation in Application

## Pagination

Admin collection endpoints use `PagingQuery` / `PagedResult<T>`:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0
}
```

Query: `?page=1&pageSize=20`. Default page size 20, maximum 100. Do not invent per-endpoint pagination shapes.

## Sorting / filtering (future)

- `sort` comma-separated fields; prefix `-` for descending
- Filters as explicit query parameters (`categoryId`, `tag`, `q`) — document per endpoint

## Sparse fieldsets / `ignoreAttr`

Do **not** add a global GET query such as `ignoreAttr` or `fields` on every endpoint.

Current DTOs are small; omitting a few timestamps does not improve frontend performance compared with TLS, JWT, and RTT. A global exclude/allow list also weakens OpenAPI and generated TypeScript types.

When list payloads grow (catalog, products):

- Prefer a **list DTO** vs **detail DTO** (summary card on collections, full resource on `GET by id`).
- Add `?fields=` (allowlist) only if one collection URL must serve both a table and a tiny dropdown. Unknown field names must return 400. Never use field filters on POST/PUT, and never as a security mechanism.

## Validation errors

ProblemDetails with `extensions.errors` map of field → messages, `code: validation_failed`.
