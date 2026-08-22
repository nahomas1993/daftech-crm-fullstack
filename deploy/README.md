# DAFTECH CRM — Deployment

This folder orchestrates all three services with Docker Compose:
`postgres` (database), `api` (.NET backend), `frontend` (Angular, served via nginx).

## Prerequisites

- Docker and Docker Compose installed on the host
- The `../api` and `../frontend` project folders present
  alongside this one (docker-compose.yml's build contexts point there) —
  adjust the `context:` paths in docker-compose.yml if your checkout layout differs

## First-time setup

1. Copy the example env file and fill in real values:
   ```
   cp .env.production.example .env
   ```
2. Generate a JWT signing key and put it in `.env`:
   ```
   openssl rand -base64 48
   ```
3. Fill in the PostgreSQL password, SMTP credentials, and the frontend origin.

## Running

```
docker compose up --build -d
```

This builds both images (multi-stage — the .NET SDK and Node.js toolchains
never end up in the final images) and starts all three containers. The
`api` service waits for PostgreSQL to report healthy before starting, and applies
pending EF Core migrations automatically on boot (see Program.cs
`MigrateAndSeedAsync`).

The frontend is reachable at `http://localhost` (or whatever `HTTP_PORT` is
set to). It proxies `/api/*` and `/health` internally to the backend
container — the two are never exposed as separate origins to the browser,
so no CORS configuration is needed for normal operation.

## Generating the database migration

No EF Core migration is checked in yet. Generate the initial one once,
from a machine with the .NET SDK and `dotnet-ef` installed, before the
first deploy:

```
cd ../api/src/DaftechCrm.Api
dotnet tool install --global dotnet-ef   # if not already installed
dotnet ef migrations add InitialCreate --project ../DaftechCrm.Infrastructure --startup-project .
```

Commit the generated migration files, then rebuild the `api` image — it
applies pending migrations automatically on startup
(`Program.cs` → `MigrateAndSeedAsync`).

## Checking health

```
curl http://localhost/health/ready   # database + storage
curl http://localhost/health/live    # process is up
curl http://localhost/health         # everything, including SMTP (soft dependency)
```

## Logs

```
docker compose logs -f api
docker compose logs -f frontend
docker compose logs -f postgres
```

## Updating

```
git pull
docker compose up --build -d
```

Uploaded agreement files (in the `api-storage` volume) and the PostgreSQL data
(in the `postgres-data` volume) both persist across this — they're named
volumes, not part of the container filesystem.
