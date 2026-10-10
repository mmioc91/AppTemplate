# 0010. Test project layout

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
Tests differ in speed and in what they need: pure domain logic, use-case orchestration with fake ports, real
database and HTTP, and architecture rules.

## Decision
Four test projects:

| Project              | What                                                                  | Needs          |
|----------------------|-----------------------------------------------------------------------|----------------|
| `Domain.Tests`       | invariants, smart enums, SharedKernel building blocks                 | nothing        |
| `Application.Tests`  | handler orchestration with NSubstitute ports, validators, `Common/`   | nothing        |
| `Integration.Tests`  | persistence on real Postgres, HTTP via `WebApplicationFactory`, each use case end to end | Docker |
| `Architecture.Tests` | layer dependencies (NetArchTest)                                      | nothing        |

The per-use-case checklist (happy path, validation, auth, not found, persisted state, concurrency, idempotency)
lives in `Integration.Tests`. Time is controlled with `FakeTimeProvider`. A database-reset tool (Respawn) or HTTP
stubs (WireMock.Net) are added when the first test needs them.

## Consequences
- `pre-push` runs only the fast projects; CI runs all of them.
- `Application.Tests` stays small: only logic worth testing without a database.

## Alternatives considered
- **One test project per layer including Infrastructure/Api** — rejected: persistence and HTTP tests share the same
  Postgres container and fixtures.
