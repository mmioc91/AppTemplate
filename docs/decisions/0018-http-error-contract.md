# 0018. HTTP error contract

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
Errors from Domain ([0004](0004-domain-exception-and-error-codes.md), 422) and Application
([0008](0008-result-pattern-for-expected-failures.md)) carry codes, but the framework also answers on its own —
malformed JSON, an unknown route, an unhandled exception — without one. Messages are English, may carry values,
and a localized client can't show them anyway.

## Decision
- **Every ProblemDetails carries `code`.** Errors from Domain and Application bring their own; anything the framework
  writes gets one from its status (`ProblemCodes.ForStatus`): 400 `MALFORMED_REQUEST`, 401 `UNAUTHENTICATED`, 403
  `FORBIDDEN`, 404 `NOT_FOUND`, 405 `METHOD_NOT_ALLOWED`, 415 `UNSUPPORTED_MEDIA_TYPE`, other 4xx `REQUEST_REJECTED`,
  5xx `UNEXPECTED_ERROR`. A well-formed (`UPPER_SNAKE_CASE`) code already set is never overwritten; a library's own
  code in another format (e.g. `UnsupportedApiVersion`) is replaced by the one for its status.
- **A 422 is always a 422:** if the client doesn't accept JSON, it gets the status without a body, never a 500.
- **Messages never reach the client.** The response has `status`, `title` (a fixed English phrase per kind) and
  `code`, no `detail`; messages go to the log.
- **One mapping each:** `DomainExceptionHandler` for 422, `BadRequestExceptionHandler` for unreadable requests (in
  Development, Minimal API binding throws instead of answering), `ErrorResults.ToProblem()` for Result failures,
  `UseExceptionHandler` + `UseStatusCodePages` for the rest. Endpoints never build ProblemDetails.
- **Validation `errors` use JSON field names:** C# paths (`Address.PostalCode`) become the names the client sent
  (`address.postalCode`), so a form can attach each error to its field.
- **500 has no details**, in every environment.

## Consequences
- A client's error interceptor can always switch on `code`, including for framework errors.
- Diagnosing a 4xx needs the log, not the response; a correlation id ties the two together once logging is set up.
- Two promises need an endpoint with a body to be tested, and are tested with the first one: malformed JSON → 400
  `MALFORMED_REQUEST`, and an unhandled exception → 500 `UNEXPECTED_ERROR` without details.

## Alternatives considered
- **Send the message as `detail`** — rejected: English, may carry values, and invites clients to parse text.
- **Codes only for Domain/Application errors** — rejected: clients would need a second path for code-less
  responses.
