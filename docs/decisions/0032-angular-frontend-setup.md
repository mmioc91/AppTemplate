# 0032. The web app: Angular, translated through i18n, on a generated client

- **Status:** Accepted
- **Date:** 2026-10-16

## Context
The template promised "an Angular PWA in `web/`, later". Two projects built from it each wrote one, and agreed on most
of it — standalone components and signals, Material, Vitest, a same-origin dev proxy, errors shown by their code —
but differed in three places: where the UI texts live, whether the API client is written or generated, and what the
end-to-end tests run against. With sign-in in the API ([0031](0031-identity-with-jwt-bearer-tokens.md)), the first
screen of every project is the same, so the template can ship it.

## Decision
**Angular 22, CLI defaults:** standalone components, zoneless, signals; strict TypeScript; Angular Material 3 with its
system variables; lazy routes. Fonts are self-hosted (`@fontsource`), never loaded from a CDN — no visitor's address
goes to a third party (GDPR) and a Content-Security-Policy can stay at `'self'`. No service worker by default: a project
that needs offline adds `@angular/service-worker` and says so in an ADR.

**Texts through Angular i18n.** Everything in the code is English, the UI's source texts included; each text is marked
with an explicit, stable id (`i18n="@@area.name"`, `` $localize`:@@area.name:…` ``), extracted into
`src/locale/messages.xlf`, and translated in `messages.<locale>.xlf` — the template ships `hr`. A missing translation
fails the build, and a template text without an id fails the lint (`@angular-eslint/template/i18n`). The production
build makes one app per locale.

**A generated client.** NSwag generates `core/api/api-client.ts` from the API's OpenAPI document (`npm run api`), and it
is committed; nobody edits it. Dates and instants stay ISO strings; nullable fields are `T | null`. CI regenerates it
against the API and fails when the committed one differs, so a contract change and its client go in one PR.

**Same origin.** The client's paths start with `/api/v1`; `API_BASE_URL` is empty. Locally the dev server proxies
`/api` and `/health` to the API (`proxy.conf.json`, the http profile); in production one host serves both
([0029](0029-hosting-behind-a-proxy-with-migrations-as-a-deploy-step.md)). The API has no CORS.

**The session, client side** ([0031](0031-identity-with-jwt-bearer-tokens.md)): the access token in memory, the refresh
token in `localStorage`; the app's start restores the session from it, waiting at most five seconds before the first
page. One refresh at a time — parallel 401s share it, and tabs take turns through Web Locks, because two tabs spending
one token would look like a stolen copy. The auth interceptor puts the bearer on every API call except the sign-in
calls, refreshes once on a 401 and repeats the call; only a refused refresh, or a refresh token gone because another tab
signed out, ends the session and goes to the sign-in page with a return URL (followed only within the app). A lost
connection keeps the session. The tabs share the stored token but not the account on screen: a sign-out in another tab
ends this one's session at once (a `storage` event), and a refresh that brings another account's token — signed in in
another tab — ends this tab's session instead of going on as that account, keeping the token for the tab it belongs
to. Without storage (a browser blocking site data) the token stays in memory, for as long as the tab.

**Errors by code.** The API sends codes, never texts the user reads ([0018](0018-http-error-contract.md)); the app owns
every sentence (`core/http/error-messages.ts`), translated like the rest. `problemOf` reads whatever the generated client
throws; `applyServerErrors` puts a 400's field codes under their fields; forms check what the API checks, the way it
checks — never refusing what it accepts. The error interceptor tells about a server failure (with the trace id,
[0028](0028-logging-and-the-trace-id.md)) and a lost connection; every 4xx is the screen's to explain.

**Tests.** Vitest on jsdom through `ng test`, next to the code. Playwright end-to-end tests in `e2e/` run Chromium
against the real API and database, the app in its source language; every test registers its own user through the API.
`ci-web.yml` lints, checks the format, builds every locale and runs the unit tests; `ci-e2e.yml` runs the API on a fresh
Postgres, checks the generated client and runs the end-to-end tests. Neither is a required check (both are
path-filtered).

**The sign-in rate limit is the host's setting** (`RateLimits:AuthPerMinute`, 10 by default, as [0031](0031-identity-with-jwt-bearer-tokens.md)
decided): the end-to-end suite registers a user per test from one address, so Development raises it to 100 and the
API's integration tests pin the production 10.

## Consequences
- A new screen: its texts marked with ids and translated, its errors named by code, its main flow in `e2e/`.
- A contract change regenerates the client in the same PR, or `e2e` goes red.
- The refresh token in `localStorage` can be read by script running in the page: the app loads nothing from elsewhere,
  and a CSP at the proxy (with `deploy/`) closes most of the rest. A project that is only a same-origin SPA may prefer
  the cookie scheme (ADR 0031's alternative).
- The start-up bundle holds no Material form fields or snackbar; a form's page provides `provideFormFields()`, and the
  snackbar loads beside the first screen.

## Alternatives considered
- **UI texts in a typed TypeScript object, in the market's language** — simple, and a missing key is a compile error;
  rejected as the default because the code would no longer be all English, and a second language means a rewrite.
- **A hand-written client** — fewer moving parts, but every DTO written twice and drifting silently from the contract.
- **End-to-end tests against the production build served with the API** — closer to what ships (CSP included); waits
  for `deploy/`, which brings the proxy and its headers.
- **A service worker by default** — offline is a product decision with its own costs (update prompts, a stale shell).
