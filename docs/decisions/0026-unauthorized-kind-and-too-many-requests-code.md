# 0026. Unauthorized is an error kind, and 429 has its own code

- **Status:** Accepted — extends [0008](0008-result-pattern-for-expected-failures.md) and
  [0018](0018-http-error-contract.md)
- **Date:** 2026-10-13

## Context
0008 maps the kinds of expected failure a use case can return to HTTP: Validation 400, NotFound 404, Conflict 409,
Forbidden 403. 0018 gives every ProblemDetails the framework writes a code from its status, with every other 4xx as
`REQUEST_REJECTED`. Both projects built from this template needed two more answers on their first use cases: a use
case that checks credentials (a login, a refresh token) had no kind that says 401, and a rate limiter answers 429,
which a client should recognise and back off from (`Retry-After`) rather than treat as a generic rejection.

## Decision
- **`ErrorKind.Unauthorized`** and `Error.Unauthorized(code, message)`, mapped to **401** by `ErrorResults` with the
  title "Authentication failed." The use case's own code travels as `code`; a wrong password, an unknown user and a
  lockout answer the same code, so nothing reveals which accounts exist (the auth ADR names it).
- The framework's own 401 — no credentials at all — keeps `UNAUTHENTICATED` from 0018. A use case open to anonymous
  callers that needs a signed-in user answers that same code, so a client with an expired token refreshes on either.
- **429 gets `TOO_MANY_REQUESTS`** in `ProblemCodes.ForStatus`; `REQUEST_REJECTED` stays for the rest.

## Consequences
- `ErrorResults` maps five kinds; 0008's table reads with this row added, 0018's with the 429 row.
- A rate limiter, when a project adds one, needs nothing in the error contract; it adds `Retry-After`.
- Clients switch on `UNAUTHENTICATED` and the auth codes (401) and on `TOO_MANY_REQUESTS` (429) like on any other code.

## Alternatives considered
- **Throw from the use case and map in an exception handler** — rejected: a failed login is an expected outcome
  (0008), not an exception.
- **A 401 only from the framework, no kind** — rejected: a login would have to answer 400 or 403 for a wrong
  password, which clients would read as a form error or a permission problem.
- **Keep 429 as `REQUEST_REJECTED`** — rejected: a client should wait and retry on 429, not show a generic error.
