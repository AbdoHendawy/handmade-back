# API guidelines

## Base URL

```
/api/v1/
```

Future resources (not implemented yet):

- `/api/v1/auth`
- `/api/v1/artworks`
- `/api/v1/makers`
- `/api/v1/categories`
- `/api/v1/tags`
- `/api/v1/collections`

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

## Pagination (future contract)

When list endpoints arrive, use a consistent shape:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0
}
```

Query: `?page=1&pageSize=20&sort=-createdAt`. Do not invent per-endpoint pagination shapes.

## Sorting / filtering (future)

- `sort` comma-separated fields; prefix `-` for descending
- Filters as explicit query parameters (`categoryId`, `tag`, `q`) — document per endpoint

## Validation errors

ProblemDetails with `extensions.errors` map of field → messages, `code: validation_failed`.
