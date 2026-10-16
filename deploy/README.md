# Deploying

One Linux server with Docker runs everything (ADR 0033): Postgres, a migration step, the Api, and Caddy serving the
web app with the Api behind it on one origin — HTTPS included. Nothing here is specific to a product: a project built
from the template deploys with a filled-in env file and a version tag.

## Try it locally

```bash
cp deploy/env.example deploy/.env
# then, in deploy/.env: SITE_ADDRESS=:8080, ALLOWED_HOSTS=localhost, the two secrets (openssl rand -base64 32),
# and HTTP_PORT/HTTPS_PORT/LOCAL_PORT set to free ports if 80, 443 or 8080 are taken
docker compose -f deploy/compose.yml --env-file deploy/.env up -d --build
```

The app is on http://localhost:8080 (`LOCAL_PORT`): Croatian at `/`, English under `/en/`. The end-to-end tests run
against it with `E2E_PORT=8080 API_URL=http://localhost:8080 npm run e2e` from `web/` — with
`AUTH_RATE_LIMIT_PER_MINUTE=100` in the env file for that run, since the suite registers an account per test from one
address (ADR 0032). `docker compose -f deploy/compose.yml --env-file deploy/.env down` stops it (`-v` drops its data too).

A second stack of this template on the same machine (another project) needs its own `EDGE_SUBNET` and ports, or Docker
refuses the network.

## First time on a server

1. A Linux VM (in the EU when the data is European users' personal data) with Docker Engine and the compose plugin;
   ports 80 and 443 open; a DNS name pointing at it.
2. On the server: `mkdir -p /opt/apptemplate` and put the filled-in env file there as `/opt/apptemplate/.env` (from
   `env.example`: the domain in `SITE_ADDRESS` and `ALLOWED_HOSTS`; secrets from `openssl rand -base64 32`). Keep it
   readable by the deploying user only (`chmod 600`).
3. Let the server read the images: `docker login ghcr.io` with a token that may read packages (GitHub packages are
   private by default).
4. In the GitHub repository: variables `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_KNOWN_HOSTS` (the output of
   `ssh-keyscan <host>`, checked by hand), optionally `DEPLOY_DIR` (default `/opt/apptemplate`); secret
   `DEPLOY_SSH_KEY` (a key only for deploying, its public half in the deploying user's `authorized_keys`).
5. Push a tag `v…`: the `deploy` workflow builds both images, pushes them to GHCR, copies `compose.yml` and `backup.sh`
   to the server and starts the stack. The migration runs first; the Api starts only once it succeeded.

Without the variables the workflow only builds and pushes the images — for a server set up by hand, or another host.

## Running it

- **Health:** Caddy takes the Api out of rotation while `/health/ready` fails; an orchestrator would restart on
  `/health/live` (ADR 0019).
- **Logs:** `docker compose --env-file .env logs api` — JSON lines; a user's error reference is the `traceId` of a
  ProblemDetails, the `TraceId` in every log entry of that request (ADR 0028).
- **Backups:** `backup.sh` (copied next to `compose.yml` by the deploy) dumps the database into `backups/` and keeps
  the newest 14. Run it nightly from cron: `crontab -e`, then
  `15 3 * * * /opt/apptemplate/backup.sh >> /opt/apptemplate/backups/backup.log 2>&1`. A copy on the server's own disk
  goes down with the server, so copy `backups/` off it too (your provider's object storage with rclone, or another host
  with scp). To restore one: `docker compose --env-file .env stop api`, then
  `docker compose --env-file .env exec -T db pg_restore --username=apptemplate --dbname=apptemplate --clean --if-exists < backups/apptemplate-….dump`,
  then `docker compose --env-file .env start api`. Try a restore into a scratch database once, before you need it.
- **Settings:** a changed value in `.env` is applied by `docker compose --env-file .env up -d` — the Api restarts with
  it, nothing is rebuilt.
- **Security headers** (ADR 0033): Caddy sends HSTS, `X-Frame-Options`, `nosniff`, the referrer policy and the app's
  Content-Security-Policy, and drops the `Server` header. Anything new the app loads from another site (a script, a
  font, an embed) needs that policy in `Caddyfile` changed first — the end-to-end tests run under it and fail on a
  violation.
- **One Api instance.** More than one needs the rate limiter's windows shared (today they are per instance) and, for
  anything real-time, a backplane — not part of this setup.
