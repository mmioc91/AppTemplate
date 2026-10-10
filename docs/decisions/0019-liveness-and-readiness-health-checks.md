# 0019. Separate liveness and readiness health checks

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
A single `/health` that checks the database is often used both to restart instances and to route traffic. A
platform that restarts an instance whose probe fails would then restart *every* instance during a database outage —
which fixes nothing. Separately, when production applies migrations as its own deploy step, nothing notices a deploy
that skipped it.

## Decision
- **`/health/live`** runs no checks: it answers while the process can serve requests. Use it for anything that
  restarts or replaces instances.
- **`/health/ready`** runs the checks tagged `ready` (`DependencyInjection.ReadinessTag` in Infrastructure): the
  database is reachable, and it has no pending migrations. Use it for load-balancer rotation and as the deploy gate.
- Both are anonymous and outside `/api/v1`.
- A check for an external service is not a readiness gate by default: its outage doesn't stop this instance from
  serving everything else.

## Consequences
- A database outage takes instances out of rotation without restarting them.
- A deploy that forgot its migrations never becomes ready.
- Two paths instead of one; hosting must point each probe at the right one.

## Alternatives considered
- **One `/health` with every check** — rejected for the restart cascade above.
- **Check migrations only in the deploy pipeline** — rejected: the readiness check covers it at no extra cost and
  also catches a manual deploy.
