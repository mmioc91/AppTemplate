# Application layer

See root `CLAUDE.md` for cross-cutting rules.

## Rules
- References **only Domain** (+ FluentValidation, Mapster). **No** EF Core/Npgsql/Dapper or ASP.NET Core types — if
  a use case can't be tested with fake ports, infrastructure has leaked. Enforced at build time by
  `tests/AppTemplate.Architecture.Tests`.
- **Orchestrate, don't decide:** no business rules here (those live in Domain). Typical flow: load an aggregate
  through a repository port → call a domain method → persist via the port. An `if` checking a business rule in a
  use case is a smell — it belongs in Domain instead.
- **External concerns behind ports:** define an interface here (e.g. `IEmailSender`, `I*Repository`) and implement
  it in Infrastructure. Application never references a concrete technology directly.
- **Validation:** FluentValidation validators for use-case inputs; run them before the use case executes.
- **Errors:** prefer a Result pattern for expected failures (not-found/validation/conflict) over exceptions; reserve
  `DomainException` for invariant breaches raised by Domain. Api maps these to the appropriate HTTP status.
- **Mapping:** Mapster between entities and DTOs — never return an EF entity from a use case.
- This template doesn't prescribe a mediator/CQRS library — pick one (or plain method calls) once the project's
  actual use cases are being built.

## Tests
Use-case tests with fake/in-memory ports (no DB, no `WebApplicationFactory`). NSubstitute for the fakes, Shouldly
for assertions. Assert the outcome (Result/exception) and that the right port methods were called.
