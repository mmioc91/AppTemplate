# 0008. Result pattern for expected failures

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
Use cases have expected outcomes that aren't success — the thing doesn't exist, it already exists, the caller
isn't allowed. Throwing typed exceptions (`NotFoundException`, `ConflictException`) for them hides those outcomes
from the use case's signature and uses exceptions for control flow.

## Decision
Use cases return a result: `Result` when there is no value, `Result<T>` otherwise (`Application/Common`). A failure
is an `Error` with a **code** (same `UPPER_SNAKE_CASE` convention as [0004](0004-domain-exception-and-error-codes.md))
and a **kind** that Api maps to HTTP:

| Kind       | HTTP | Examples                                                   |
|------------|------|------------------------------------------------------------|
| Validation | 400  | input shape ([0012](0012-validation-errors-carry-codes.md)) |
| NotFound   | 404  | doesn't exist, or belongs to someone else                  |
| Conflict   | 409  | duplicate, optimistic-concurrency conflict (user action)   |
| Forbidden  | 403  | plan limit reached, not a member                           |

Both result types convert implicitly from an `Error` (and `Result<T>` from a value), so a handler writes
`return Error.NotFound(...)` or `return response;`. A successful `Result<T>` never holds `null`. `DomainException`
stays for invariant breaches → 422 ([0007](0007-domain-errors-return-422.md)).

## Consequences
- Expected outcomes are visible in use-case signatures instead of hidden in `throw` statements.
- Every error the client sees — Result error or domain exception — carries a `code`.
- Two result types and two command-handler interfaces (`ICommandHandler<TCommand>` and
  `ICommandHandler<TCommand, TResult>`), each with its validation decorator; the validation itself is shared.

## Alternatives considered
- **Typed exceptions** — rejected: exceptions for control flow, and outcomes invisible in signatures.
- **Only `Result<T>`, with a dummy value type for commands that return nothing** — rejected: every such handler would
  return a meaningless value.
- **A result library (FluentResults, ErrorOr, OneOf)** — rejected: two small classes cover it, and the error shape
  (code + kind + field errors) is ours anyway.
