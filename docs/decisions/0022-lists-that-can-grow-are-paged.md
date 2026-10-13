# 0022. Lists that can grow are paged, and queries are validated like commands

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
`src/AppTemplate.Api/CLAUDE.md` asked lists that can grow for `{ items, total, page, pageSize }`, but nothing in the
code defined the shape or its limits, and query handlers were registered without a validation decorator "until a
query has input to validate". The first paged query of both projects built from this template (UnifiedPackageTracker
ADR 0042, Podium's list endpoints) hit both gaps on the same day: a bad page size reached the read store, and each
project wrote the same `PagedList` again.

## Decision
- **`PagedList<T>(Items, Total, Page, PageSize)`** in `Application/Common` is the shape of every list that can grow;
  Api serializes it as `{ items, total, page, pageSize }`.
- **`page`** is 1-based and defaults to 1; **`pageSize`** defaults to 20 and is at most 100 (`Paging`). Below 1 →
  `NOT_POSITIVE`; a page size over the maximum → the generic code **`TOO_LARGE`** (a number over its maximum;
  `TOO_LONG` stays for text). A value that isn't a number can't be bound → `400 MALFORMED_REQUEST`. A page past the
  end is `200` with no items and the real `total`.
- **Order:** a stable key (created at, name) with the id as the tie breaker, so a page never reshuffles.
- **Queries are validated like commands:** every query handler is registered wrapped in `QueryValidationDecorator`
  (a query without a validator passes straight through); the concrete handlers can't be resolved on their own.
- A value with a fixed set of names (`?status=active|archived`) is bound as a **string** and checked by the
  validator (`NOT_ALLOWED`), not as an enum: Minimal APIs parse an enum in the query with `Enum.TryParse`, which also
  accepts numbers and flag combinations.
- A list bounded by what a person keeps (an address book, tens of rows) may stay a plain array; the endpoint's ADR
  says so.

## Consequences
- One list shape every frontend can reuse; one validation path for commands and queries.
- Two statements per page (count, then the page); under concurrent writes the total can be off by the rows written in
  between — acceptable for a list.
- The OpenAPI document shows a plain string for a name-valued query parameter; listing its allowed values for a
  generated client is a separate decision.
- An oversized page size is refused, not clamped: clamping hides a client bug and answers with less than was asked.

## Alternatives considered
- **Register queries undecorated until one needs validation** (the template's previous state) — rejected: the first
  paged query needed it on day one in both projects.
- **Clamp an oversized page size to the maximum** — rejected: it hides a client bug.
- **Cursor paging** — deferred: the lists are small and the frontends page by number; a cursor can be added per
  endpoint later without changing the shape of the others.
