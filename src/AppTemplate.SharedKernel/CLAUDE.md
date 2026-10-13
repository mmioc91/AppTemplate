# SharedKernel

See root `CLAUDE.md` for cross-cutting rules. This is the innermost project — even Domain depends on it, so the
purity bar here is at least as strict as Domain's.

## Rules
- **BCL only, zero dependencies.** No `ProjectReference` to any other AppTemplate project, no NuGet packages beyond
  what the SDK gives you. Enforced by `tests/AppTemplate.Architecture.Tests`.
- **Generic DDD building blocks, not business logic.** This project holds base types every project built from this
  template will want (`Entity<TId>`, `AggregateRoot<TId>`, `Enumeration<TEnum>`, `DomainException`) — nothing that
  encodes a specific domain's rules. There is no `ValueObject` base class: value objects are `record`s in Domain
  (ADR 0016).
- `Entity<TId>` / `AggregateRoot<TId>`: identity-based equality (same `Id` + runtime type = same entity), not
  reference equality. `Id` is set once, by the constructor (`private init`) — neither outside code nor a derived
  entity can change it. `AggregateRoot<TId>` is currently just a marker distinguishing aggregate roots from plain
  entities (e.g. for constraining repository ports to `TAggregateRoot : AggregateRoot<TId>`) — add domain-event
  raising here only once there's an actual dispatch mechanism in Infrastructure to receive them.
- `Enumeration<TEnum>`: "smart enum" — a plain C# `enum` has no behavior; this lets each value carry methods
  (e.g. `PaymentStatus.Failed.CanRetry()`) and gives safe `FromValue`/`FromName`/`GetAll` lookups instead of
  `Enum.Parse` (ADR 0005). Values and names must be unique — names case-insensitively, as `FromName` matches them
  (values are stored by name). A duplicate throws on the type's first lookup (`TypeInitializationException`), so
  every smart enum needs a test that calls `FromName` or `GetAll`.
- `DomainException(code, message)`: thrown by Domain on an invariant breach. The code is mandatory — a stable
  `UPPER_SNAKE_CASE` string clients switch on; the message is English, for logs only (ADR 0004).

## Tests
No dedicated test project — these are thin enough to be tested from `tests/AppTemplate.Domain.Tests`, at its project
root (`EntityTests`, `EnumerationTests`, `DomainExceptionTests`). Add `tests/AppTemplate.SharedKernel.Tests` if/when
this project grows real logic worth testing in isolation.
