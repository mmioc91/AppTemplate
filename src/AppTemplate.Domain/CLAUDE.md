# Domain layer

See root `CLAUDE.md` for cross-cutting rules that also apply here.

## Rules (non-negotiable)

- **Pure:** BCL only. **No** `Microsoft.EntityFrameworkCore` / `Npgsql` / `Dapper` / `Microsoft.AspNetCore` (or
  `Microsoft.AspNetCore.Identity`) / any framework ref. If you reach for a framework `using`, you're in the wrong
  layer. Enforced at build time by `tests/AppTemplate.Architecture.Tests`.
- **Rich model:** private setters; state changes only via methods that guard invariants; creation via `Create(...)`
  /factory methods; throw `DomainException` on invariant breaches (never for control flow). An aggregate or child
  whose constructor EF can't bind gets an extra `private` parameterless constructor "for EF Core materialization
  only" (ADR 0015) — never call it from code.
- **Error codes:** every `DomainException` takes a code — `new DomainException(OrderErrors.X, "message")`. Codes are
  `UPPER_SNAKE_CASE` `const string`s in an `<Aggregate>Errors` class in the aggregate's folder. They are API contract
  (the frontend switches on them): never rename or reuse one. The message is English for logs, may include values,
  never PII. See `docs/decisions/0004-domain-exception-and-error-codes.md`.
- **Building blocks live in `AppTemplate.SharedKernel`** (referenced by this project): entities derive from
  `Entity<TId>` or `AggregateRoot<TId>` (identity equality) and "smart enums" from `Enumeration<TEnum>`. See
  `src/AppTemplate.SharedKernel/CLAUDE.md`.
- **Value objects are `sealed record`s** (value equality for free, no base class). A record compares collections by
  reference, so one holding a collection overrides `Equals`/`GetHashCode` itself. See
  `docs/decisions/0016-value-objects-are-records-and-complex-types.md`.
- **Enum vs smart enum:** if any code will ask something *of a value* — is it terminal, how many days, what limit,
  can it transition to X — it's an `Enumeration<TEnum>` and that answer is a property/method on the value. A plain
  `enum` is only for pure labels. A `switch` over an enum that returns data is the smell that it should have been a
  smart enum. See `docs/decisions/0005-smart-enums-for-values-with-behavior.md`.
- **Layout:** one type per file; folders per aggregate (`Orders/`, `Customers/`), holding its root, child
  entities, value objects, IDs, enums and its `<Aggregate>Errors` class
  (`docs/decisions/0006-one-type-per-file-folders-per-aggregate.md`).
- **Aggregates reference other aggregates by Id only** — no navigation property across aggregate boundaries.
  Navigation is fine *within* an aggregate. One transaction changes one aggregate.
- **Strongly-typed IDs** (`readonly record struct OrderId(Guid Value)`) instead of raw `Guid`/`int` (ADR 0003).
  Domain receives IDs as parameters and never creates them (ADR 0002).
- **Invariants live only here.** FluentValidation in Application checks input shape (non-empty, format, range) and
  must not duplicate domain rules; Domain is the last line.
- **Time:** methods receive timestamps as parameters (e.g. `DateTimeOffset now`) instead of Domain depending on
  `TimeProvider` or any clock service itself — the caller (Application) resolves the time and passes it in. Keeps
  Domain pure and testable without mocking a clock. Instants are `DateTimeOffset` (UTC), calendar dates `DateOnly`.
- **No ports here:** Domain never calls an interface to an external concern; Application resolves the value (time,
  IDs, configured policy values) and passes it in (ADR 0002).
- **Children change only through their root:** child factories and mutators are `internal` (visible to
  `Domain.Tests` via `AssemblyInfo.cs`).

## Model
Project-specific: once this template becomes a real project, list the aggregates, their children and value objects,
and the boundaries between them here (or point to the design doc that does).

## Tests

See `tests/AppTemplate.Domain.Tests/CLAUDE.md`.
