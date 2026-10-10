# 0001. Clean Architecture, layered by project

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
A new backend needs one layout rule that every feature follows. The two usual candidates are vertical slices
(one folder per feature holding its endpoint, handler and data access, often with only Domain/Infrastructure/Api
projects) and Clean Architecture layered by project, where the compiler and architecture tests enforce which layer
may reference which.

## Decision
Five projects, dependencies pointing inward only: **SharedKernel ← Domain ← Application ← Infrastructure ← Api**
(Api also references Application as the composition root). `tests/AppTemplate.Architecture.Tests` (NetArchTest)
enforces the direction and keeps infrastructure packages out of SharedKernel, Domain and Application.
Within Application, code is still grouped by feature (`Orders/PlaceOrder/…`), so a use case's command, validator,
handler and response sit together; its endpoint lives in Api.

## Consequences
- Business rules live in Domain once, whichever use case needs them.
- A layer violation is a build failure, not a review comment.
- A few more files per feature than a vertical slice; a design doc written for slices has to be mapped onto the
  layers (record non-obvious mappings as ADRs).

## Alternatives considered
- **Vertical slices with 3 projects** — rejected: nothing stops a slice from putting business rules next to its
  data access, and there is no build-time guard for the dependency direction.
