# 0024. New aggregates may be created in the transaction of the change they serve

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
`src/AppTemplate.Domain/CLAUDE.md` says one transaction changes one aggregate. The first use case that strains the
rule looks the same in every project: a user enters something that is both a new aggregate of its own and an entry on
an existing one — a person created for a roster entry, a customer created with their first order, the matches a
format generates for a category's structure (Podium met all three). Saving the new aggregate first and the entry second
leaves an orphan whenever the second save fails (a taken number, a frozen parent, a concurrent edit). Asking the
client for two calls only moves the orphan to the client.

## Decision
- A use case may **create** new aggregates in the same `SaveChangesAsync` as the one existing aggregate it changes,
  when the new ones exist only for that change.
- It still **changes at most one existing aggregate**. Two existing aggregates changed together remain out of bounds;
  their consistency goes through a second use case or a deferred side effect.
- The domain rule runs before anything is added: the existing aggregate's method validates the entry for the new
  aggregate's id, then the new aggregate is added to its repository, then one save.
- The existing aggregate's `xmin` is the concurrency check that matters; the new rows are inserts and can't be stale.

## Consequences
- No orphans: a refused entry leaves nothing behind.
- A failed save loses the new aggregates with it, which is what the client expects from one request.
- Inserts of other aggregates widen the transaction a little; they touch no row anyone else can hold.

## Alternatives considered
- **Two transactions** (the new aggregate, then the entry) — rejected: the orphan on every refused entry.
- **Two client calls** (`POST /people`, then `POST /teams/{id}/members` with the id) — rejected: the same orphan,
  created by the client, and a worse API for the form.
- **The entry holds a copy of the data instead of an id** — rejected: everything that later refers to the new thing
  (sides, statistics, history) needs its identity.
