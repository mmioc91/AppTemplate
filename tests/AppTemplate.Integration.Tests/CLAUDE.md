# Integration tests

See root `CLAUDE.md`, `src/AppTemplate.Infrastructure/CLAUDE.md` and `src/AppTemplate.Api/CLAUDE.md` for the rules
this project tests — it covers both database-level integration tests and Api-level functional tests.

## Rules
- Real Postgres via **Testcontainers.PostgreSql** — never `UseInMemoryDatabase`, never mock `DbContext`.
- HTTP-level tests use `WebApplicationFactory<Program>` (`Microsoft.AspNetCore.Mvc.Testing`): 401 without a token,
  403 with the wrong role, `ProblemDetails` mapping, happy path.
- Shouldly for assertions.
- Slower than unit tests by nature — this is exactly why `.githooks/pre-push` intentionally skips this project and
  leaves it to CI.
- `Program` (top-level statements in Api) is `internal` by default — visible here via
  `[assembly: InternalsVisibleTo("AppTemplate.Integration.Tests")]` in `src/AppTemplate.Api/AssemblyInfo.cs`. Keep
  that attribute in sync if the test project is ever renamed.
