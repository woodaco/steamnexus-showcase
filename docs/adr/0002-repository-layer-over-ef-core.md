# 0002. A repository layer over EF Core

- **Status:** accepted
- **Made by:** team decision (capstone, February 2023)
- **Evidence:** `src/SteamProject/DAL/Abstract/IRepository.cs` and `DAL/Concrete/Repository.cs` were added on 2023-02-15, and every team member added repositories to `DAL/` during the capstone.

## Context

Four people were building features in parallel, and each feature needed to read and write the domain
database. Controllers that talk to `DbContext` directly are hard to unit test and tend to duplicate queries.

## Decision

Put data access behind interfaces in `DAL/Abstract` with EF Core implementations in `DAL/Concrete`: a generic
`IRepository<T>` / `Repository<T>` for the common operations, plus one repository per aggregate
(`IUserRepository`, `ICompetitionRepository`, `IBadgeRepository` and so on). They are registered as scoped
services in `Program.cs` and injected into controllers.

## Consequences

- Controllers and services are unit tested against mocked repositories; most of the NUnit suite works this way.
- Query logic lives in one place per aggregate, which made the later performance fixes local: N+1 badge
  checks, full-table scans on the blacklist and per-message `SaveChanges` were each fixed in one repository.
- Lazy-loading proxies (also a team choice) make navigation access easy but hide queries. The solo refactor
  added eager loading where lazy loads caused null-reference errors or N+1 queries, and added async
  repository methods for the async controller paths.
