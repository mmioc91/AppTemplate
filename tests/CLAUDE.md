# Tests (shared conventions)

Applies to every project under `tests/`. Project-specific rules live in each `tests/*/CLAUDE.md`; which kind of test
goes where is ADR 0010.

## Rules
- xUnit + Shouldly (not FluentAssertions). Explicit types per `.editorconfig` — no `var` unless the type is apparent
  from `new` or a static factory on the same type (root `CLAUDE.md` → `var` gotcha).
- Naming: `Method_Scenario_ExpectedOutcome` (e.g. `Ship_WhenAlreadyShipped_Throws`) — names should read like a spec.
- Structure: `// Arrange` / `// Act` / `// Assert`. One behavior per test; multiple asserts only when they describe
  one outcome.
- No logic in tests (loops/conditionals computing expected values) — use `[Theory]` + `[InlineData]`/`[MemberData]`
  for the same rule over many inputs instead of copy-pasted `[Fact]`s.
- Cover what matters, not vanity coverage: happy path, each branch, boundary values, null/empty input, failure paths.
- Every test must be able to go red — a test that can't fail is worse than no test. Mutation testing (Stryker, ADR
  0017) is how we check.
- Stryker on part of a large file: `--mutate "**/Orders/Order.cs{start..end}"` takes **character offsets** into the
  file, not line numbers — line numbers silently match nothing ("0 mutants will be tested").
- Deterministic time: Domain receives timestamps as parameters; where a `TimeProvider` is involved use
  `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) with a fixed instant — never the real clock. Tests
  must not depend on today's date.
- Assert error `code`s, never message text (ADR 0004).
