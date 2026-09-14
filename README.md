# AppTemplate — template for Clean Architecture / DDD / vertical slice projects

Base repo for new portfolio projects: fork / "Use this template", then rename
`AppTemplate` to the new project's name.

Backend: ASP.NET Core (.NET 10), Clean Architecture, PostgreSQL.
Frontend (later): Angular PWA in `web/`.

---

## New project from this template

```powershell
# 1. On GitHub: "Use this template" -> Create a new repository
# 2. Clone the new repo locally, then from its root:
powershell -ExecutionPolicy Bypass -File .\rename-template.ps1 -NewName Slicice

# This renames "AppTemplate" -> "Slicice" (and "apptemplate" -> "slicice") in both
# file contents and file/folder names (namespaces, csproj, sln, docker-compose, README...).

# 3. Delete rename-template.ps1 (no longer needed in the new project), then commit.
```

---

## Getting started

```powershell
# 1. Start local infrastructure
docker compose up -d

# 2. Secrets (NOT committed to git)
cd src/AppTemplate.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Db" "Host=localhost;Database=apptemplate;Username=postgres;Password=dev"
dotnet user-secrets set "Jwt:Key" "dev-secret-at-least-32-chars-long-1234567890"
cd ../..

# 3. Run
dotnet run --project src/AppTemplate.Api
```

---

## Project structure

```
.
├─ src/
│  ├─ AppTemplate.Domain/           entities, invariants, value objects  (no dependencies)
│  ├─ AppTemplate.Application/      use cases, validators, ports         (-> Domain)
│  ├─ AppTemplate.Infrastructure/   EF Core, repositories, adapters       (-> Application)
│  └─ AppTemplate.Api/              endpoints, auth, composition root     (-> App + Infra)
├─ tests/
│  ├─ AppTemplate.Domain.Tests/         unit
│  ├─ AppTemplate.Application.Tests/    unit (fake ports)
│  ├─ AppTemplate.Integration.Tests/    Testcontainers + real database
│  └─ AppTemplate.Architecture.Tests/   layer boundaries (NetArchTest)
├─ web/                         Angular (coming later)
├─ infra/                       Bicep (coming later)
├─ .github/workflows/           CI
├─ .githooks/                   commit-msg, pre-push
├─ Directory.Build.props        shared MSBuild settings
└─ Directory.Packages.props     package versions in one place (CPM)
```

**Dependency direction is a rule, not a suggestion:** Domain depends on nothing; Api is the only project that knows about Infrastructure (for DI registration). Architecture tests verify this at build time.

---

## Conventions

- **Branches:** `<type>/<task-id>-<description>` → `feat/fe-033-rapid-numpad`
  Types: `feat fix hotfix refactor perf test docs chore ci infra`
- **Commits:** Conventional Commits → `feat(inventory): add rapid numpad entry`
- **No direct commits to `main`** — branch + pull request + squash merge.
- **Hooks** live in `.githooks/` and are activated with `git config core.hooksPath .githooks`.
  - `commit-msg` → message format
  - `pre-push` → branch name, blocks push to `main`, fast unit tests

Unfinished work goes into `main` **behind a feature flag**, not on a long-lived branch.

---

## CI

| Workflow | When it runs |
|---|---|
| `backend-ci` | PR / push to `main` with changes in `src/` or `tests/` |
| `web-ci` | same, for `web/` |
| `pr-title` | on every PR (PR title = commit on `main` due to squash merge) |

After the first push to GitHub, enable **branch protection** for `main`:
Require PR + required status checks (`build-test`, `lint`), **without** "Require approvals"
(a solo dev can't approve their own PR).
