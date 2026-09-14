# Architecture tests

See root `CLAUDE.md` for the dependency-direction rules this project enforces.

## Rules
- **NetArchTest.Rules** + Shouldly. Every `[Fact]` here is a real, build-time guard against a layer or
  package-leakage violation (see `LayerDependencyTests.cs`) — none of them are placeholders.
- References `AppTemplate.Api` only, since Api transitively pulls in every other layer's assembly; each layer's
  assembly is then loaded by name (`Assembly.Load("AppTemplate.<Layer>")`) at test runtime.
- When adding a new rule, verify it actually catches something: temporarily introduce the violation it's meant to
  catch, confirm the test fails and names the offending type, then revert the violation.
