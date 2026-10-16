# Architecture Decision Records

One file per decision. A decision is never deleted or rewritten — when it changes, add a new ADR and mark the old
one `Superseded by NNNN`. The ADRs below are the template's baseline: every project created from it starts with them
and adds its own after the last one here. Product design (what to build) lives in the project's own docs; these ADRs record how
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
| 0021 | [Git stores text files with LF](0021-git-stores-text-files-with-lf.md) | Accepted | 2026-10-13 |
| 0022 | [Lists that can grow are paged, and queries are validated like commands](0022-lists-that-can-grow-are-paged.md) | Accepted | 2026-10-13 |
| 0023 | [Action endpoints answer 204, repeat as a no-op and answer 409 to a concurrent change](0023-action-endpoints-answer-204-and-409-on-concurrent-changes.md) | Accepted | 2026-10-13 |
| 0024 | [New aggregates may be created in the transaction of the change they serve](0024-new-aggregates-created-with-the-change-they-serve.md) | Accepted | 2026-10-13 |
| 0025 | [Single-value value objects are stored through value converters](0025-single-value-objects-stored-through-converters.md) | Accepted | 2026-10-13 |
| 0026 | [Unauthorized is an error kind, and 429 has its own code](0026-unauthorized-kind-and-too-many-requests-code.md) | Accepted | 2026-10-13 |
| 0027 | [Strict JSON, and an OpenAPI document fit for a generated client](0027-strict-json-and-openapi-for-a-generated-client.md) | Accepted | 2026-10-13 |
| 0028 | [Logs are the built-in logging, JSON lines in production, tied to errors by the W3C trace id](0028-logging-and-the-trace-id.md) | Accepted | 2026-10-13 |
| 0029 | [Hosting: one origin behind a reverse proxy, migrations as a deploy step](0029-hosting-behind-a-proxy-with-migrations-as-a-deploy-step.md) | Accepted | 2026-10-13 |
| 0030 | [No Idempotency-Key for creates until a client retries them automatically](0030-no-idempotency-key-until-a-client-retries-creates.md) | Accepted | 2026-10-13 |
| 0031 | [Authentication: ASP.NET Core Identity with JWT bearer tokens](0031-identity-with-jwt-bearer-tokens.md) | Accepted | 2026-10-14 |
| 0032 | [The web app: Angular, translated through i18n, on a generated client](0032-angular-frontend-setup.md) | Accepted | 2026-10-16 |

## Open questions

Not decided yet. When one is settled, it becomes a new ADR and is removed from this list.

- **Multi-tenancy.** If the product has tenants (workspaces, organizations): every aggregate root carries the tenant
  id; isolation is a *named* global EF query filter fed by a scoped `ITenantContext` the Api fills on every request
  from the signed-in user's membership — never a tenant id from the request or a claim, never a hand-written
  `WHERE TenantId = …`; an id of another aggregate taken from a request is confirmed global or in the current tenant
  before it is stored; only system paths without a user (webhooks, jobs) bypass the filter, through their own
  `ISystem…` ports that ignore just the named filter, which an architecture test limits to named handlers; an endpoint
  that must work without a tenant (logout) says so with an attribute; every use case gets a tenant-boundary
  integration test (two users, neither sees the other's data). A product without tenants scopes loads by owner
  instead (`GetAsync(id, owner)`), and another owner's data answers 404, never 403.
- **Hosting platform.** The code side is settled ([0029](0029-hosting-behind-a-proxy-with-migrations-as-a-deploy-step.md));
  what remains is where it runs: one Linux VM with Docker Compose and Caddy (proven), or a managed platform. It
  arrives with the `deploy/` files and their own ADR, together with the security headers and Content-Security-Policy
  the proxy sends.
- **Account deletion and GDPR erasure.** A real delete, not a soft one — erasure means the personal data is gone — as
  an explicit flow that deletes the user's data, their tenant (if they are its last owner) and the account in order;
  the database refuses shortcuts (`ON DELETE RESTRICT`). Open: what happens to a shared tenant when a non-last owner
  leaves.
- **Operator access.** Admin tools (a job dashboard, platform settings) stay in Development until there is an operator
  role or a separate, network-restricted host: the product's users are customers, and such tools show everyone's data.
- **Count-then-insert limits.** A quota that counts rows and then inserts can be exceeded by one under concurrent
  requests. Options: a per-tenant advisory lock or a serializable transaction around count + insert (inside the
  retrying execution strategy, [0013](0013-save-outcomes-unique-violations-and-conflicts.md)), or accept a soft limit.
  Settle before the first limit is enforced.

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
