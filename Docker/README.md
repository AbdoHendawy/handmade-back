# Docker notes for Handmade backend local development

## What runs in Docker

**PostgreSQL** and **MinIO** are containerized by default. Run the API with `dotnet run` on the host for a faster inner loop.

MinIO is local object storage for product images (`IFileStorage`). The API is not containerized.

## Start

```bash
cp .env.example .env
docker compose up -d
```

- PostgreSQL: `localhost:5432`
- MinIO API: `http://localhost:9000`
- MinIO console: `http://localhost:9001`

## Stop

```bash
docker compose down
```

## Reset database volume

```bash
docker compose down -v
```

This also resets MinIO development data.
