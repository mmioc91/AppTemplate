# Application tests

See root `CLAUDE.md` and `src/AppTemplate.Application/CLAUDE.md` for the rules this project tests.

## Rules
- Fake/in-memory ports via **NSubstitute** — no real database, no `WebApplicationFactory`.
- Shouldly for assertions.
- Assert the outcome (Result/exception) and that the right port methods were called — not incidental
  implementation detail.
