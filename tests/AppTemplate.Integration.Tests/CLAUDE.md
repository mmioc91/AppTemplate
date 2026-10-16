# Integration tests

See `tests/CLAUDE.md` for shared test conventions, and root `CLAUDE.md`, `src/AppTemplate.Infrastructure/CLAUDE.md`
and `src/AppTemplate.Api/CLAUDE.md` for the rules this project tests — it covers both database-level integration
tests and Api-level functional tests.

## Rules
- Real Postgres via **Testcontainers.PostgreSql** (Docker must be running) — never `UseInMemoryDatabase` (build
  error, see root `BannedSymbols.txt`), never mock `DbContext`. Once aggregate roots carry an `xmin` concurrency
  token, test the actual conflict path (two scopes load, one saves, the other gets `ConcurrencyConflictException`),
  not just the happy path.
- **Checklist per use case** (ADR 0010): happy path · validation (400 + `errors`) · auth (401 without a token —
  `AuthClient.SignInAsync` signs a fresh account in, `AuthClient.SignInWithRoleAsync` one with a role, creating the
  role if the database lacks it; 403 for the wrong role once a policy exists) · not found / another owner's data
  (404) · persisted state (assert the database, not just the response) · concurrency conflict where relevant ·
  idempotency for anything retried (webhooks, jobs).
- **With the first aggregate:** add persistence tests for `UnitOfWork` — a real duplicate insert throws
  `UniqueConstraintViolationException`, a real stale update throws `ConcurrencyConflictException` (ADR 0013). The
  host-wide promises are pinned already: an unmigrated database answers `/health/ready` with 503 while
  `/health/live` stays 200 (`ApiHostTests`, on `new ApiFactory(await postgres.CreateEmptyDatabaseAsync(),
  Environments.Production)`); malformed JSON → 400 `MALFORMED_REQUEST` and an unhandled exception → 500
  `UNEXPECTED_ERROR` without details (`AuthEndpointsTests`, ADR 0018).
- External services are never called live — stub the HTTP transport. Each test arranges its own data with fresh
  ids; no ordering dependencies.
- **Persistence tests** join `[Collection(PostgresCollection.Name)]`: one `postgres:17` container per run, migrated
  once by `PostgresFixture` (which is also the "migrations apply to an empty database" check). Build services with
  `postgres.CreateServices()` — the real `AddInfrastructure`; `postgres.CreateServices(configure)` adds what a test
  needs on top (`AuthServices.Configure` adds the clock and `JwtOptions` the auth ports need) — and isolate each test with fresh ids instead of
  resetting the database. A test about a database before its first migration (readiness, a seed) gets its own from
  `postgres.CreateEmptyDatabaseAsync()`. Read back in a new scope so values come from Postgres, not the change
  tracker. `CommandCounter` counts the statements one `DbContext` sends (a test for an `AsSplitQuery()` load). Use
  whole-second instants (Postgres keeps microseconds); any offset may be saved — `AppDbContext` converts every
  `DateTimeOffset` to UTC — and values read back have offset 0.
- **HTTP tests** reuse the same `PostgresFixture` container and hand its connection string to the Api through
  configuration (`builder.UseSetting("ConnectionStrings:Db", …)`), so the real `AddInfrastructure` registers
  `AppDbContext` — use `Api/ApiFactory` (Development environment, so startup migrations and OpenAPI run as locally;
  `new ApiFactory(connectionString, environment)` for another database or environment; `new ApiFactory(postgres, time)`
  with a `FakeTimeProvider` replaces the clock for the whole host — token issuing and validation included, so it
  expires tokens (not Identity's lockout or the rate limiter, which keep the system clock); `new ApiFactory(postgres, settings: …)` adds configuration as command-line settings, above
  appsettings files and environment variables; the JWT signing key is `ApiFactory.SigningKey`, never a real one; the sign-in rate limit is pinned to production's
  `AuthRateLimitOptions.DefaultPerMinute`, not Development's raised one).
  `Api/Auth/AuthClient` drives the auth endpoints as a client would; `AuthenticationTests` pins the host-wide auth
  behavior (fallback policy, token validation, claims, rate limits), `AuthEndpointsTests` the endpoints. Never
  `RemoveAll<DbContextOptions<AppDbContext>>()` and re-register it — that drops `UseSnakeCaseNamingConvention()` and
  the rest of the Infrastructure setup, so the tests would run against a different model than production.
- **Races are tested, not hoped for:** `Persistence/BeforeNextSave` runs one competing write right before the next
  `SaveChanges` of the context under test (register it with `ConfigureDbContext<AppDbContext>(o =>
  o.AddInterceptors(race))`, then `Arm()`) — the window where a concurrent request lands. A competing write from
  another connection commits at once; assert `race.Fired`, so a test can't pass because the race never happened.
- **Error-mapping tests don't need a server:** `Api/ProblemResponse` writes through the real ProblemDetails setup on a
  `DefaultHttpContext` and reads back the JSON.
- **`ApiHostTests`** pins host-wide behavior (health, JSON options, OpenAPI document, framework error codes, the
  `traceId` continued from a caller's `traceparent`, the forwarded-headers options). Every new endpoint gets a row
  pinning the statuses it declares in the OpenAPI document (ADR 0009). A middleware that only looks at the request
  (`UnmatchedRoutesTests`) is tested on a `DefaultHttpContext` through a one-middleware `ApplicationBuilder`, and so
  is a port on the principal (`CurrentUserTests`). `OpenApiContractTests` runs the rules in `OpenApiContract` over
  the whole live document for the generated client's sake (ADR 0027); `OpenApiContractRulesTests` proves each rule
  on a hand-written document.
- Slower than unit tests by nature — this is exactly why `.githooks/pre-push` intentionally skips this project and
  leaves it to CI.
- `Program` (top-level statements in Api) and the `internal` Api/Infrastructure types are visible here via
  `[assembly: InternalsVisibleTo("AppTemplate.Integration.Tests")]` in each project's `AssemblyInfo.cs`. Keep those
  attributes in sync if the test project is ever renamed.
