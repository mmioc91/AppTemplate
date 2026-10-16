# Implementation plan

The ordered steps from the template to a working product. **What** to build comes from the design docs
([`README.md`](README.md) is the map); **how** follows the root `CLAUDE.md` and the ADRs. Tick a step off in the
same change that completes it; if a step turns out wrong, change this plan in that change.

## Approach: vertical slices in time, not in the code

The product is built feature by feature, not layer by layer: each slice goes through every layer it needs — Domain →
Application → Infrastructure (EF mapping + migration) → Api — and ends with something that works over HTTP. The code
stays layered by project (ADR 0001); only the order of work is sliced. That way every domain decision is checked
against a real use case and a real database mapping on a small piece, instead of after the whole domain is written.
Pure logic without a database (a scheduling engine, a pricing rule set) gets steps of its own, but only when a slice
needs it.

## How every step is done

- One step = one branch (`feat/...`, `docs/...`, …) = one coherent change: code + its tests, `dotnet build` and
  `dotnet test` green, Conventional Commits, a pull request squash-merged into `main`.
- Tests go with the code they cover, at every layer the slice touches: Domain unit tests, Application tests with fake
  ports, integration tests (Testcontainers + `WebApplicationFactory`) for persistence and endpoints, each use case
  through the checklist of ADR 0010 (happy path, validation, auth, not found, persisted state, concurrency,
  idempotency). Error **codes** asserted, never messages. Domain and Application changes also run Stryker on the pull
  request, report-only (ADR 0017): a surviving mutant is a test to fix, not a blocker.
- A decision that isn't obvious from the code gets an ADR in the same change. An open question in
  `decisions/README.md` is settled by the step that first needs the answer, not "later".
- A step that adds or changes an endpoint, a DTO or an error code updates the hand-written contract doc (the error
  codes per endpoint and the flows the OpenAPI document cannot carry) in the same change; the frontend brief points at
  it and at the OpenAPI document instead of copying them.
- A large slice is split into a few steps (domain first, then persistence + endpoints) — each one still leaves
  `main` green. A step that touches more than one aggregate and one use case is too big.

## Done

| # | Step | PR | ADRs |
|---|---|---|---|
| 0 | Fork from the template, rename, describe the product in `CLAUDE.md` and `docs/README.md` | | |

## Next

| # | Step | Notes |
|---|---|---|
| 1 | The first aggregate and its first use case, end to end over HTTP | sign-in is built (ADR 0031): the user comes from `ICurrentUser`; add a role and a policy only if the use case needs one |

## Deferred

- What is consciously left out of the MVP, and why — so nobody re-asks.
