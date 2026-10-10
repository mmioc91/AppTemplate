# Architecture Decision Records

One file per decision. A decision is never deleted or rewritten — when it changes, add a new ADR and mark the old
one `Superseded by NNNN`. The ADRs below are the template's baseline: every project created from it starts with them
and adds its own from 0021 on. Product design (what to build) lives in the project's own docs; these ADRs record how
the code implements it and every choice that had to be made along the way.

## Index

| #    | Decision | Status | Date |
|------|----------|--------|------|
| 0001 | [Clean Architecture, layered by project](0001-clean-architecture-layered-by-project.md) | Accepted | 2026-09-29 |
| 0002 | [Ports live in Application; Domain receives resolved values](0002-ports-in-application.md) | Accepted | 2026-09-29 |
| 0003 | [Strongly-typed IDs](0003-strongly-typed-ids.md) | Accepted | 2026-09-29 |
| 0004 | [DomainException and error codes](0004-domain-exception-and-error-codes.md) | Accepted | 2026-09-29 |
| 0005 | [Smart enums for values with behavior](0005-smart-enums-for-values-with-behavior.md) | Accepted | 2026-09-29 |
| 0006 | [One type per file, folders per aggregate](0006-one-type-per-file-folders-per-aggregate.md) | Accepted | 2026-09-29 |
| 0007 | [Domain errors return 422](0007-domain-errors-return-422.md) | Accepted | 2026-09-29 |
| 0008 | [Result pattern for expected failures](0008-result-pattern-for-expected-failures.md) | Accepted | 2026-09-29 |
| 0009 | [Built-in OpenAPI document](0009-built-in-openapi.md) | Accepted | 2026-09-29 |
| 0010 | [Test project layout](0010-test-project-layout.md) | Accepted | 2026-09-29 |
| 0011 | [No mediator library](0011-no-mediator-library.md) | Accepted | 2026-09-29 |
| 0012 | [Validation errors carry codes and stop the handler chain](0012-validation-errors-carry-codes.md) | Accepted | 2026-09-29 |
| 0013 | [Save outcomes: unique violations, retried commits and concurrency conflicts](0013-save-outcomes-unique-violations-and-conflicts.md) | Accepted | 2026-09-29 |
| 0014 | [Map DTOs explicitly](0014-map-dtos-explicitly.md) | Accepted | 2026-09-29 |
| 0015 | [Private parameterless constructors for EF Core](0015-private-constructors-for-ef.md) | Accepted | 2026-09-29 |
| 0016 | [Value objects are records, mapped as EF complex types](0016-value-objects-are-records-and-complex-types.md) | Accepted | 2026-09-29 |
| 0017 | [Mutation testing on pull requests](0017-mutation-testing-on-pull-requests.md) | Accepted | 2026-09-29 |
| 0018 | [HTTP error contract](0018-http-error-contract.md) | Accepted | 2026-09-29 |
| 0019 | [Separate liveness and readiness health checks](0019-liveness-and-readiness-health-checks.md) | Accepted | 2026-09-29 |
| 0020 | [Missing value-type fields are REQUIRED, not defaults](0020-missing-value-fields-are-required.md) | Accepted | 2026-09-29 |

## Open questions

Not decided yet. When one is settled, it becomes a new ADR and is removed from this list.

- **Authentication scheme: cookie or JWT?** Decide with the first project that has users. An SPA served from the same
  origin as the Api is simplest with ASP.NET Identity + an httpOnly `__Host-` cookie (`SameSite=Lax`, JSON-only bodies
  as CSRF protection; no token storage in the browser); a mobile app or a third-party API client needs bearer tokens
  (JWT or Identity's bearer endpoints). Either way: the user's id comes from the authenticated principal through an
  `ICurrentUser` port, never from the request; a fallback policy requires authentication everywhere and exceptions are
  marked `.AllowAnonymous()`; wrong password, unknown user and lockout answer the same 401 code.
- **Multi-tenancy.** If the product has tenants (workspaces, organizations): every aggregate root carries the tenant
  id; isolation is a *named* global EF query filter fed by an `ITenantContext` port the Api fills from the signed-in
  user — never a tenant id from the request, never a hand-written `WHERE TenantId = …`; `IgnoreQueryFilters()` only
  in system paths without a user; every use case gets a tenant-boundary integration test.
- **Hosting.** Settles with it: forwarded headers behind the platform's proxy (`UseForwardedHeaders` with the proxy's
  known networks — a loose setup lets clients spoof their IP and scheme), `AllowedHosts` set to the real domain in
  `appsettings.Production.json` instead of `*`, which probe uses `/health/live` vs `/health/ready`
  ([0019](0019-liveness-and-readiness-health-checks.md)), and how migrations run as a deploy step.
- **Retried POSTs (idempotency).** A client that re-sends a create request because it never got the first answer gets
  409 for its own resource. The usual fix is an `Idempotency-Key` header (store key + response for a while; the same
  key returns the same response). Decide before a client retries requests automatically.
- **Logging and correlation id.** Structured logging (e.g. Serilog) and a correlation id that ties a 4xx/5xx response
  to its log entries ([0018](0018-http-error-contract.md)).

## Template

```markdown
# NNNN. Title

- **Status:** Proposed | Accepted | Superseded by NNNN
- **Date:** YYYY-MM-DD

## Context
Why a decision was needed.

## Decision
What we chose.

## Consequences
What follows from it — good and bad.

## Alternatives considered
What we rejected and why.
```
