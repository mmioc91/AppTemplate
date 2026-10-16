# Infrastructure layer

See root `CLAUDE.md` for cross-cutting rules.

## Rules
- **Implements Application ports** + technical detail (EF Core, Identity, Dapper, etc.). **EF is used only here**;
  `DbContext` never leaks to Application or Api. `AppDbContext` is `internal` — Api and Application reach the
  database only through ports; Integration.Tests see it via `InternalsVisibleTo`.
- **Persistence conventions:** strongly-typed IDs, smart enums and plain enums get their converters registered once,
  in `AppDbContext.ConfigureConventions` (an ID converter is one line:
  `internal sealed class OrderIdConverter() : ValueConverter<OrderId, Guid>(id => id.Value, value => new OrderId(value));`).
  `EFCore.NamingConventions` handles snake_case column/table names for Postgres — don't hand-name them, and don't
  hand-name indexes or constraints either (no `HasDatabaseName`; the one exception is `AppDbContext.RenameIdentityTables`,
  below); use `jsonb` where a document fits better than a
  normalized table; Fluent API (`IEntityTypeConfiguration<>` in `Persistence/Configurations/`), not attributes on
  entities. A value object with a single value (`Slug`) is stored as that value through a converter registered in
  `ConfigureConventions`, like the IDs, and read back through its factory, so it can carry an index and be compared in
  queries (ADR 0025).
- **Aggregate boundaries in EF:** navigations *within* an aggregate; cross-aggregate references stay a scalar
  **Id only**, no navigation property, but with a database foreign key (`ON DELETE RESTRICT` unless an ADR says
  otherwise). Child entities are owned collections (`OwnsMany`) so they load and save with their root.
- **Value objects with two or more values are complex types** (`ComplexProperty`; a single value goes through a
  converter, ADR 0025), never owned types: owned types have identity, so a shared instance (a static default, one
  value on two rows) is saved on one owner and lost on the rest (ADR 0016). EF has
  **no complex types on owned entities** (`OwnedNavigationBuilder` lacks `ComplexProperty`), so a value object on a
  child entity goes through a value converter instead — one column, or one `jsonb` document for several values.
- **Gotcha — get-only properties:** EF doesn't map `{ get; }` properties by convention, and Domain uses them a lot.
  List every one explicitly in the configuration (`builder.Property(o => o.CreatedAt);`) so EF fills its backing
  field. A model error only shows at runtime — generate the migration (or `GenerateCreateScript()`) to check a
  mapping change. IDs are app-generated (`ValueGeneratedNever`).
- **Time:** every `DateTimeOffset` is converted to UTC on save (`UtcDateTimeOffsetConverter`) — Npgsql refuses other
  offsets for `timestamptz`. Values read back have offset 0; the instant is unchanged.
- **Smart enum values are stored by name** (`EnumerationConverter<TEnum>`): never rename or remove a value without a
  data migration — an unknown name in the database fails the query on purpose (code and data have drifted apart).
- **Concurrency:** Postgres `xmin` on every aggregate root via a shadow property —
  `builder.Property<uint>("Version").IsRowVersion();` (Npgsql maps it to `xmin`; no extra column). `xmin` changes only
  when the root's own row is written: an aggregate method that changes only child rows must still touch the root
  (e.g. set `UpdatedAt`), or the concurrency check never runs.
- **Read-stores (queries):** `.AsNoTracking()`, project to DTOs with `.Select()`, paginate (`Skip`/`Take` + a
  stable `OrderBy` with the id as the tie breaker, ADR 0022), prefer `AnyAsync()` over `CountAsync() > 0`, watch for
  N+1. Load aggregates whole via repositories with the needed `Include`s; one with several owned collections loads
  with `AsSplitQuery()` (one joined query returns every combination of them) — `CommandCounter` in Integration.Tests
  counts the statements a load sends.
- **No `ExecuteUpdate`/`ExecuteDelete` on aggregate roots** — they bypass domain invariants and the concurrency
  check; changes go through the aggregate. Bulk statements only in system maintenance paths, and only with an ADR.
- **No `AddDbContextPool`:** a context that holds per-request state (a tenant filter, the current user) would leak it
  into another request. **No global `NoTracking` default:** commands rely on tracking to save aggregate changes —
  `AsNoTracking()` stays per query in read-stores.
- **Unit of work:** `IUnitOfWork.SaveChangesAsync` translates the expected outcomes: a Postgres unique violation
  (SQLSTATE `23505`) becomes `UniqueConstraintViolationException`, `DbUpdateConcurrencyException` becomes
  `ConcurrencyConflictException` (ADR 0013). Cover both with integration tests using real conflicts once the first
  aggregate exists — and that `DiscardChanges()` lets the same scope reload and save again after one. Anything else
  (e.g. a foreign-key violation) is a bug and surfaces untranslated (500).
  `DiscardChanges()` (`ChangeTracker.Clear()`) lets a system path reload and retry after a conflict. Infrastructure
  code that saves its own rows outside the unit of work (an identity service, a worker) asks
  `DbUpdateExceptions.IsUniqueViolation()` and checks for its own row before calling it a duplicate.
- **Retrying execution strategy** (`EnableRetryOnFailure`): a retried commit can hit a unique index with the row it
  already saved — use cases check "is it my own row?" before calling it a duplicate (ADR 0013). An explicit
  transaction must run inside `Database.CreateExecutionStrategy().ExecuteAsync(...)`, or EF throws. Dapper calls are
  not covered by the strategy — they fail on the first transient error unless wrapped in it too.
- **Dapper** is available for read-heavy or raw-SQL cases where EF's query translation gets in the way — keep it
  next to EF Core, not as a full replacement.
- **Outbound HTTP:** typed `HttpClient` with resilience (retry/backoff, circuit breaker, timeout); an anti-corruption
  mapping turns the external vocabulary into domain types here, before anything reaches Domain. Tests never call
  live services.
- **Health (ADR 0019):** checks tagged `DependencyInjection.ReadinessTag` decide readiness — today the database
  (`AddDbContextCheck`) and pending migrations (`PendingMigrationsHealthCheck`).
- **Migrations** live in `Persistence/Migrations/` and are created with the design-time factory (root `CLAUDE.md` →
  Commands). Applied on startup in Development only (`MigrateDatabaseAsync`); in production as a separate deploy
  step — the Api started with `Database:MigrateAndExit=true` migrates and exits (ADR 0029). Never edit a migration
  that has been pushed — add a new one. A model change and its migration go in the same commit.
- **Identity (ADR 0031):** `AppDbContext` is an `IdentityDbContext`; Identity's tables and its three explicitly named
  indexes are renamed in `AppDbContext.RenameIdentityTables` (the only hand-written names in the model) **before**
  our configurations are applied, so foreign keys are named after `users`; the e-mail index is made unique there.
  `Identity/` holds the user, the `refresh_tokens` entity and the two auth ports (`IdentityService` on `UserManager`,
  `TokenService` for JWTs and hashed, rotating refresh tokens); both save their own changes, and every login refusal
  costs a password hash (`IdentityService.CheckPasswordAsync`). Gotcha: `UserManager` reports a lost race on the
  user row's concurrency stamp as a failed `IdentityResult` — it never throws — and leaves the entry Modified, so the
  next `SaveChanges` of the scope replays the stale write and fails: check every result of a `UserManager` write that
  can race, reload the row and go again (`IdentityService` does this for the failed-attempt count). `JwtOptions` lives
  here (the token service signs with it) and is bound by the Api; `DependencyInjection.ConfigureIdentity` is shared
  with the design-time factory, so Identity's store options (schema version, key lengths) shape the migrations too.
- **Mail (ADR 0034):** `IMailQueue` adds a `QueuedMail` row to the request's context — it commits with the use
  case's `SaveChangesAsync`, never on its own. `MailDeliveryWorker` (hosted) claims a batch in one statement
  (`UPDATE … SET next_attempt_at = now + lease … FOR UPDATE SKIP LOCKED … RETURNING *`), then sends through
  `IMailTransport` (SMTP, MailKit) with no transaction open, saving each mail's outcome as it comes; it backs off on
  failure and purges old rows. Never send inside a transaction or an execution strategy — a retried commit resends
  the batch. `HandlebarsMailRenderer` caches compiled templates by their text; `Web/WebLinks` builds every link from
  `Web:BaseUrl` (a new page gets a method there, the token in the query). `MailOptions` and `WebOptions` live here and
  are bound by the Api like `JwtOptions`.
- **Reference data (ADR 0031):** inserted if missing by well-known id after every `Migrate`, through EF's seeding
  hook in `DependencyInjection.Configure` (`UseSeeding`/`UseAsyncSeeding`, so `dotnet ef database update` seeds too),
  straight through the `DbContext` — no managers, nothing else is registered where migrations run — and only when no
  migration is pending, so a revert never seeds a table that is gone. `RoleSeed` + `RoleSeeder` is the pattern: a
  project's roles go into `RoleSeed`, other reference data gets a seeder of the same shape next to it (it clears the
  tracker first, so a retried migration doesn't meet its own rolled-back rows), called from the same hook. Never
  `HasData` — it diffs the data into every migration.
- Register all Infrastructure services/ports from `AddInfrastructure(...)`, called once from `Program.cs`. A missing
  connection string fails there with a message naming the setting.

## Tests
See `tests/AppTemplate.Integration.Tests/CLAUDE.md`.
