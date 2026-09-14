# Domain layer

See root `CLAUDE.md` for cross-cutting rules that also apply here.

## Rules (non-negotiable)

- **Pure:** BCL only. **No** `Microsoft.EntityFrameworkCore` / `Npgsql` / `Dapper` / `Microsoft.AspNetCore` (or
  `Microsoft.AspNetCore.Identity`) / any framework ref. If you reach for a framework `using`, you're in the wrong
  layer. Enforced at build time by `tests/AppTemplate.Architecture.Tests`.
- **Rich model:** private setters; state changes only via methods that guard invariants; creation via `Create(...)`
  /factory methods; throw `DomainException` on invariant breaches (never for control flow).
- **Building blocks live in `AppTemplate.SharedKernel`** (referenced by this project): entities derive from
  `Entity<TId>` or `AggregateRoot<TId>` (identity equality), value objects from `ValueObject` (value equality), and
  "smart enums" from `Enumeration<TEnum>` instead of a plain C# `enum` when a value needs behavior. See
  `src/AppTemplate.SharedKernel/CLAUDE.md`.
- **Aggregates reference other aggregates by Id only** — no navigation property across aggregate boundaries.
  Navigation is fine *within* an aggregate.
- **Strongly-typed IDs** (`record struct`) instead of raw `Guid`/`int`. Value objects for domain concepts that need
  their own validation/equality (e.g. `Money`, `EmailAddress`).
- **Time:** methods receive timestamps as parameters (e.g. `DateTimeOffset now`) instead of Domain depending on
  `TimeProvider` or any clock service itself — the caller (Application) resolves the time and passes it in. Keeps
  Domain pure and testable without mocking a clock.

## Tests

xUnit + Shouldly (see root `CLAUDE.md` — this template uses Shouldly, not FluentAssertions). Pure logic only, no I/O,
no framework references — same purity rule as the production code. Test invariants directly (e.g. calling a
state-changing method a second time should throw where that's an invariant).
