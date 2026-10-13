# AppTemplate — template for Clean Architecture / DDD projects

Base repo for new projects: "Use this template" on GitHub, then rename `AppTemplate` to the new project's name.

Backend: ASP.NET Core (.NET 10), Clean Architecture layered by project, PostgreSQL.
Frontend (later): Angular PWA in `web/`.

It is a walking skeleton, not an empty scaffold: the cross-cutting plumbing every project needs is already built,
tested and documented, so the first commit of a new project can be its first use case.

---

## New project from this template

```powershell
# 1. On GitHub: "Use this template" -> Create a new repository
# 2. Clone the new repo locally, then from its root:
powershell -ExecutionPolicy Bypass -File .\rename-template.ps1 -NewName Slicice

# This renames "AppTemplate" -> "Slicice" (and "apptemplate" -> "slicice") in both
# file contents and file/folder names (namespaces, csproj, sln, docker-compose, README...),
# drops the script's own entry from the .slnx and lists any leftover "AppTemplate" it couldn't rewrite.

# 3. Delete rename-template.ps1 (no longer needed in the new project), then commit.
# 4. In CLAUDE.md: describe the product and link its design docs (Read first);
#    in src/*.Domain/CLAUDE.md: the model. Settle the open questions in docs/decisions/README.md as they come up.
#    Design docs written before the fork? Add an ADR saying this template's conventions win on *how* where the two
#    disagree (the docs stay the truth for *what*), with the known differences listed, so every later conflict has
#    one answer.
```

---

## Getting started

```powershell
# 0. Once per clone: git hooks and local tools (dotnet-ef, dotnet-stryker)
git config core.hooksPath .githooks
dotnet tool restore

# 1. Start local infrastructure
docker compose up -d

# 2. Secrets (NOT committed to git)
cd src/AppTemplate.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Db" "Host=localhost;Database=apptemplate;Username=postgres;Password=dev"
cd ../..

# 3. Run (Development applies migrations on startup and serves /openapi/v1.json)
dotnet run --project src/AppTemplate.Api
```

Integration tests start their own Postgres with Testcontainers — Docker must be running.

---

## What's already built

| Area | What | Where |
|---|---|---|
| Building blocks | `Entity<TId>`, `AggregateRoot<TId>`, `Enumeration<TEnum>` (smart enum), `DomainException(code, message)` | `src/*.SharedKernel` |
| Use cases | `Result` / `Result<T>` + `Error` (code + kind), `ICommandHandler`/`IQueryHandler`, validation decorator with error codes, assembly-scanning `AddApplication()` (no mediator library) | `src/*.Application/Common` |
| Persistence | `AppDbContext` (snake_case, UTC `DateTimeOffset`, smart-enum converter), `IUnitOfWork` that turns unique violations and concurrency conflicts into Application exceptions, retrying Npgsql, design-time factory for migrations | `src/*.Infrastructure` |
| HTTP | ProblemDetails with a `code` on every error (422 domain, 400 validation, 404/409/403, framework errors), API versioning + built-in OpenAPI per version, JSON enums by name, `/health/live` + `/health/ready` | `src/*.Api` |
| Tests | Postgres fixture + `WebApplicationFactory` on the real DI setup, error-contract tests, DI tests that prove every handler is validated, architecture tests | `tests/` |
| Decisions | 20 baseline ADRs + open questions (auth scheme, tenancy, hosting, idempotency, logging) | `docs/decisions/` |
| Claude Code | `CLAUDE.md` per layer and test project, project reviewer agent | `CLAUDE.md`, `.claude/agents/` |

---

## Project structure

```
.
├─ src/
│  ├─ AppTemplate.SharedKernel/     generic DDD building blocks           (no dependencies)
│  ├─ AppTemplate.Domain/           aggregates, invariants, value objects (-> SharedKernel)
│  ├─ AppTemplate.Application/      use cases, validators, ports          (-> Domain)
│  ├─ AppTemplate.Infrastructure/   EF Core, repositories, adapters       (-> Application)
│  └─ AppTemplate.Api/              endpoints, errors, composition root   (-> App + Infra)
├─ tests/
│  ├─ CLAUDE.md                         conventions shared by all test projects
│  ├─ AppTemplate.Domain.Tests/         unit
│  ├─ AppTemplate.Application.Tests/    unit (fake ports)
│  ├─ AppTemplate.Integration.Tests/    Testcontainers Postgres + WebApplicationFactory
│  └─ AppTemplate.Architecture.Tests/   layer boundaries (NetArchTest)
├─ docs/decisions/              ADRs (one per decision) + open questions
├─ .claude/agents/              Claude Code agents (project-rules reviewer)
├─ web/                         Angular (coming later)
├─ .github/workflows/           CI
├─ .githooks/                   commit-msg, pre-push
├─ .config/dotnet-tools.json    local tools: dotnet-ef, dotnet-stryker
├─ Directory.Build.props        shared MSBuild settings (warnings as errors, code style in build)
└─ Directory.Packages.props     package versions in one place (CPM)
```

**Dependency direction is a rule, not a suggestion:** SharedKernel and Domain depend on nothing else; Api is the only
project that knows about Infrastructure (for DI registration). Architecture tests verify this at build time.

---

## Conventions

- **Branches:** `<type>/<description>` (optionally `<type>/<task-id>-<description>`) → `feat/order-aggregate`;
  `claude/<name>` for the worktree branches the Claude desktop app creates.
  Types: `feat fix hotfix refactor perf test docs chore ci infra`
- **Commits:** Conventional Commits → `feat(orders): add the order aggregate`; a `!` before the colon marks a
  breaking change (`feat!: …`).
- **No direct commits to `main`** — branch + pull request + squash merge.
- **Hooks** live in `.githooks/` and are activated with `git config core.hooksPath .githooks`. They are stored with
  the executable bit (`git add --chmod=+x .githooks/<hook>` for a new one — Windows never records it, and git on
  Linux/macOS skips a hook stored without it) and with LF line endings (`.gitattributes`, ADR 0021).
  - `commit-msg` → message format
  - `pre-push` → branch name, blocks push to `main`, fast unit tests
- **Decisions:** a choice that isn't obvious from the code gets an ADR in `docs/decisions/` in the same change.

Unfinished work goes into `main` **behind a feature flag**, not on a long-lived branch.

---

## CI

| Workflow | When it runs |
|---|---|
| `backend-ci` | every PR (not path-filtered: `build-test` is a required check); push to `main` with changes in `src/`, `tests/` or `Directory.*.props`; on PRs also report-only mutation testing (Stryker) of changed Domain/Application code, skipped with a notice when a PR changes more than 40 C# files of the project |
| `web-ci` | PR / push to `main` with changes in `web/`: lint, format check, production build and Vitest (`web-build-test`) |
| `pr-title` | on every PR (PR title = commit on `main` due to squash merge) |
| `secrets-scan` | gitleaks on every PR / push |

After the first push to GitHub, enable **branch protection** for `main`:
Require PR + required status checks (`build-test`, `lint`), **without** "Require approvals"
(a solo dev can't approve their own PR). Don't make `mutation` a required check — it is report-only — nor
`web-build-test`: it is path-filtered, so a PR that doesn't touch `web/` would wait for it forever.
A private repository on GitHub Free has no branch protection or rulesets at all; until the account is on Pro or Team,
`main` changes only through pull requests merged once the checks are green — by convention, plus the local `pre-push`
hook. Record that in an ADR so nobody later mistakes it for an oversight.
