# 0016. Value objects are records, mapped as EF complex types

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
A `ValueObject` base class (equality via `GetEqualityComponents()`) duplicates what C# records generate for free,
boxes every component on each comparison, and gives two ways to write the same thing. In EF, value objects are often
mapped as owned types — but owned types are entities with an identity tied to their owner, so one instance can
belong to only one owner: a shared static default or a value copied onto two rows is saved on one and lost (or a
not-null violation) on the other.

## Decision
- Value objects are `sealed record`s with no base class; strongly-typed IDs are `readonly record struct`s. A value
  object holding a collection overrides `Equals`/`GetHashCode`, because a record compares collections by reference.
  SharedKernel has no `ValueObject` class.
- Infrastructure maps value objects with `ComplexProperty` (EF complex types): no identity, one instance may be
  shared, stored as columns of the owning table. Optional complex types are supported (EF 10). Child *entities* with
  their own id stay owned collections.

## Consequences
- One way to write a value object; value semantics hold in the database too.
- Record equality includes every field; a value object that must exclude one overrides equality itself.

## Alternatives considered
- **Keep `ValueObject`** — rejected: no case records don't cover, and dead code to maintain.
- **Owned types and never share instances** — rejected: a rule every caller must remember, for a type that promises
  value semantics.
