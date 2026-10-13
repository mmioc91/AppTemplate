# 0030. No Idempotency-Key for creates until a client retries them automatically

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
An open question asked whether create requests need an `Idempotency-Key` header: a client that sends a create again
because its answer was lost gets 409 for its own resource, or a second row. The usual fix stores each key with its
response for a while and answers a repeated key with the stored response. The one project that went to production
from this template answered it after its first months of use.

## Decision
**Not now.** A person's action has its outcome on screen: a lost answer shows as an error, the list it belongs to shows
whether it landed, and where a natural key must be unique the second try answers the same Conflict as the pre-check
([0013](0013-save-outcomes-unique-violations-and-conflicts.md)) instead of a second row. A frontend disables a form's
button while its request runs. Where a client retries by itself (an offline queue), it carries only what is
idempotent already: a repeated action is a no-op ([0023](0023-action-endpoints-answer-204-and-409-on-concurrent-changes.md)),
and a child create may carry a client-generated id, so the same id on the same parent returns the existing record.

Revisit when any of these comes: a client that retries creates by itself (a queued create offline, a mobile app with
background sync), integrations calling the Api, or a create without a natural unique key that people report doubling.
The fix then is the client's own id in the body, where the resource can take one; an `Idempotency-Key` store
otherwise.

## Consequences
- No store of keys and responses to run and purge.
- A double-submitted create without a unique key can land twice; the duplicate is removed by hand until the fix above
  is needed.

## Alternatives considered
- **`Idempotency-Key` on every POST now** — rejected: a store and a middleware for retries nobody makes.
- **Client ids on every create** — deferred to when a create is retried automatically.
