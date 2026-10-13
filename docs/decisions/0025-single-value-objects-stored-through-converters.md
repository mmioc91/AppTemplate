# 0025. Single-value value objects are stored through value converters

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
[0016](0016-value-objects-are-records-and-complex-types.md) maps value objects as EF complex types — columns of the
owning table. A value object that wraps a single value (a `Slug`, an `EmailAddress`) often needs a **unique index**
and is compared in queries (`WHERE slug = @slug`). A complex type would put the value in a column named after the
wrapper (`public_slug_value`), and an index on a complex type's property is not something EF's `HasIndex` offers on
the entity. Strongly-typed IDs, which are also single-value wrappers, already go through value converters
([0003](0003-strongly-typed-ids.md)); Podium hit the question with its first slug.

## Decision
- A value object with **exactly one value** is stored as that value through a `ValueConverter` in
  `Persistence/Converters/`, registered once in `AppDbContext.ConfigureConventions` like the IDs.
- The converter reads back through the value object's own factory (`Slug.Create`), so a row that breaks the format
  fails the query instead of producing an invalid object — the same stance as smart enums stored by name.
- Value objects with two or more values (`DateRange`, `Address`) stay complex types per 0016.

## Consequences
- The column has the property's name (`slug`), can carry an index and is compared directly in LINQ.
- Changing a single-value object's format rules can make stored rows unreadable; tightening a rule needs a data
  migration first.

## Alternatives considered
- **Complex type for every value object** — rejected for single values: no index on it, and an awkward column name.
- **A plain `string` property on the aggregate** — rejected: the format invariant would move out of the type.
