# 0004. DomainException and error codes

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
Domain signals a broken invariant with an exception. The client (a frontend, often in another language than the
backend's English messages) must react to *which* rule broke — show a text next to the right field — and parsing
message text breaks on every rewording.

## Decision
- `DomainException` lives in **SharedKernel**, next to the other generic building blocks, so Api can map it
  without referencing anything business-specific.
- It requires a code: its only constructors are `(code, message)` and `(code, message, inner)`; a blank code throws.
- Codes are `UPPER_SNAKE_CASE` `const string`s in an `<Aggregate>Errors` class in the aggregate's folder
  (e.g. `Orders/OrderErrors.cs`). They are API contract: never renamed or reused.
- The message is free English text for logs; it may include values, never PII. It never reaches the client
  ([0018](0018-http-error-contract.md)).
- Tests assert `Code`, never message text.

## Consequences
- A throw without a code doesn't compile.
- The frontend maps codes to its own texts and form fields.
- Message wording is deliberately untested; Stryker skips string mutations ([0017](0017-mutation-testing-on-pull-requests.md)).

## Alternatives considered
- **An exception subclass per error** — rejected: dozens of near-empty classes; a code carries the same information.
- **Codes as an enum** — rejected: codes cross the API boundary as strings anyway, and a `const string` class per
  aggregate keeps each aggregate's errors in its own folder.
- **`DomainException` in Domain** — rejected: it's a generic building block, not business logic.
