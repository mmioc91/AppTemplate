# Integration tests

See `tests/CLAUDE.md` for shared test conventions, and root `CLAUDE.md`, `src/AppTemplate.Infrastructure/CLAUDE.md`
and `src/AppTemplate.Api/CLAUDE.md` for the rules this project tests — it covers both database-level integration
tests and Api-level functional tests.

## Rules
- Real Postgres via **Testcontainers.PostgreSql** (Docker must be running) — never `UseInMemoryDatabase` (build
  error, see root `BannedSymbols.txt`), never mock `DbContext`. Once aggregate roots carry an `xmin` concurrency
  token, test the actual conflict path (two scopes load, one saves, the other gets `ConcurrencyConflictException`),
  not just the happy path.
- **Checklist per use case** (ADR 0010): happy path · validation (400 + `errors`) · auth (401/403 once auth exists) ·
  not found / another owner's data (404) · persisted state (assert the database, not just the response) ·
  concurrency conflict where relevant · idempotency for anything retried (webhooks, jobs).
- **With the first aggregate:** add persistence tests for `UnitOfWork` — a real duplicate insert throws
  `UniqueConstraintViolationException`, a real stale update throws `ConcurrencyConflictException` (ADR 0013). With the
  first migration: a test that an unmigrated database answers `/health/ready` with 503 while `/health/live` stays 200
  (`new ApiFactory(await postgres.CreateEmptyDatabaseAsync(), Environments.Production)`; without a migration nothing
  is pending, so the test can't exist yet). With the first endpoint that takes a body: malformed JSON → 400
  `MALFORMED_REQUEST`, and an unhandled exception → 500 `UNEXPECTED_ERROR` without details (ADR 0018).
- External services are never called live — stub the HTTP transport. Each test arranges its own data with fresh
  ids; no ordering dependencies.
- **Persistence tests** join `[Collection(PostgresCollection.Name)]`: one `postgres:17` container per run, migrated
  once by `PostgresFixture` (which is also the "migrations apply to an empty database" check). Build services with
  `postgres.CreateServices()` — the real `AddInfrastructure`; `postgres.CreateServices(configure)` adds what a test
  needs on top (a fake clock, options the Api would bind) — and isolate each test with fresh ids instead of
  resetting the database. A test about a database before its first migration (readiness, a seed) gets its own from
  `postgres.CreateEmptyDatabaseAsync()`. Read back in a new scope so values come from Postgres, not the change
  tracker. `CommandCounter` counts the statements one `DbContext` sends (a test for an `AsSplitQuery()` load). Use
  whole-second instants (Postgres keeps microseconds); any offset may be saved — `AppDbContext` converts every
  `DateTimeOffset` to UTC — and values read back have offset 0.
- **HTTP tests** reuse the same `PostgresFixture` container and hand its connection string to the Api through
  configuration (`builder.UseSetting("ConnectionStrings:Db", …)`), so the real `AddInfrastructure` registers
  `AppDbContext` — use `Api/ApiFactory` (Development environment, so startup migrations and OpenAPI run as locally;
  `new ApiFactory(connectionString, environment)` for another database or environment; `new ApiFactory(postgres, time)`
  with a `FakeTimeProvider` replaces the clock for the whole host; `new ApiFactory(postgres, settings)` adds
  configuration the way an environment variable would). Never
  `RemoveAll<DbContextOptions<AppDbContext>>()` and re-register it — that drops `UseSnakeCaseNamingConvention()` and
  the rest of the Infrastructure setup, so the tests would run against a different model than production.
- **Error-mapping tests don't need a server:** `Api/ProblemResponse` writes through the real ProblemDetails setup on a
  `DefaultHttpContext` and reads back the JSON.
- **`ApiHostTests`** pins host-wide behavior (health, JSON options, OpenAPI document, framework error codes). Every new
  endpoint gets a row pinning the statuses it declares in the OpenAPI document (ADR 0009).
- Slower than unit tests by nature — this is exactly why `.githooks/pre-push` intentionally skips this project and
  leaves it to CI.
- `Program` (top-level statements in Api) and the `internal` Api/Infrastructure types are visible here via
  `[assembly: InternalsVisibleTo("AppTemplate.Integration.Tests")]` in each project's `AssemblyInfo.cs`. Keep those
  attributes in sync if the test project is ever renamed.
