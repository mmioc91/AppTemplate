# SharedKernel

See root `CLAUDE.md` for cross-cutting rules. This is the innermost project — even Domain depends on it, so the
purity bar here is at least as strict as Domain's.

## Rules
- **BCL only, zero dependencies.** No `ProjectReference` to any other AppTemplate project, no NuGet packages beyond
  what the SDK gives you. Enforced by `tests/AppTemplate.Architecture.Tests`.
- **Generic DDD building blocks, not business logic.** This project holds base types every project built from this
  template will want (`Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `Enumeration<TEnum>`) — nothing that
  encodes a specific domain's rules.
- `Entity<TId>` / `AggregateRoot<TId>`: identity-based equality (same `Id` + runtime type = same entity), not
  reference equality. `AggregateRoot<TId>` is currently just a marker distinguishing aggregate roots from plain
  entities (e.g. for constraining repository ports to `TAggregateRoot : AggregateRoot<TId>`) — add domain-event
  raising here only once there's an actual dispatch mechanism in Infrastructure to receive them.
- `ValueObject`: value-based equality via `GetEqualityComponents()`. For a simple case, a `record`/`record struct`
  in Domain gets you the same thing for free — reach for this base class when you want explicit control over which
  fields participate in equality, or when it's not a `record`.
- `Enumeration<TEnum>`: "smart enum" — a plain C# `enum` has no behavior; this lets each value carry methods
  (e.g. `PaymentStatus.Failed.CanRetry()`) and gives safe `FromValue`/`FromName` lookups instead of `Enum.Parse`.

## Tests
No dedicated test project yet — these are thin enough that Domain's own tests exercising entities/value objects
built on top of them provide adequate coverage. Add `tests/AppTemplate.SharedKernel.Tests` if/when this project
grows real logic worth testing in isolation.
