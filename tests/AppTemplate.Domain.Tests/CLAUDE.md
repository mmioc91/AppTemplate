# Domain tests

See `tests/CLAUDE.md` for shared test conventions, and root `CLAUDE.md` + `src/AppTemplate.Domain/CLAUDE.md`
for the rules this project tests.

## Rules
- Pure logic only: no I/O, no EF Core, no framework references — same purity rule as the Domain project itself.
- No mocks: Domain has no dependencies to substitute. If something needs a mock, it probably doesn't belong in Domain.
- Test invariants directly (e.g. calling a state-changing method a second time should throw where that's an
  invariant), not incidental implementation detail. Every invariant gets a test for its `DomainException` path,
  asserting the exception's `Code` (e.g. `exception.Code.ShouldBe(OrderErrors.AlreadyShipped)`), never its message
  text.
- Smart enum values with behavior (`IsTerminal`, `DefaultDays`, …) are domain rules too: test every value.
- Folders mirror Domain's aggregate folders (`Orders/OrderTests.cs`).
- SharedKernel has no test project: its building blocks (`Entity<TId>` equality, `Enumeration<TEnum>` lookups,
  `DomainException`) are tested here, at the project root (`EntityTests`, `EnumerationTests`,
  `DomainExceptionTests`).
- **Mutation testing** (Stryker.NET, config at `src/AppTemplate.Domain/stryker-config.json`): run
  `dotnet tool run dotnet-stryker` from `src/AppTemplate.Domain` (not the repo root — from there it ignores the
  config and mutates every project). Too slow for `dotnet build`/`dotnet test` or `pre-push`. CI runs it on every PR
  (`mutation (Domain)` in the `mutation` matrix job of `ci-backend.yml`, ADR 0017), limited to Domain code changed in
  that PR (`--since`); the HTML report is uploaded as the `mutation-report-Domain` artifact. Report-only (`break: 0`);
  no enforced threshold yet. String mutations are skipped (exception messages are never asserted). Surviving mutants
  usually point to tests that can't go red.
