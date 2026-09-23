# Infrastructure layer

See root `CLAUDE.md` for cross-cutting rules.

## Rules
- **Implements Application ports** + technical detail (EF Core, Identity, Dapper, etc.). **EF is used only here**;
  `DbContext` never leaks to Application or Api.
- **Persistence conventions:** strongly-typed ID value converters registered globally (`ConfigureConventions`);
  `EFCore.NamingConventions` handles snake_case column/table names for Postgres — don't hand-name them; use `jsonb`
  for value objects/complex properties where it fits better than a normalized table; Fluent API
  (`IEntityTypeConfiguration<>`), not attributes on entities.
- **Aggregate boundaries in EF:** navigations *within* an aggregate; cross-aggregate references stay a scalar
  **Id only**, no navigation property.
- **Read-stores (queries):** `.AsNoTracking()`, project to DTOs with `.Select()`, paginate (`Skip`/`Take` + a
  stable `OrderBy`), prefer `AnyAsync()` over `CountAsync() > 0`, watch for N+1. Load aggregates whole via
  repositories with the needed `Include`s.
- **Dapper** is available for read-heavy or raw-SQL cases where EF's query translation gets in the way — keep it
  next to EF Core, not as a full replacement.
- Register all Infrastructure services/ports from one composition method (e.g. `AddInfrastructure(this
  IServiceCollection ...)`), called once from `Program.cs`.

## Tests
See `tests/AppTemplate.Integration.Tests/CLAUDE.md`.
