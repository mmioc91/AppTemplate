# Domain tests

See root `CLAUDE.md` and `src/AppTemplate.Domain/CLAUDE.md` for the rules this project tests.

## Rules
- xUnit + Shouldly (not FluentAssertions — see root `CLAUDE.md`).
- Pure logic only: no I/O, no EF Core, no framework references — same purity rule as the Domain project itself.
- Test invariants directly (e.g. calling a state-changing method a second time should throw where that's an
  invariant), not incidental implementation detail.
