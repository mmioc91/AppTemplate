# 0033. Deployment: one host with Docker Compose, Caddy in front, the security headers and the CSP at the proxy

- **Status:** Accepted
- **Date:** 2026-10-16
- **Changes:** [0032](0032-angular-frontend-setup.md) (the end-to-end tests now run on the production build under the
  shipped policy, which 0032 had deferred to this decision)

## Context
[0029](0029-hosting-behind-a-proxy-with-migrations-as-a-deploy-step.md) settled the code side of hosting — one origin
behind a reverse proxy, forwarded headers from known proxies only, migrations as a deploy step — and left open where
the code runs. Both projects built from this template ran on one Linux VM with Docker Compose and Caddy, one of them
proven with a smoke stack and a load test. A template that ships the files makes a project's first deploy a filled-in
env file and a version tag; a project that outgrows one host moves on with the images it already builds.

## Decision
**Four services in `deploy/compose.yml`:** `db` (Postgres 17 on a named volume, with a health check); `migrate` (the
Api image started with `Database__MigrateAndExit=true`, which exits when the schema is current — the Api waits for it
to complete successfully, so a new version never serves an old schema); `api` (the Production image, non-root, port
8080 on the internal networks only, every setting from the env file); `web` (Caddy, serving the Angular build and
forwarding `/api/*` and `/health/*` to the Api, on ports 80 and 443 — UDP too, for HTTP/3 — and a plain-HTTP port for a
local try). Two networks: `backend` for the database, and `edge` between Caddy and the Api with a **fixed subnet**
(`EDGE_SUBNET`), so the Api can name exactly its proxy as the one sender whose `X-Forwarded-For` it believes
(`Proxy__KnownNetworks`, 0029); `AllowedHosts` is the public host plus `api`, the name Caddy's health checks use.

**Two images** from `deploy/api.Dockerfile` and `deploy/web.Dockerfile`, built from the repository root (`.dockerignore`
keeps tests, docs and local secrets out): the Api published by the SDK image into the ASP.NET runtime image under its
non-root user; the app built by Node 22 into a Caddy image with the `Caddyfile` and the build under `/srv`.

**Caddy** gets and renews the certificate for the domain in `SITE_ADDRESS` (`:8080` serves plain HTTP for a local try),
compresses, and sends the security headers on every answer: HSTS for a year **without** `includeSubDomains` (other
hosts of the domain aren't ours to vouch for), `X-Content-Type-Options: nosniff`, `Referrer-Policy:
strict-origin-when-cross-origin`, `X-Frame-Options: SAMEORIGIN`, and no `Server` header. The app's pages carry a
**Content-Security-Policy**: `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self'
data:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors
'self'`. Scripts only the app's own; inline styles because Angular's component styles are inline; everything else from
this origin — which is why the fonts are self-hosted (0032) and why the production build keeps critical-CSS inlining
off (it would load the styles through an inline script the policy blocks, and the app would come up unstyled).
Anything the app loads from another site changes the policy first. **The end-to-end tests run under the policy that
ships:** `web/e2e/serve.mjs` serves the production build the way Caddy does and reads the policy from the `Caddyfile`;
every page of every test watches for violations, and one fails the test.

**Caching:** the hashed bundles and the fonts are immutable for a year; everything else — `index.html` above all, also
as the answer to a deep link — is `no-cache`, revalidated on every load, so a release reaches everyone at once.
**Languages:** the market's language at `/`, the source language under `/en/` (0032); Caddy answers a deep link with
its language's `index.html`.

**The deploy workflow** (`deploy.yml`): a tag `v*`, or a manual run, builds both images and pushes them to GitHub's
registry as `ghcr.io/<owner>/<repo>/api:<tag>` and `…/web:<tag>`. With the repository variables `DEPLOY_HOST`,
`DEPLOY_USER`, `DEPLOY_KNOWN_HOSTS` (and `DEPLOY_DIR`) and the secret `DEPLOY_SSH_KEY` set, it then copies
`compose.yml` and `backup.sh` to the server and runs `docker compose up -d --no-build` with the new image names;
without them it only builds, for a server set up by hand.

**Backups:** `deploy/backup.sh` — `pg_dump` in custom format from the `db` container, written under a temporary name
and renamed only when complete, the newest 14 kept — from the server's cron, with a copy taken off the host.

**Logs:** JSON lines (0028); EF Core's per-statement logging is off in Production (`appsettings.Production.json`) —
one line per SQL statement at Information was noise around the lines that matter.

## Consequences
- A project deploys with `deploy/env.example` filled in and a tag; its first server is set up in five steps
  (`deploy/README.md`).
- What the app loads is a decision: a new third-party script, font or embed means a `Caddyfile` change, reviewed, and
  the end-to-end tests fail until it is made.
- One Api instance. More than one needs the rate limiter's windows shared (they are per instance today) and, for
  anything real-time, a backplane. The images and the compose file are the starting point for that, not the answer.
- The database and Caddy's certificates live in Docker volumes on the host; the backups are the project's duty, and a
  restore is to be rehearsed once before it is needed.
- The env file holds the secrets, readable by the deploying user only; GitHub's packages are private by default, so the
  server logs in to the registry once.
- A second stack of the template on one host (two projects) needs its own `EDGE_SUBNET` and host ports.
- The open question "Hosting platform" is settled.

## Alternatives considered
- **A managed platform** (App Service, Container Apps, Fly.io, …) — fine for a project that wants no server; each
  brings its own proxy, so the headers, the policy and the forwarded-headers trust move there, and the images stay.
- **Nginx in front** — proven too, but Caddy gets and renews the certificate itself and its configuration for this is a
  page.
- **Kubernetes** — far more than one host needs.
- **Building the images on the server** (`docker compose up --build`) — slower deploys, and the server needs the SDK
  and Node; CI builds once and the server pulls.
- **The CSP in the app's `index.html`** (a `<meta>` tag) — it can't set `frame-ancestors`, and the headers belong to
  the host, where one place serves both the app and the Api.
- **Migrating on the Api's start in production** — rejected in 0029; the `migrate` service is that decision in compose.
