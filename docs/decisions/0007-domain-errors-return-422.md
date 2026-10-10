# 0007. Domain errors return 422

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
Some domain rule violations look like state conflicts (shipping an order that is already shipped) and some like
invalid values. A per-rule HTTP status would need a lookup table in Api that follows every new domain code.

## Decision
Every `DomainException` becomes **422 Unprocessable Entity** with its code in the ProblemDetails `code` extension
(`DomainExceptionHandler`). **409** is reserved for real conflicts reported by Application — duplicates and
optimistic-concurrency conflicts ([0008](0008-result-pattern-for-expected-failures.md),
[0013](0013-save-outcomes-unique-violations-and-conflicts.md)).

## Consequences
- One mapping rule for all domain errors; Api doesn't need to know which invariant failed.
- The client reacts to `code`, not to the status.

## Alternatives considered
- **Per-code status mapping (409 for state conflicts, 422 for invalid values)** — rejected: a table in Api that
  must follow every new domain code, for no gain on the client side.
