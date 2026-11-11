# 0001. Separate Identity and domain databases

- **Status:** accepted
- **Made by:** team decision (capstone, February 2023)
- **Evidence:** the commit "Identity db is now set to SteamInfoAuth db created by up script" (2023-02-09); `Program.cs` registers `ApplicationDbContext` and `SteamInfoDbContext` against two connection strings.

## Context

The app has two kinds of data: ASP.NET Core Identity's accounts, roles and logins, and the Steam/domain data
(users' games, achievements, friends, competitions, badges, messages). The domain schema was designed first
in SQL scripts (`Up.sql` / `Down.sql`) and scaffolded into EF Core, while Identity brings its own schema and
migrations.

## Decision

Keep them in two SQL Server databases, `SteamInfoAuth` and `SteamInfo`, each with its own EF Core context
(`ApplicationDbContext` and `SteamInfoDbContext`) and connection string. A domain `User` row points at its
Identity user through `AspNetUserId`.

## Consequences

- Each schema can be migrated and reset on its own, which the team used while iterating on the domain model.
- There is no foreign key across the two databases, so anything that writes both must keep them in step itself.
  In the solo refactor, registration was made atomic across both contexts, and the domain `User` row is now
  provisioned for Steam-first sign-ins, which previously had none and caused null-reference errors.
- Health checks and startup work have to cover both databases (see [0004](0004-migrate-and-seed-in-background-service.md)).
