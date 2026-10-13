# 0029. Hosting: one origin behind a reverse proxy, migrations as a deploy step

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
The open question "Hosting" held what the first deployment has to settle: forwarded headers behind the proxy,
`AllowedHosts`, which health probe does what, and how migrations run. One project built from this template settled
all of it on one Linux server with Docker and Caddy; the other still runs locally. The code-side answers are the
same whatever the platform, so they belong in the template; the platform itself does not.

## Decision
- **One origin:** a reverse proxy serves the web app and forwards `/api`, `/health` (and a live hub, if any) to the
  Api, so there is no CORS and no second host to configure.
- **Forwarded headers** (`X-Forwarded-For`, `X-Forwarded-Proto`) are trusted from the networks listed in
  `Proxy:KnownNetworks` (CIDR, comma-separated) next to loopback, and from nowhere else; without the setting, from
  loopback only. `UseForwardedHeaders` runs first in the pipeline, so per-IP rate limits and logs see the real client.
- **`AllowedHosts`** is the public host in production configuration, plus the proxy's internal name for its health
  checks — never `*`.
- **Probes** ([0019](0019-liveness-and-readiness-health-checks.md)): the proxy health-checks `/health/ready` and takes
  a failing Api out of rotation; an orchestrator restarts on `/health/live`.
- **Migrations are a deploy step:** the Api image started with `Database__MigrateAndExit=true` migrates the database
  and exits, and the new version serves only after that succeeded. Production never migrates on startup; Development
  still does.
- **Unknown routes answer 404 before authorization** (`UseNotFoundForUnmatchedRoutes`), so a fallback policy that
  requires a signed-in user never hides a missing route behind a 401 ([0018](0018-http-error-contract.md)). That holds
  only while `UseAuthentication()`/`UseAuthorization()` are called explicitly after it; `WebApplication` would
  otherwise insert them ahead of every user middleware.
- **The platform is the project's call**, recorded with its `deploy/` files: one Linux VM with Docker Compose
  (Postgres with a volume, a one-shot migrate service, the Api, Caddy with automatic HTTPS and the security headers,
  a nightly `pg_dump`) is the proven setup; a managed platform runs the same images with its proxy's network in
  `Proxy:KnownNetworks`.

## Consequences
- A deploy that skipped the migration step stays out of rotation (503 on `/health/ready`) without being restarted.
- A loose forwarded-headers setup would let any client spoof its address and scheme; the setting names the proxy.
- The runtime image needs time-zone data (tzdata) if the app does calendar arithmetic; a chiseled image needs its
  `-extra` variant.
- Data Protection keys matter only once cookies are used; the auth decision says where they live.

## Alternatives considered
- **Migrate on startup in production** — rejected: several instances would race, and a failed migration would
  restart forever instead of failing the deploy.
- **Serve the web app from the Api** — rejected: the static files and their caching rules belong to the proxy, and
  the Api stays an Api.
- **Trust every forwarded header** — rejected: spoofed client addresses pass every per-IP limit.
