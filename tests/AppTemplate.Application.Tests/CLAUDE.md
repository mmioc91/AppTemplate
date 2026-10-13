# Application tests

See `tests/CLAUDE.md` for shared test conventions, and root `CLAUDE.md` + `src/AppTemplate.Application/CLAUDE.md`
for the rules this project tests.

## Rules
- Fake/in-memory ports via **NSubstitute** — no real database, no `WebApplicationFactory`.
- Assert the outcome (Result/exception) and that the right port methods were called — not incidental
  implementation detail. Validator tests assert the error codes (`FluentValidation.TestHelper`).
- Keep this project small: only handler orchestration worth testing without a database, plus validators and the
  shared types in `Common/`. Each use case's end-to-end test — real Postgres, persistence, HTTP — lives in
  `Integration.Tests` (ADR 0010).
- **`DependencyInjectionTests`** resolves every registered handler through `AddApplication()`: when a new handler
  needs a port, register it there as a substitute (`BuildServices`) or the test fails — which is the point. The
  sample use cases in `Samples/` exercise the registration itself (every handler kind, decorated, concrete type not
  resolvable); leave them in place.
- **Mutation testing** (Stryker.NET, config at `src/AppTemplate.Application/stryker-config.json`): run
  `dotnet tool run dotnet-stryker` from `src/AppTemplate.Application` (never the repo root). CI runs it on every PR for
  changed Application code (`mutation (Application)`, artifact `mutation-report-Application`; ADR 0017); report-only,
  and skipped with a notice past 40 changed C# files (run it locally on the files that matter then).
  String mutations are skipped (message text is never asserted). Anything surviving is a test that can't go red.
