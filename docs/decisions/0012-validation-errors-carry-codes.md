# 0012. Validation errors carry codes and stop the handler chain

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
Input validation with FluentValidation usually throws a `ValidationException` mapped to 400 with field → messages.
We use Results for expected failures ([0008](0008-result-pattern-for-expected-failures.md)), and the client must
not show or parse backend message text.

## Decision
- A validation decorator wraps every command handler: it runs all validators for the command and, if any rule
  fails, returns a `Validation` error **without calling the handler** (no exception).
- Field errors map **input property → error codes**, not messages. Every rule sets its code with
  `.WithErrorCode(ValidationCodes.X)`; a code appears once per field.
- Codes are **generic** (`REQUIRED`, `TOO_SHORT`, `TOO_LONG`, `INVALID`, `NOT_POSITIVE`, `NOT_ALLOWED`): the field says
  *what* is wrong, the code says *how*, so the frontend needs one text per code, not per field. Add a code only for a
  genuinely new kind of problem.
- Api maps it to 400 with `code: VALIDATION_FAILED` and `errors` under the JSON field names
  ([0018](0018-http-error-contract.md)).

## Consequences
- A rule without `.WithErrorCode(...)` leaks FluentValidation's default code (e.g. `NotEmptyValidator`) — validator
  tests must assert the codes.
- Validation runs for every entry point (HTTP, jobs, webhooks), not just HTTP.

## Alternatives considered
- **Throw `ValidationException`** — rejected: an exception for an expected outcome.
- **Field → messages** — rejected: backend text can't be shown in a localized UI.
- **Validate in an Api endpoint filter** — rejected: other entry points would skip it.
- **A code per field and rule (`NAME_REQUIRED`)** — rejected: the frontend would need a text per field.
