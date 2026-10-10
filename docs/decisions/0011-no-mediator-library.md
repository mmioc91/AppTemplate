# 0011. No mediator library

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
Use cases are often dispatched through MediatR with a behavior pipeline (logging → validation → transaction).
MediatR has required a commercial license since 2025; source-generated alternatives exist
(e.g. martinothamar/Mediator, MIT).

## Decision
No mediator library. Application defines its own small handler interfaces — `ICommandHandler<TCommand>`,
`ICommandHandler<TCommand, TResult>`, `IQueryHandler<TQuery, TResult>`; endpoints get the contract from DI.
Cross-cutting steps (validation, later logging) are decorators registered around the handlers by
`AddApplication()`. The "transaction" step is the handler's single `SaveChanges` through `IUnitOfWork` — one command,
one save.

## Consequences
- No external dependency and no license question; handlers are plain classes, easy to test.
- Decorator registration is written by hand (one scanning extension method, covered by `DependencyInjectionTests`).

## Alternatives considered
- **MediatR** — rejected: commercial license, and runtime reflection we don't need.
- **Source-generated mediator** — rejected for now: free and fast, but an extra dependency for what a few interfaces
  do; it can be adopted later behind the same handler interfaces.
