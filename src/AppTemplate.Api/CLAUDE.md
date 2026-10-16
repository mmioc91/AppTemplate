# Api layer (composition root)

See root `CLAUDE.md` for cross-cutting rules.

## Rules
- **Thin:** endpoint → build the use-case input → call Application → map the result to HTTP. **No** business
  logic here; **never** touch `DbContext` or repositories directly.
- **Minimal APIs** with route groups; prefer `TypedResults`. Version endpoints via `Asp.Versioning.Http`.
- **Error contract (ADR 0007, 0008, 0018, 0026), already wired in `Errors/`:** every error response is ProblemDetails with
  a `code`. An endpoint returns `error.ToProblem()` for a Result failure (its return type is
  `Results<…, ProblemHttpResult>`) and never builds ProblemDetails itself — Validation → **400** with `errors`
  (field → error codes; C# property paths like `Address.Street` become the JSON names the client sent,
  `address.street`), Unauthorized → **401**, NotFound → **404**, Conflict → **409**, Forbidden → **403**.
  `DomainExceptionHandler` turns a
  `DomainException` into **422** with its code; `BadRequestExceptionHandler` turns unreadable requests (malformed
  JSON — thrown, not answered, in Development) into their own status; every other ProblemDetails gets a `code` from
  its status (`ProblemCodes`, a 429 from a rate limiter included), and a library's code that isn't `UPPER_SNAKE_CASE`
  is replaced. Unexpected → 500 without details. No message ever reaches the client — no `detail`; messages go to the
  log. No `try/catch` in endpoints.
- **Actions on an aggregate** (`POST …/{id}/archive`, ADR 0023) answer **204** from a `Result`, declare
  401/404/409/422 (and 403 under a role policy), repeat as a no-op once answered, and answer 409
  `CONCURRENCY_CONFLICT` to a concurrent change.
- **Options:** every Options class is bound here, in `Program.cs` (`AddOptions<T>().Bind(section).ValidateOnStart()`)
  — Application only registers defaults, so an unbound one silently ignores its configuration.
- **JSON (ADR 0027):** camelCase (Minimal API default — `ErrorResults` relies on it, a test guards it), enums by name
  only (`JsonStringEnumConverter(allowIntegerValues: false)`), instants only with an offset (`StrictInstantConverter`:
  `Z` or `±hh:mm`, anything else is 400 `MALFORMED_REQUEST` — System.Text.Json would otherwise read it in the server's
  time zone; a test of a new instant field sends one with `Z` or `+02:00`), numbers only as JSON numbers
  (`JsonNumberHandling.Strict`, so the schemas say `integer`, not `integer|string`). The converter covers bodies only:
  an instant in a query string would bind in the server's time zone — bind it as a string and let the validator apply
  the same rule, or keep instants out of query strings.
- **Requests (ADR 0020):** a mandatory value-type field (enum, number, bool, date) is **nullable** in the `XxxRequest`
  and the command, validated `NotNull → REQUIRED` — otherwise a missing field silently binds to its default (an enum's
  first member, `0`, `false`).
- **OpenAPI (ADR 0009):** the built-in `AddOpenApi()` + `Asp.Versioning.OpenApi` — one document per version
  (`/openapi/v1.json`), mapped in Development only. Every endpoint declares its result types, its error statuses
  (`.ProducesValidationProblem()`, `.ProducesProblem(4xx)` — `ProblemHttpResult` carries none) and a `WithName`, so a
  typed client (e.g. NSwag) can be generated. Pin each endpoint's status list in `ApiHostTests` when you add it.
  `Location` only for a route that exists, by route name — never a hand-built URL. Add a UI package (Scalar) only if
  the project actually needs one. When CI needs the document, generate it at build time
  (`Microsoft.Extensions.ApiDescription.Server`) rather than exposing it in production. For the generated client
  (ADR 0027): `ClientTypesTransformer` types instants and enums; query parameters are named in camelCase with
  `[FromQuery(Name = "...")]` (otherwise the document uses the record's PascalCase names); a string query parameter
  with fixed values lists them with `[QueryValues(...)]` — not `[AllowedValues]`, which makes the generator fail;
  `OpenApiContractTests` walks the whole document and pins all of it.
- **Startup (ADR 0029):** migrations run on startup in Development only; `Database:MigrateAndExit=true` migrates and
  exits (the deploy step, before the new version serves; the key is `DependencyInjection.MigrateAndExitKey`);
  `UseForwardedHeaders` runs first and trusts `Proxy:KnownNetworks` next to loopback, nothing else; HSTS outside
  Development; `AllowedHosts` is the real host in production configuration, never `*`. The `deploy/` stack sets all of
  these (`compose.yml`, ADR 0033); a new production setting joins it and `env.example` in the same change.
- **Logs and trace ids (ADR 0028):** log through `ILogger` with `[LoggerMessage]`; the request's W3C trace id is in
  every entry's scope and in every ProblemDetails as `traceId` (`ProblemDetailsSetup`) — never invent another
  correlation id. Production logs are JSON lines (`appsettings.Production.json`). A use case's outcome is logged by its
  decorator; an endpoint logs nothing of its own.
- **Health (ADR 0019):** `/health/live` has no checks (for restarts); `/health/ready` checks the database and pending
  migrations (for rotation and the deploy gate). Both anonymous, outside `/api/v1`.
- **Endpoint groups:** `app.MapApiV1()` is `/api/v{version}`; each area maps its own sub-group
  (`v1.MapGroup("/orders")`) in an `<Area>/<Area>Endpoints.cs` extension, with `WithTags` (one client service per tag)
  and `WithName` per endpoint.
- **DTOs:** `XxxRequest` (input), `XxxResponse` (single result), `XxxListItem` (list row); flat, shaped for the
  screen; never a serialized aggregate. Lists that can grow return `PagedList<T>` as `{ items, total, page, pageSize }`
  (ADR 0022); `page`/`pageSize` bind through nullable query parameters and are validated in Application.
- **Auth (ADR 0031), wired in `Auth/`:** JWT bearer validated against `JwtOptions`; a **fallback policy** requires a
  signed-in user on every endpoint — a new endpoint is protected by doing nothing, and only an explicit
  `.AllowAnonymous()` opens one (`/health/*`, OpenAPI, register/login/refresh), with a comment saying why.
  `AuthenticationTests` walks the endpoint table and fails if an unmarked endpoint answers an anonymous call. The
  current user comes from
  `ICurrentUser` (`CurrentUser`, the principal's `sub` claim), **never** from the request body. A role-based policy is
  named in a static class (`AuthPolicies`), built on `Roles` in `AuthenticationSetup` (`RequireRole`) and required on a
  route group (`RequireAuthorization(AuthPolicies.X)`), which then declares 403 too. Every rate-limit policy (a fixed
  window per client: its IPv4 address, or its IPv6 /64 — `ApiRateLimiting.ClientKey`; a delegated /56 can still
  spread over its /64s, ADR 0031) is registered in
  `ApiRateLimiting`, and the limiter runs **after authentication** so a policy
  can count per account: register and login carry `.RequireRateLimiting(AuthRateLimiting.PolicyName)` — its number
  is the host's (`RateLimits:AuthPerMinute`, 10; Development 100 for the e2e suite, ADR 0032) — refresh its
  own `RefreshPolicyName`; a later endpoint that mails someone or tells whether an address has an account (password
  reset, invitations) gets a policy of its own. Token lifetimes run on the host's `TimeProvider`, so an integration test expires tokens with a
  `FakeTimeProvider` through `ApiFactory` (Identity's lockout and the rate limiter keep the system clock). Gotcha: the
  fallback policy also covers requests that matched no real endpoint — `UseNotFoundForUnmatchedRoutes()` runs before
  authorization to keep unknown routes at 404 and to let routing's own 405/415 answer (ADR 0018, 0029, 0031), and
  `UseAuthentication()`/`UseAuthorization()` are called explicitly after it: registered without the calls,
  `WebApplication` inserts them ahead of every user middleware and the 401 wins again.

## Tests
See `tests/AppTemplate.Integration.Tests/CLAUDE.md`.
