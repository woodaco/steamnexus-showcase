# Architecture decision records

Each record says who made the decision, based on `git log`:

- **Team decision**: made during the 2023 capstone by the four-person team.
- **Cole, solo refactor**: made by Cole Woodard after graduation.

| # | Decision | Made by |
|---|---|---|
| [0001](0001-separate-identity-and-domain-databases.md) | Separate Identity and domain databases | Team decision |
| [0002](0002-repository-layer-over-ef-core.md) | A repository layer over EF Core | Team decision |
| [0003](0003-secure-by-default-authorization.md) | Secure-by-default authorization (global fallback policy) | Cole, solo refactor |
| [0004](0004-migrate-and-seed-in-background-service.md) | Migrations and seeding in a background service, with live/ready health checks | Cole, solo refactor |
