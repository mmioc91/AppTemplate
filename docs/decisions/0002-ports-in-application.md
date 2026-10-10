# 0002. Ports live in Application; Domain receives resolved values

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
Domain logic needs things from outside: the current time, new IDs, configured policy values, data from other
aggregates. Common DDD practice lets Domain call interfaces (`IClock`, `IIdGenerator`, domain services) defined in
Domain itself.

## Decision
- Port interfaces are defined in **Application** (`Abstractions/`) and implemented in Infrastructure.
- Domain methods never call a port. Application resolves the value first and passes it in as a parameter: current
  time (`DateTimeOffset now`), new IDs (`OrderId id`), configured values.
- **No ID generator port:** Application creates IDs directly with `Guid.CreateVersion7()` wrapped in the
  strongly-typed ID. Tests read the new ID from the use case's result. Introduce a port only if a use case ever needs
  deterministic IDs.
- Time comes from the injected `TimeProvider` (`FakeTimeProvider` in tests); `DateTime.Now`/`UtcNow` are banned.

## Consequences
- Domain stays pure and is tested without mocks.
- Every Domain factory takes its ID and timestamp as arguments.
- One less interface and adapter for IDs.

## Alternatives considered
- **Domain-service interfaces in Domain** — rejected: keeps one simple rule instead: Domain has no interfaces to
  external concerns at all.
- **`IIdGenerator` port** — rejected: an abstraction with a single fixed implementation and no consumer that benefits
  from replacing it.
