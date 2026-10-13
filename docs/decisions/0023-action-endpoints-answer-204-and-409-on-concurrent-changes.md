# 0023. Action endpoints answer 204, repeat as a no-op and answer 409 to a concurrent change

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
An action on an aggregate (archive, confirm, cancel, snooze) is a `POST` sub-route that changes a loaded aggregate and
is the first place a project meets `xmin` conflicts over HTTP ([0013](0013-save-outcomes-unique-violations-and-conflicts.md)
translates them, but names no code and no answer for a repeated or concurrent action). UnifiedPackageTracker
(ADR 0043) and Podium (ADR 0032, 0061) settled the same contract independently.

## Decision
- **`POST /…/{id}/<action>` answers `204 No Content`** (a `Result` from `ICommandHandler<TCommand>`): the client
  reloads the resource afterwards anyway, and an empty answer needs no DTO per action. It declares 401/403/404/409/422
  for OpenAPI.
- **A repeated action is a no-op:** a request repeated after the first one was answered gets `204` again (the domain
  method ignores a repeat that changes nothing); a state that forbids the action answers `422` with the domain code.
- **A concurrent change answers `409 CONCURRENCY_CONFLICT`** (`CommonErrors.ConcurrencyConflict()`): two requests in
  flight at once both load the same `xmin`, the second one's save throws `ConcurrencyConflictException`, the client
  reloads and decides again. No retry on the server for a user action.
- **Checks run 404s first** (the aggregate, then a child id from the route), **then the domain rules** (422): an
  archived aggregate with an unknown child id answers `404`, not `422`.
- **A rule that depends on "now"** (a date must be in the future) **is a domain `422`**, not a validator `400`:
  validators check the shape of the input and never repeat a domain rule.

## Consequences
- No DTO per action; the frontend reloads the detail after an action and switches on `CONCURRENCY_CONFLICT` to say
  "this changed meanwhile, reload".
- A repeated action is safe once the first was answered; concurrent duplicates get `409` rather than a second
  success.
- `CommonErrors` holds the codes several use cases share; the conflict is the first.

## Alternatives considered
- **`200` with the changed resource** — rejected for now: it saves the frontend one `GET` but adds a DTO per action;
  it can be added later without breaking clients that ignore the body.
- **Validate "now" rules in the validator (400)** — rejected: it duplicates the domain rule.
- **Answer `204` to a concurrent duplicate** — rejected: it needs a re-read after the conflict to tell "already done"
  from "changed to something else", and isn't needed while the frontend disables an action's button during its
  request.
