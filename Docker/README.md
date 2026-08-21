# Docker notes for Handmade backend local development

## What runs in Docker

Only **PostgreSQL** is containerized by default. Run the API with `dotnet run` on the host for a faster inner loop.

## Start

```bash
cp .env.example .env
docker compose up -d
```

## Stop

```bash
docker compose down
```

## Reset database volume

```bash
docker compose down -v
```
