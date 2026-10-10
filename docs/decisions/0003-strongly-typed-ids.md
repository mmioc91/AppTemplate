# 0003. Strongly-typed IDs

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
With several aggregates and child entities keyed by `Guid`, nothing stops a caller from passing an order's id
where a customer's is expected.

## Decision
Every aggregate and child entity gets a `readonly record struct` ID wrapping a `Guid`
(`public readonly record struct OrderId(Guid Value)`). Values are UUIDv7 (`Guid.CreateVersion7()`), generated in
Application, never by the database ([0002](0002-ports-in-application.md)).

## Consequences
- The compiler rejects passing an `OrderId` where a `CustomerId` is expected.
- UUIDv7 is time-ordered, so it indexes well as a primary key.
- Infrastructure needs one value converter per ID type, registered once in `AppDbContext.ConfigureConventions`;
  EF must not generate the value (`ValueGeneratedNever`).

## Alternatives considered
- **Raw `Guid`** — rejected: ID mix-ups are a real bug class once there are many ID types.
- **Database-generated keys** — rejected: an aggregate should have its identity before it is saved.
