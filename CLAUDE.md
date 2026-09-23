# Project: AppTemplate

Instructions for Claude Code working in this repository. Follow these by default; ask before deviating.
This repo is a reusable template (fork / GitHub "Use this template") for new Clean Architecture .NET projects — keep it generic, don't bake in business-specific logic.
Chat with Claude can be in any language; everything written into the repo (code, comments, commit messages, docs) must be in English.
**Layer-specific rules live in each `src/*/CLAUDE.md` and `tests/*/CLAUDE.md`** (e.g. `src/AppTemplate.Domain/CLAUDE.md`, `tests/AppTemplate.Domain.Tests/CLAUDE.md`), loaded lazily when Claude works in that folder. `src/*` and `tests/*` are sibling folders, so a test project's `CLAUDE.md` isn't picked up just because the matching `src` layer has one — each needs its own. This root file holds only what's always true across all layers.

## Stack
- **.NET 10 / C# 14** (`net10.0`) · ASP.NET Core **Minimal APIs**
- **EF Core 10 + PostgreSQL** (Npgsql, `EFCore.NamingConventions` for snake_case) · **Dapper** (raw-SQL/read-heavy cases) · **FluentValidation** · **Mapster** (mapping, no AutoMapper)
- ASP.NET Core **Identity + JWT** · **Asp.Versioning.Http** (API versioning)
- Tests: **xUnit + Shouldly + NSubstitute + Microsoft.AspNetCore.Mvc.Testing + Testcontainers.PostgreSql + NetArchTest.Rules**
- **Architecture:** Clean Architecture, layered by project (SharedKernel/Domain/Application/Infrastructure/Api) — not vertical-slice feature-folders
- Frontend (later): Angular PWA in `web/`

## Read first
`README.md` is the map for this template (structure, conventions, CI, getting started). There are no fixed design docs baked into this repo — add project-specific docs under `docs/` once this template is used for a real project.

## Architecture (always applies)
- **Dependency direction, inward only:** Api → Infrastructure → Application → Domain → SharedKernel. Api also references Application directly (composition root).
- **SharedKernel is the innermost project** — BCL only, zero dependencies on anything, including Domain. Holds generic DDD building blocks (`Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `Enumeration<TEnum>`) that Domain inherits from — never business-specific logic. See `src/AppTemplate.SharedKernel/CLAUDE.md`.
- **Domain is pure** — no dependency on Application/Infrastructure/Api, and no infrastructure packages (EF Core, Npgsql, Dapper, ASP.NET Core).
- **Application** only knows abstractions/ports — no EF Core/Npgsql/Dapper/ASP.NET Core package references either.
- **`DbContext` lives only in Infrastructure.**
- All of the above is enforced at build time by `tests/AppTemplate.Architecture.Tests` (NetArchTest) — don't weaken those rules without discussing it first.

## Core conventions (cross-cutting)
- **`var`:** only when the type is textually apparent from the right-hand side (e.g. `new Foo()`); explicit type everywhere else — enforced as build errors by `.editorconfig` (`csharp_style_var_elsewhere`/`csharp_style_var_for_built_in_types` = `false:error`). This includes `Program.cs` — write `WebApplicationBuilder builder = WebApplication.CreateBuilder(args);`, not `var builder = ...`.
- **Central Package Management:** all package versions live in `Directory.Packages.props`; individual `.csproj` files use bare `<PackageReference Include="X" />` with no `Version=`.
- **TargetFramework** lives only in `Directory.Build.props` (`net10.0`), never in individual `.csproj` files.
- **NuGet sources:** restricted to `nuget.org` via the repo's `NuGet.Config` (`<clear/>` + package source mapping) — needed because multiple configured sources + Central Package Management trigger NU1507, escalated to a build error by `TreatWarningsAsErrors`.
- **Time:** never `DateTime.Now`/`UtcNow` — use `TimeProvider`; enforced as a build error by `BannedApiAnalyzers` (RS0030, see `BannedSymbols.txt`).
- **Async:** pass `CancellationToken` everywhere (forwarding it is a build error via `CA2016`); never block; no `async void`.
- **APIs:** Minimal APIs, prefer `TypedResults`; version endpoints via `Asp.Versioning.Http`; immutable `record` DTOs; never expose EF entities directly (map with Mapster).
- **Config:** Options pattern (`ValidateOnStart`); secrets via `dotnet user-secrets` locally, never in source (`.gitignore` blocks `appsettings.*.Local.json`, `.env*`, etc.).
- **Modern C#:** primary ctors, collection expressions, pattern matching, records where they help.

## Gotchas / bridges
- `EnforceCodeStyleInBuild=true` + `TreatWarningsAsErrors=true` in `Directory.Build.props` mean **any** `.editorconfig` rule with severity `:error` (code style, not just compiler warnings) fails `dotnet build`, not just the IDE.
- `bootstrap.ps1` already ran once to scaffold this repo — don't re-run it against an existing `src/`/`tests/` (it errors on `dotnet new` conflicts). It's kept for reference / in case the structure is ever wiped and regenerated. New projects created from this template don't need to run it either — the scaffolding is already baked in.
- Solution file is the new XML `.slnx` format — add loose root files (props, configs, docs) to `<Folder Name="/Solution Items/">` with `<File Path="..."/>`, not `<Project Path="..."/>` (that's only for real projects).

## Commands
`dotnet build AppTemplate.slnx` · `dotnet test AppTemplate.slnx` · `dotnet run --project src/AppTemplate.Api`
`docker compose up -d` (Postgres) · `dotnet user-secrets set "ConnectionStrings:Db" "..."` (from `src/AppTemplate.Api`)
`dotnet ef migrations add <Name> --project src/AppTemplate.Infrastructure --startup-project src/AppTemplate.Api`

CI (`.github/workflows/`): `ci-backend.yml` (PR/push to `main`, path-filtered on `src/`, `tests/`, `Directory.*.props`), `ci-web.yml` (same, for `web/`), `pr-title.yml` (Conventional Commits PR title check), `secrets-scan.yml` (gitleaks). No secrets needed — NuGet restore is scoped to `nuget.org` only.

## What to avoid
Deps in SharedKernel or Domain · EF Core/Npgsql/Dapper/ASP.NET Core packages in Application · `DateTime.Now`/`UtcNow` (build error, see `BannedSymbols.txt`), magic strings, service-locator · in-memory DB in tests (use `Testcontainers.PostgreSql`; build error, see `BannedSymbols.txt`) · `var` where an explicit type is required (build error, see `.editorconfig`) · inline `Version=` on `PackageReference` or `TargetFramework` in individual `.csproj` files · over-abstracting.

## Workflow with Claude
Don't make edits on your own initiative — **propose a plan first** (what you'd change and why) and wait for explicit approval before writing to any file. This applies beyond "non-trivial" changes: including config/build files (`.slnx`, `.csproj`, `Directory.*.props`, etc.), not just application code.
Push back on my decisions and code — you're here to review and challenge, not to rubber-stamp. If I've done something wrong or there's a better approach, say so and propose the better solution instead of silently patching around the symptom.
Don't invent problems — leave correct, idiomatic code alone. Explain each change in one line (what + why). When unsure, ask rather than guess.
Whenever an important one-off command comes up (setup, tooling, deploy, git remote, etc.), add it to the **Commands** section above so it doesn't need to be re-derived later.
