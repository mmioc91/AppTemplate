# AppTemplate — template for Clean Architecture / DDD projects

Base repo for new projects: "Use this template" on GitHub, then rename `AppTemplate` to the new project's name.

Backend: ASP.NET Core (.NET 10), Clean Architecture layered by project, PostgreSQL.
Frontend: Angular in `web/` (ADR 0032). Deployment: Docker Compose with Caddy in front, in `deploy/` (ADR 0033). Mail: a transactional outbox with SMTP delivery (ADR 0034).

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
# 4. In CLAUDE.md: describe the product and link its design docs (Read first); in docs/README.md: fill in the
#    document map; in docs/implementation-plan.md: the first steps; in src/*.Domain/CLAUDE.md: the model.
#    Settle the open questions in docs/decisions/README.md as they come up.
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

# 1. Start local infrastructure: Postgres, and Mailpit catching the mail the Api sends (http://localhost:8025)
docker compose up -d

# 2. Secrets (NOT committed to git)
cd src/AppTemplate.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Db" "Host=localhost;Database=apptemplate;Username=postgres;Password=dev"
# The JWT signing key: 32+ random bytes in base64 (bash: openssl rand -base64 32); the Api refuses to start without it
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))
cd ../..

# 3. Run (Development applies migrations on startup and serves /openapi/v1.json)
dotnet run --project src/AppTemplate.Api

# Production (ADR 0028, 0029, 0031): logs are JSON lines on the console; migrations are a deploy step — the same build
# with Database__MigrateAndExit=true migrates and exits; forwarded headers are trusted from Proxy__KnownNetworks only;
# set AllowedHosts to the real host and Jwt__SigningKey to a key of its own.
```

The web app (`web/README.md` for the rest):

```powershell
cd web
npm ci
npm start    # http://localhost:4200, the API proxied — start the API first
```

Integration tests start their own Postgres with Testcontainers — Docker must be running.

The whole stack as it ships — Postgres, migrations, the Api, Caddy with the app — runs locally from `deploy/`
(`deploy/README.md`); a `v…` tag deploys it to a server.

---

## What's already built

| Area | What | Where |
|---|---|---|
| Building blocks | `Entity<TId>`, `AggregateRoot<TId>`, `Enumeration<TEnum>` (smart enum), `DomainException(code, message)` | `src/*.SharedKernel` |
| Use cases | `Result` / `Result<T>` + `Error` (code + kind), `ICommandHandler`/`IQueryHandler`, logging and validation decorators (name, duration and outcome in the log, never the input; error codes for the client) for commands and queries, `PagedList<T>` for lists that can grow, assembly-scanning `AddApplication()` (no mediator library) | `src/*.Application/Common` |
| Auth | ASP.NET Core Identity + JWT bearer (ADR 0031): register / login / refresh / logout / me, hashed and rotating refresh tokens with family revoke and a reload-safe grace, a fallback policy (every endpoint protected unless `.AllowAnonymous()`), `ICurrentUser`, one 401 code for every login refusal at the cost of one password hash, lockout, per-IP rate limits (429 + `Retry-After`), roles ready to seed and build policies on | `src/*.Application/Auth`, `src/*.Infrastructure/Identity`, `src/*.Api/Auth` |
| Mail | A transactional outbox (ADR 0034): `IMailQueue` commits the mail with the use case's change; a hosted worker claims batches with `FOR UPDATE SKIP LOCKED` and a lease, sends over SMTP (MailKit) outside any transaction, backs off on failure and purges after 30 days; Handlebars templates in the app's layout, links to the web app from `IWebLinks`; Mailpit locally and in CI | `src/*.Application/Mail`, `src/*.Infrastructure/Mail`, `src/*.Infrastructure/Web` |
| Persistence | `AppDbContext` (snake_case, UTC `DateTimeOffset`, smart-enum converter, Identity's tables renamed to match), the first migration, `IUnitOfWork` that turns unique violations and concurrency conflicts into Application exceptions (and `DiscardChanges()` to retry), retrying Npgsql, design-time factory for migrations, a reference-data seeding hook that runs after every migration | `src/*.Infrastructure` |
| HTTP | ProblemDetails with a `code` and the W3C `traceId` on every error (422 domain, 400 validation, 401/403/404/409, framework errors, 429), API versioning + built-in OpenAPI per version typed for a generated client, strict JSON (enums by name, instants with an offset, numbers as numbers), `/health/live` + `/health/ready`, forwarded headers from known proxies only, migrations as a deploy step, JSON logs in production | `src/*.Api` |
| Web | Angular 22 + Material 3: sign-in and registration, the session (access token in memory, rotating refresh token, one refresh across tabs), guards, errors shown by code, forms that check what the API checks, Angular i18n (English source, Croatian translation), an NSwag client generated from the OpenAPI document, Vitest and Playwright tests | `web/` |
| Deploy | `deploy/`: Docker Compose (Postgres, a migrate step that runs before the Api serves, the Api, Caddy), two Dockerfiles, Caddy with automatic HTTPS, security headers and the app's Content-Security-Policy (the e2e tests run under it), caching that lets a release reach everyone, a backup script, a GitHub workflow that builds the images on a tag and restarts the server's stack | `deploy/`, `.github/workflows/deploy.yml` |
| Tests | Postgres fixture + `WebApplicationFactory` on the real DI setup (a fake clock, extra settings or an empty database on request), an auth client for the tests behind sign-in, error-contract tests, DI tests that prove every handler is logged and validated, architecture tests | `tests/` |
| Decisions | 34 baseline ADRs + open questions (tenancy, account deletion, operator access, quota races) | `docs/decisions/` |
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
├─ docs/
│  ├─ README.md                 documentation map: what to read, build order (placeholders to fill in)
│  ├─ implementation-plan.md    the ordered steps, ticked off as they land
│  ├─ ubiquitous-language.md    UI terms and the code names for them
│  └─ decisions/                ADRs (one per decision) + open questions
├─ .claude/                     Claude Code: the project-rules reviewer agent, the shared command allowlist
├─ web/                         Angular app: sign-in, a generated API client, i18n, Vitest + Playwright
├─ deploy/                      Docker Compose stack, Dockerfiles, Caddyfile (HTTPS, headers, CSP), backup script
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
| `backend-ci` | every PR (not path-filtered: `build-test` is a required check); push to `main` with changes in `src/`, `tests/`, `Directory.*.props` or the workflow itself; on PRs also report-only mutation testing (Stryker) of changed Domain/Application code, skipped with a notice when a PR changes more than 40 C# files of the project |
| `web-ci` | PR / push to `main` with changes in `web/`: `npm run lint`, `npm run format:check`, a production build and `npm test` (Vitest) in the `web-build-test` job — `web/package.json` must provide exactly those scripts (Prettier with `"endOfLine": "auto"`, ADR 0021) |
| `e2e` | PR / push to `main` with changes in `web/`, `src/`, `Directory.*.props` or `deploy/Caddyfile`: the API on a fresh Postgres, the committed API client checked against a regenerated one, then Playwright on the production build under the shipped Content-Security-Policy (`web-e2e`) |
| `deploy` | a `v*` tag, or by hand: both images built and pushed to GHCR; with the deploy variables set, the server's stack restarted over SSH (`deploy/README.md`) |
| `pr-title` | on every PR (PR title = commit on `main` due to squash merge) |
| `secrets-scan` | gitleaks on every PR / push |

After the first push to GitHub, enable **branch protection** for `main`:
Require PR + required status checks (`build-test`, `lint`), **without** "Require approvals"
(a solo dev can't approve their own PR). Don't make `mutation` a required check — it is report-only — nor
`web-build-test` or `web-e2e`: they are path-filtered, so a PR that doesn't touch them would wait forever.
A private repository on GitHub Free has no branch protection or rulesets at all; until the account is on Pro or Team,
`main` changes only through pull requests merged once the checks are green — by convention, plus the local `pre-push`
hook. Record that in an ADR so nobody later mistakes it for an oversight.
