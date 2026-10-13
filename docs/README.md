# Documentation map

Entry point into the documentation: what to build, in what order, and where each decision lives. The template
ships this file with placeholders — replace them when it becomes a project and keep the shape, so the next reader
(a person or Claude) finds the same things in the same places.

## How to read (by goal)

| I want to… | Read |
|---|---|
| understand the product and its users | the product brief (`spec.md`, or wherever it lives — link it here) |
| build the backend | `domain.md` → `application.md` → `infrastructure.md` → `api.md` |
| build the frontend | `frontend-brief.md` — the hand-off to the frontend work: screens and flows, pointing at the API contract |
| know how the code is built, and why | [`decisions/`](decisions/README.md) — one ADR per decision, open questions at the end |
| know what is done and what comes next | [`implementation-plan.md`](implementation-plan.md) |
| map the words users see to the names the code uses | [`ubiquitous-language.md`](ubiquitous-language.md) |

## Document map

Suggested names; what matters is that each kind of truth has one home.

| File | Holds | The truth for |
|---|---|---|
| `spec.md` | market, users, the feature set, the MVP line | *what* and *why* |
| `domain.md` | aggregates, their children and value objects, invariants, boundaries | the model |
| `application.md` | use cases, ports, cross-aggregate rules | the orchestration |
| `infrastructure.md` | persistence mapping, external systems, jobs | the adapters |
| `/openapi/v1.json` (generated) | endpoints and DTO shapes, as the typed client is generated from it (ADR 0009, 0027) | the HTTP contract |
| `api.md` | what the OpenAPI document cannot carry: the error codes each endpoint answers, flows across endpoints | the HTTP contract's rules |
| `frontend-brief.md` | screens and flows; points at the two rows above instead of copying them | the hand-off |
| `decisions/` | ADRs | *how*, wherever a doc above and the code disagree |

Design docs written before the fork say *what*; the root `CLAUDE.md` and the ADRs say *how*, and win on any
conflict. Record the known differences in an ADR when you fork (root `README.md` → "New project from this template"),
so every later conflict has one answer.

## Build order

1. The solution skeleton — the template.
2. One vertical slice at a time ([`implementation-plan.md`](implementation-plan.md)): Domain → Application →
   Infrastructure (mapping + migration) → Api, each slice ending with something that works over HTTP.
3. The frontend from the frontend brief, as soon as the first endpoints stand; the contract doc (`api.md`) is updated
   in every change that touches an endpoint, and the client is regenerated from the OpenAPI document.
