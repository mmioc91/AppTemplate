# Project: AppTemplate

Instructions for Claude Code working in this repository. Follow these by default; ask before deviating.
This repo is a reusable template (fork / GitHub "Use this template") for new Clean Architecture .NET projects — keep it generic, don't bake in business-specific logic.
Chat with Claude can be in any language; everything written into the repo (code, comments, commit messages, docs) must be in English.
**Layer-specific rules live in each `src/*/CLAUDE.md` and `tests/*/CLAUDE.md`** (e.g. `src/AppTemplate.Domain/CLAUDE.md`, `tests/AppTemplate.Domain.Tests/CLAUDE.md`), loaded lazily when Claude works in that folder. `src/*` and `tests/*` are sibling folders, so a test project's `CLAUDE.md` isn't picked up just because the matching `src` layer has one — each needs its own. Test conventions shared by all test projects live in `tests/CLAUDE.md`. This root file holds only what's always true across all layers.

## Stack
- **.NET 10 / C# 14** (`net10.0`) · ASP.NET Core **Minimal APIs**
- **EF Core 10 + PostgreSQL** (Npgsql, `EFCore.NamingConventions` for snake_case) · **Dapper** (raw-SQL/read-heavy cases) · **FluentValidation** · **Mapster** (only for large flat mappings, no AutoMapper; ADR 0014)
- ASP.NET Core **Identity** (the auth scheme — cookie or JWT — is an open question, see `docs/decisions/README.md`) · **Asp.Versioning.Http** (API versioning) · built-in OpenAPI (ADR 0009)
- Tests: **xUnit + Shouldly + NSubstitute + Microsoft.AspNetCore.Mvc.Testing + Testcontainers.PostgreSql + NetArchTest.Rules**
- **Architecture:** Clean Architecture, layered by project (SharedKernel/Domain/Application/Infrastructure/Api) — not vertical-slice feature-folders (ADR 0001)
- Frontend (later): Angular PWA in `web/`

## Read first
`README.md` is the map of the repo (structure, conventions, CI, getting started). Product design docs (what to build) are project-specific — once this template is used for a real project, link them here and say where they live.
**Decisions:** `docs/decisions/` holds one ADR per decision (index + open questions in its `README.md`). The ADRs shipped with the template are the baseline every project starts from. Whenever we make a choice that isn't obvious from the code — especially a deviation from a rule here or from a baseline ADR — add an ADR in the same change. Never rewrite an accepted ADR; supersede it with a new one.

## Architecture (always applies)
- **Dependency direction, inward only:** Api → Infrastructure → Application → Domain → SharedKernel. Api also references Application directly (composition root).
- **SharedKernel is the innermost project** — BCL only, zero dependencies on anything, including Domain. Holds generic DDD building blocks (`Entity<TId>`, `AggregateRoot<TId>`, `Enumeration<TEnum>`, `DomainException`) that Domain inherits from — never business-specific logic. See `src/AppTemplate.SharedKernel/CLAUDE.md`.
- **Domain is pure** — no dependency on Application/Infrastructure/Api, and no infrastructure packages (EF Core, Npgsql, Dapper, ASP.NET Core).
- **Application** only knows abstractions/ports — no EF Core/Npgsql/Dapper/ASP.NET Core package references either.
- **`DbContext` lives only in Infrastructure.**
- All of the above is enforced at build time by `tests/AppTemplate.Architecture.Tests` (NetArchTest) — don't weaken those rules without discussing it first.

## Cross-cutting rules (always apply)
- **IDs:** UUIDv7 (`Guid.CreateVersion7()`), generated in the application, never by the database; wrapped in strongly-typed IDs (ADR 0003). Not monotonic within one millisecond — never order by id to mean "in the order added"; store an explicit order.
- **Errors:** every error a client can see carries a stable `UPPER_SNAKE_CASE` `code` (ADR 0004, 0008, 0018); clients switch on `code`, never on text. Messages are English, for logs only.
- **No PII or secrets** in logs, exception messages or ProblemDetails. Logs carry ids only and never a command, query or request body (ADR 0028).
- **Logs (ADR 0028):** `ILogger` with `[LoggerMessage]`; the request's W3C trace id is in every entry's scope and in every ProblemDetails as `traceId` — never invent another correlation id. Every use case is logged by its decorator (name, duration, outcome); production writes JSON lines.

## Core conventions (cross-cutting)
- **`var`:** only when the type is textually apparent from the right-hand side (e.g. `new Foo()`); explicit type everywhere else — enforced as build errors by `.editorconfig` (`csharp_style_var_elsewhere`/`csharp_style_var_for_built_in_types` = `false:error`). This includes `Program.cs` — write `WebApplicationBuilder builder = WebApplication.CreateBuilder(args);`, not `var builder = ...`. Gotcha: the analyzer also counts a static factory on the same type (`Money.Of(10)` returning `Money`, `Guid.CreateVersion7()` returning `Guid`, `Result.Success()` returning `Result`) as apparent, so there `var` is *required* (IDE0007 build error) — same for LINQ `.ToDictionary(...)`; when the build reports IDE0007, switch to `var`; `Foo.FromName(x)!` and generic calls like `Should.Throw<T>(...)` are not.
- **Central Package Management:** all package versions live in `Directory.Packages.props`; individual `.csproj` files use bare `<PackageReference Include="X" />` with no `Version=`.
- **TargetFramework** lives only in `Directory.Build.props` (`net10.0`), never in individual `.csproj` files.
- **NuGet sources:** restricted to `nuget.org` via the repo's `NuGet.Config` (`<clear/>` + package source mapping) — needed because multiple configured sources + Central Package Management trigger NU1507, escalated to a build error by `TreatWarningsAsErrors`.
- **Time:** never `DateTime.Now`/`UtcNow` — use `TimeProvider`; enforced as a build error by `BannedApiAnalyzers` (RS0030, see `BannedSymbols.txt`).
- **Async:** pass `CancellationToken` everywhere (forwarding it is a build error via `CA2016`); never block (`CA1849` is a build error), misuse a `ValueTask` (`CA2012`) or write `async void`.
- **APIs:** Minimal APIs, prefer `TypedResults`; version endpoints via `Asp.Versioning.Http`; immutable `record` DTOs; never expose EF entities or aggregates directly — build DTOs explicitly; Mapster only for large flat one-to-one mappings (ADR 0014).
- **Config:** Options pattern (`ValidateOnStart`); secrets via `dotnet user-secrets` locally, never in source (`.gitignore` blocks `appsettings.*.Local.json`, `.env*`, etc.).
- **Modern C#:** primary ctors, collection expressions, pattern matching, records where they help.

## Gotchas / bridges
- `EnforceCodeStyleInBuild=true` + `TreatWarningsAsErrors=true` in `Directory.Build.props` mean **any** `.editorconfig` rule with severity `:error` (code style, not just compiler warnings) fails `dotnet build`, not just the IDE.
- `NuGetAuditMode=all` + `TreatWarningsAsErrors`: a newly published CVE in any package, including transitive, turns restore into a build error (NU1901–NU1904) overnight with no code change — fix it by bumping the version in `Directory.Packages.props` (for a transitive package: a `PackageVersion` there plus a bare `PackageReference` in the affected project, since `CentralPackageTransitivePinningEnabled` is off), never with an inline `Version=` or by suppressing the warning.
- EF Core migrations aren't marked `<auto-generated>`, so `.editorconfig` declares `**/Persistence/Migrations/*.cs` as generated code — otherwise the style rules above fail the build on the first `migrations add`. Never hand-edit a migration.
- Solution file is the new XML `.slnx` format — add loose root files (props, configs, docs) to `<Folder Name="/Solution Items/">` with `<File Path="..."/>`, not `<Project Path="..."/>` (that's only for real projects).
- Line endings (ADR 0021): `.gitattributes` (`* text=auto`) has git store text files with LF whatever the committer's settings, while each checkout keeps its machine's endings — CRLF on Windows — so anything that compares files byte for byte must accept both (a `web/` Prettier config must set `"endOfLine": "auto"`). `.githooks/*`, `*.sh`, `deploy/**` and `.dockerignore` are LF in every checkout, and `.editorconfig` keeps the hooks and scripts LF in the editor too (bash and Linux containers trip over a CR). A file that must be CRLF everywhere (`text eol=crlf`) or a binary git would take for text (`binary`) needs a line of its own there.
- A worktree the desktop app creates may carry its own `core.hooksPath` pointing at the main checkout's `.githooks` (absolute path, `git config --show-origin core.hooksPath`): its hooks are then `main`'s version, not the branch's. To run the branch's own hooks for one command: `git -c core.hooksPath=.githooks push ...` — never `--no-verify`.
- `[Range(typeof(decimal), "0.01", …)]` parses its limits in the **current culture** — on a Croatian machine "0.01" is no number and the Api won't start. Add `ParseLimitsInInvariantCulture = true`.

## Commands
`dotnet build AppTemplate.slnx` · `dotnet test AppTemplate.slnx` · `dotnet run --project src/AppTemplate.Api`
`git config core.hooksPath .githooks` (once per clone — activates the `commit-msg`/`pre-push` hooks) · `git add --chmod=+x .githooks/<hook>` (once per new hook, to stage it as 100755 — Windows doesn't record the executable bit, and git on Linux/macOS skips a hook stored without it; ADR 0021) · `dotnet tool restore` (once per clone — `dotnet-ef`, `dotnet-stryker`)
`winget install --id GitHub.cli` then `gh auth login --web --git-protocol https` (once per machine — lets Claude open PRs: `gh pr create --base main --title "<conventional title>" --body-file <file>`)
`docker compose up -d` (Postgres) · `dotnet user-secrets set "ConnectionStrings:Db" "..."` (from `src/AppTemplate.Api`)
`dotnet tool run dotnet-ef migrations add <Name> --project src/AppTemplate.Infrastructure --startup-project src/AppTemplate.Infrastructure --output-dir Persistence/Migrations` (design-time factory, no Api needed; applying needs `ConnectionStrings__Db` in the environment). A model change and its migration go in the same commit.
`dotnet run --project src/AppTemplate.Api -- --Database:MigrateAndExit=true` — migrates the database and exits: the production deploy step, run before the new version serves (ADR 0029; in a container the environment variable `Database__MigrateAndExit=true` does the same — never set it in a shell you then run `dotnet test` from, or every Api test host migrates and stops). Production also needs `Proxy__KnownNetworks` (the proxy's CIDR) and `AllowedHosts` (the real host).
`dotnet tool run dotnet-stryker` (from `src/AppTemplate.Domain` or `src/AppTemplate.Application`, never the repo root) — mutation testing; also runs on PRs for changed Domain/Application code, see `tests/AppTemplate.Domain.Tests/CLAUDE.md` and `tests/AppTemplate.Application.Tests/CLAUDE.md`; locally `--since:main` can fail with LibGit2Sharp's "not owned by current user", so pass the changed files instead (`--mutate "**/Orders/Order.cs"`)

CI (`.github/workflows/`): `ci-backend.yml` (every PR, deliberately not path-filtered because `build-test` is a required check; push to `main` path-filtered on `src/`, `tests/`, `Directory.*.props` and the workflow itself; on PRs also a report-only Stryker matrix job for changed Domain and Application code, ADR 0017, skipped with a notice past 40 changed C# files of a project), `ci-web.yml` (path-filtered on `web/`: `web-build-test` — lint, format check, production build, Vitest; named apart from `build-test` and never a required check), `pr-title.yml` (Conventional Commits PR title check), `secrets-scan.yml` (gitleaks). No secrets needed — NuGet restore is scoped to `nuget.org` only.

## What to avoid
Deps in SharedKernel or Domain · EF Core/Npgsql/Dapper/ASP.NET Core packages in Application · `DateTime.Now`/`UtcNow` (build error, see `BannedSymbols.txt`), magic strings, service-locator · in-memory DB in tests (use `Testcontainers.PostgreSql`; build error, see `BannedSymbols.txt`) · `var` where an explicit type is required (build error, see `.editorconfig`) · inline `Version=` on `PackageReference` or `TargetFramework` in individual `.csproj` files · over-abstracting.

## Workflow with Claude
Don't make edits on your own initiative — **propose a plan first** (what you'd change and why) and wait for explicit approval before writing to any file. This applies beyond "non-trivial" changes: including config/build files (`.slnx`, `.csproj`, `Directory.*.props`, etc.), not just application code.
Push back on my decisions and code — you're here to review and challenge, not to rubber-stamp. If I've done something wrong or there's a better approach, say so and propose the better solution instead of silently patching around the symptom.
Don't invent problems — leave correct, idiomatic code alone. Explain each change in one line (what + why). When unsure, ask rather than guess.
Whenever an important one-off command comes up (setup, tooling, deploy, git remote, etc.), add it to the **Commands** section above so it doesn't need to be re-derived later.
Before a PR, `.claude/agents/code-reviewer.md` reviews the branch against this repo's own rules (`CLAUDE.md` files + ADRs); the generic `/code-review` looks for bugs.
