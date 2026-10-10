# 0017. Mutation testing on pull requests

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
Coverage says a line ran, not that a test would fail if it were wrong. Stryker.NET mutation testing finds tests that
can't go red, but a full run is too slow for `pre-push`, and a manual run is easy to forget.

## Decision
- `stryker-config.json` in `src/AppTemplate.Domain` and `src/AppTemplate.Application`; run from those folders, never
  the repo root (from there it mutates every project).
- CI job `mutation` in `ci-backend.yml`: a matrix over `Domain` and `Application`, **pull requests only**, in
  parallel with `build-test`, `--since` the base branch (only code changed in the PR is mutated), one HTML report
  artifact per project (`mutation-report-<Project>`), `fail-fast: false`.
- **Report-only** (`break: 0`) until a few reports show typical scores.
- String mutations are skipped (`"ignore-mutations": ["string"]`): they are message text, which is never asserted
  ([0004](0004-domain-exception-and-error-codes.md)), and only hide the real survivors. A behavior-carrying string
  must be a `const` or pinned explicitly by its tests.
- Infrastructure and Api are not mutation-tested: their tests need Postgres or the whole Api, so every mutant would
  rerun them.

## Consequences
- Every PR touching Domain/Application gets a weak-test signal without extra effort; PR wall time doesn't grow.
- A score threshold that fails PRs is a separate, later decision.

## Alternatives considered
- **`pre-push` hook** — rejected: minutes of waiting on every push.
- **Full run on every PR** — rejected: cost grows with the code base; `--since` keeps it proportional to the change.
- **Keep it manual** — rejected: easy to forget.
