# 0004. Migrations and seeding in a background service, with live/ready health checks

- **Status:** accepted
- **Made by:** Cole, solo refactor (November 2026)
- **Evidence:** commits "fix(startup): run migrations and seeding in a BackgroundService so Azure's 230 s port probe passes" and "feat(ops): split health into live/ready, add /status build stamp, Seeding:RunOnStartup flag" (2026-11-10); `src/SteamProject/Services/StartupDataInitializer.cs`.

## Context

The app applied EF Core migrations to both databases and ran every seeder synchronously in `Program.cs`
before the web server started. Against a cold Azure SQL instance this took longer than Azure App Service's
230-second container start limit, so the site restarted in a loop. A single `/health` endpoint checked both
databases, so "the process is up" and "the databases answer" could not be told apart.

## Decision

- Move migrate and seed into `StartupDataInitializer`, a `BackgroundService` that yields straight away so
  Kestrel starts listening in seconds, then does the database work. Every seeder is idempotent.
- Migrations always run. Seeding is controlled by `Seeding:RunOnStartup` (default `true`), so an environment
  can skip it on cold starts.
- Split health checks: `/health/live` runs no checks and never touches SQL, `/health/ready` checks both
  databases, and `/health` keeps its old behaviour for existing monitors. An anonymous `/status` page shows the
  build SHA, build time and environment.

## Consequences

- The site answers quickly after a cold start; requests that arrive before the background work finishes may
  briefly see a database that is not yet seeded.
- Monitors can tell a dead process (`/health/live` fails) from a slow or unavailable database
  (`/health/ready` fails).
- Startup failures in the background work are logged rather than stopping the host.
