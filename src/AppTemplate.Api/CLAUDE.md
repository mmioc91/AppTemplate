# Api layer (composition root)

See root `CLAUDE.md` for cross-cutting rules.

## Rules
- **Thin:** endpoint → build the use-case input → call Application → map the result to HTTP. **No** business
  logic here; **never** touch `DbContext` or repositories directly.
- **Minimal APIs** with route groups; prefer `TypedResults`. Version endpoints via `Asp.Versioning.Http`.
- **Central error mapping** via `IExceptionHandler` + `ProblemDetails`: `DomainException` → **422**,
  `ValidationException` → **400**, Result failures → **404/409**. Avoid a `try/catch` in every endpoint — handle
  it once, centrally.
- **Auth:** JWT bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`). The current user's id/role comes from the
  JWT claims (a small `ICurrentUser`-style abstraction), **never** from the request body.
- **OpenAPI:** the built-in `AddOpenApi()`/`MapOpenApi()` (no extra package needed) covers the spec; add a UI
  package (Scalar/Swagger) only if the project actually needs one rendered.

## Tests
See `tests/AppTemplate.Integration.Tests/CLAUDE.md`.
