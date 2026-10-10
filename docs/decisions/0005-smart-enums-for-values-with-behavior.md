# 0005. Smart enums for values with behavior

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
A plain C# `enum` is a label. As soon as code asks something *of* a value — is this status terminal, how many days
does this policy grant — that answer ends up in `switch` statements scattered across the code base.

## Decision
If code needs to ask something of a value, the type is an `Enumeration<TEnum>` (SharedKernel) and the answer is a
property or method on the value (`OrderStatus.Shipped.IsTerminal`). A plain `enum` only for pure labels. A `switch`
over an enum that returns data is the smell that it should have been a smart enum.

## Consequences
- The rule for a value is defined once, next to the value.
- Smart enum values can't be `case` constants; compare with `==` or use their properties.
- Infrastructure stores smart enums by `Name` (`EnumerationConverter<TEnum>`) and plain enums as strings, so columns
  are readable and survive reordering. Renaming or removing a value needs a data migration.

## Alternatives considered
- **Plain enums everywhere** — rejected: scatters per-value rules into `switch` statements.
