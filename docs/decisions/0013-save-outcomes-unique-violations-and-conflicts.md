# 0013. Save outcomes: unique violations, retried commits and concurrency conflicts

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
A use case checks "does this already exist?" before saving, but two identical requests at the same moment both pass
the check; the database's unique index rejects the second save — a provider exception that would answer 500.
Npgsql's retrying execution strategy (`EnableRetryOnFailure`) adds a twist: if the connection breaks *after* Postgres
committed, EF retries the whole save, and the retried insert hits the unique index with its own row. Separately,
optimistic concurrency (`xmin`) makes EF throw `DbUpdateConcurrencyException` when a loaded aggregate changed.

## Decision
- `IUnitOfWork.SaveChangesAsync` never leaks provider exceptions for expected outcomes: a unique violation (SQLSTATE
  `23505`) becomes Application's `UniqueConstraintViolationException`, a concurrency conflict becomes
  `ConcurrencyConflictException`. Anything else (e.g. a foreign-key violation) is a bug and surfaces as it is.
- **Unique violation → decide by the database, not the constraint name.** The use case catches it and checks: is
  *our own* row there (by the id we generated)? Then the save had succeeded — success. Is a row with the same natural
  key there? Then it's a real duplicate — the same Conflict as its own pre-check. Otherwise rethrow.
- **Concurrency conflict:** a user action answers Conflict (409); a background process reloads and retries.
- The retrying strategy stays; an explicit transaction must run inside `strategy.ExecuteAsync(...)`, or EF throws.

## Consequences
- A race gives the client the same 409 and code as a sequential duplicate; a lost acknowledgement doesn't turn a
  successful insert into an error.
- A write use case that can hit a unique index needs an "is it my own row?" lookup (`ExistsByIdAsync`).
- A lost acknowledgement on an **update** is not recognized (the retried `UPDATE` carries the old `xmin`) and answers
  409; the client, reloading, sees its change saved. Accepted as rare.

## Alternatives considered
- **`SaveChangesAsync` returns a Result** — rejected: every caller would handle failures only a few writes can
  produce.
- **Map the violation centrally in Api** — rejected: Api can't know which use-case code applies.
- **Turn off retries** — rejected: transient failures on a managed Postgres are common.
- **Match the constraint name in the handler** — rejected: couples Application to database naming, and can't tell a
  retried commit from a duplicate.
