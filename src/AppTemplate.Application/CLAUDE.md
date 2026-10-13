# Application layer

See root `CLAUDE.md` for cross-cutting rules.

## Rules
- References **only Domain** (+ FluentValidation, Mapster). **No** EF Core/Npgsql/Dapper or ASP.NET Core types — if
  a use case can't be tested with fake ports, infrastructure has leaked. Enforced at build time by
  `tests/AppTemplate.Architecture.Tests`.
- **Orchestrate, don't decide:** no business rules here (those live in Domain). Typical flow: load an aggregate
  through a repository port → call a domain method → persist via the port. An `if` checking a business rule in a
  use case is a smell — it belongs in Domain instead.
- **External concerns behind ports:** define an interface in `Abstractions/` (e.g. `IEmailSender`, `I*Repository`)
  and implement it in Infrastructure. Application never references a concrete technology directly.
- **Errors (ADR 0008):** use cases return `Result` (no value) or `Result<T>`; an expected failure is an `Error` with a
  `code` and a kind — Validation (400), Unauthorized (401), NotFound (404), Conflict (409), Forbidden (403).
  `DomainException` only comes from Domain (422). Shared codes go in `Common/CommonErrors` (today
  `ConcurrencyConflict()`, ADR 0023), use-case codes in `<UseCase>Errors`; never rename or reuse one.
- **Mapping:** build response DTOs explicitly from the aggregate (`order.Id.Value`, `order.Status.Name`) — never
  return an aggregate or EF entity from a use case. Mapster only for large flat one-to-one mappings (ADR 0014).
- **No mediator library** (ADR 0011): own handler interfaces in `Common/` — `ICommandHandler<TCommand>` (returns
  `Result`), `ICommandHandler<TCommand, TResult>` and `IQueryHandler<TQuery, TResult>` — resolved from DI;
  cross-cutting steps are decorators around them: Logging → Validation → handler (ADR 0028).
- **Handlers are `internal`** and never registered as themselves: `AddApplication()` scans the assembly and registers
  only the decorated contract, so logging and validation can't be bypassed by injecting a concrete handler. Tests
  reach handlers via `InternalsVisibleTo`; `DependencyInjectionTests` proves every registered handler resolves wrapped
  in logging then validation and the concrete type isn't resolvable.
- **Logging (ADR 0028):** `LoggingDecorator`/`QueryLoggingDecorator` log a use case's name, duration and outcome (the
  code and kind of a failure, the type of an exception) — never the input. A handler that logs something of its own
  uses `ILogger<T>` with `[LoggerMessage]` and ids only.
- **Unique-constraint races:** a write that can hit a unique index catches `UniqueConstraintViolationException`
  around `SaveChangesAsync` and decides by the database, never by the constraint name: own row there → a retried
  commit succeeded (success); same natural key there → Conflict; otherwise rethrow (ADR 0013).
- **Concurrency:** `ConcurrencyConflictException` from `SaveChangesAsync` → `CommonErrors.ConcurrencyConflict()` (409)
  on a user action; a background process calls `IUnitOfWork.DiscardChanges()`, reloads and retries (ADR 0013, 0023).

## Use cases
- **Command** (`XxxCommand`): load the *whole* aggregate through its repository → call one domain method → one
  `IUnitOfWork.SaveChangesAsync` = one transaction, changing that one existing aggregate (it may create new aggregates
  that exist only for the change — ADR 0024; the domain method runs first, the new ones are added after it accepted).
  **Query** (`XxxQuery`, `IQueryHandler`): project straight to a DTO through a read-store port (`IXxxReadStore`) — no
  repository, no transaction, no loading of whole aggregates. Query handlers are registered decorated like commands
  (`QueryValidationDecorator`, a pass-through for a query without a validator). A list that can grow takes optional
  `Page`/`PageSize` (validated `NOT_POSITIVE`/`TOO_LARGE`, defaults and the maximum in `Common/Paging`) and returns
  `PagedList<T>` (ADR 0022).
- **Layout:** one folder per aggregate, one sub-folder per use case (`Orders/PlaceOrder/PlaceOrderCommand.cs`,
  `…Validator.cs`, `…Handler.cs`, `…Response.cs`); ports in `Abstractions/`, shared types in `Common/`.
- **Who is asking** (current user, tenant) comes from ports the Api fills per request — never from command or query
  input.
- **Await port calls one after another** — never `Task.WhenAll` over repositories/read-stores: behind the ports they
  share one scoped `DbContext`, which isn't thread-safe (EF throws at runtime, and only sometimes).
- **One validator per command/query**, checking input shape only (non-empty, format, range); domain invariants stay
  in Domain. Every rule sets `.WithErrorCode(ValidationCodes.X)` — generic codes (`REQUIRED`, `TOO_SHORT`, `TOO_LONG`,
  `INVALID`, `NOT_POSITIVE`, `TOO_LARGE`, `NOT_ALLOWED`; add one only for a genuinely new kind of problem); the
  validation decorator reports field → codes, never messages (ADR 0012). Validator tests assert the codes (`FluentValidation.TestHelper`).
  Every free-text field has a max length. A mandatory value-type field is nullable and starts with
  `NotNull().WithErrorCode(ValidationCodes.Required)` (ADR 0020).
- **Cross-aggregate orchestration lives here**, never in an aggregate (e.g. "count existing X before creating a Y").
- **Time and IDs** are resolved here — time from the injected `TimeProvider` (`AddApplication` registers the system
  clock unless one is already registered), IDs with `Guid.CreateVersion7()` wrapped in the strongly-typed ID (no ID
  port, ADR 0002) — and passed into Domain.
- **Options** classes may live here, but Application only registers their defaults; the Api binds them to
  configuration.

## Tests
See `tests/AppTemplate.Application.Tests/CLAUDE.md`.
