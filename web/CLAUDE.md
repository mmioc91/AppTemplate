# web (Angular frontend)

See root `CLAUDE.md` for what holds everywhere, and `docs/decisions/0032-angular-frontend-setup.md`. What to build
comes from the project's frontend brief (`docs/README.md` says where it lives).

## Rules
- **Everything in code is English**: identifiers, comments, file names and the **source texts** of the UI. The UI the
  user sees is translated through **Angular i18n**: mark every visible text — `i18n="@@area.name"` in templates,
  `i18n-aria-label="@@…"` on an attribute, `` $localize`:@@area.name:Text` `` in TypeScript — always with an explicit,
  stable id. `npm run i18n` extracts them into `src/locale/messages.xlf`; each new id needs its `<target>` in
  `messages.hr.xlf`. A missing translation fails the build (`i18nMissingTranslation: error`); an unmarked template
  text fails the lint, but an attribute or a `$localize` without an id is caught only in review. The target locale is
  the market's: change `i18n.locales` in `angular.json` when it isn't Croatian.
- **Errors are shown by `code`**, never by the API's text (it sends none): `core/http/error-messages.ts` holds each
  code's sentence (a new code a screen explains gets its line there, and its Croatian target). `problemOf` reads
  whatever the generated client throws; `applyServerErrors` puts a 400's field codes under their fields and
  `fieldErrorCode` gives client-side checks the same codes. Who shows what: 5xx and no connection get the error
  interceptor's snackbar (with the trace id); a 401 is the auth interceptor's (refresh, or sign in again); every other
  4xx is the screen's — skip the rest with `leftToInterceptors(problem)`; only the sign-in screens, which read their
  own 401 (wrong password), use `shownByInterceptor(problem)`.
- **Session** (ADR 0031, 0032): `core/auth/AuthService` — the access token in memory, the refresh token in
  `localStorage` (`TokenStore`), the current user as a signal, restored at start-up. `authGuard` on the shell,
  `guestGuard` on the sign-in pages; a return URL is followed only through `safeReturnUrl`; the sign-in pages also go
  on by themselves once a session restored late arrives. Never read or store a token anywhere else, and never call the
  refresh endpoint around `AuthService.refresh()` (one refresh at a time, across tabs). The tabs share the stored token,
  not the account on screen: a sign-out in another tab ends this tab's session at once (a `storage` event), and a
  refresh that brings another account's token (signed in in another tab) ends it too, keeping that token for its tab —
  both through `endedElsewhere`, which sends the tab to sign in.
- **The API client is generated** (`npm run api` → `src/app/core/api/api-client.ts`, NSwag from `/openapi/v1.json` of a
  running API) and committed; never edit it by hand. Regenerate it in the same PR as the backend change that alters
  the contract — `ci-e2e.yml` fails otherwise. Dates and instants are ISO **strings**; a nullable field is `T | null`
  (test with `=== null`). Tests that call a client provide `API_BASE_URL` (an empty string).
- **Structure:** `core/` exists once (api, auth, http, forms), `shared/` holds stateless UI pieces, `features/<area>/`
  holds screens with their signal-based services, `layout/` the shell. Feature routes are lazy (`loadComponent`).
  Standalone components, signals, functional interceptors and guards; no NgModules, no state library.
- **Same origin**: the client's paths start with `/api/v1` and `API_BASE_URL` is empty — locally the dev server
  proxies to the API's http profile (`proxy.conf.json`), in production one host serves both.
- **Forms**: validators that check what the API checks, the way it checks — `requiredTextChecks` (white space only is
  empty, as FluentValidation's `NotEmpty`), Angular's `minLength`/`maxLength` (FluentValidation counts the value as
  sent), limits mirrored from Application in a `*-rules.ts` (`core/auth/auth-rules.ts`); never `Validators.email` — an
  address's shape is the API's to judge. Blank optional text goes out as `null` (`textOrNull`). A required field is
  required for a screen reader (`aria-required`, from `Validators.required` — `requiredTextChecks` includes it) and
  shows no asterisk: provide `provideFormFields()` in every page or dialog that holds a form; an optional field says
  "(optional)" in its label. Its spec checks the fields with `requiredFields` and `markedFields` (`src/testing`).
- **Bundle budget**: initial warning at 500 kB; the start-up holds no Material form field or snackbar — the error
  interceptor loads the snackbar on its own, right after start-up. Keep anything the start-up imports free of
  Material.
- **Fonts** come from `@fontsource/*` through `angular.json` styles, never from a CDN (ADR 0032). Icons: add
  `@fontsource/material-symbols-outlined` the same way when the first one is needed.

## Tests
- **Unit**: Vitest via `ng test` (jsdom), `*.spec.ts` next to the code; Arrange / Act / Assert, one behaviour per
  test, no logic in tests, every test able to go red. Tests read the English source texts. HTTP through
  `provideHttpClientTesting()`; the generated client asks for blobs, so answer with `jsonBlob(...)` /
  `flushProblem(...)` (`src/testing/http.ts`). Page tests fake the services, not the HTTP client, and navigate through
  `RouterTestingHarness`. Drive Material through its test harnesses where a harness exists.
- **End to end** (`npm run e2e`): Playwright in `e2e/`, Chromium against the real API (http profile, started by
  Playwright unless running) and the app in English on port 4300; locally they need Postgres (`docker compose up -d`)
  and the API's user-secrets. Every `test` starts signed in as a user of its own (the automatic `user` fixture
  registers one through the API); a visitor's flow uses Playwright's own `test`. Set up what a flow needs through the
  API, then do the flow on the screen. Locate by role, label and text, never CSS; no fixed waits. A new screen adds
  its main flow here in the same PR.
- **CI**: `ci-web.yml` — `npm ci`, lint (with the e2e type check), format check, production build (every locale),
  unit tests; `ci-e2e.yml` — the API on a fresh Postgres, the generated client checked against it, then the end-to-end
  tests.

## Gotchas
- Prettier keeps each file's line endings (`"endOfLine": "auto"`, ADR 0021) and rewraps templates — re-read a template
  before an exact-match edit, and run `npm run i18n` after a reflow: a source text's whitespace changes with it.
- The sign-in rate limit is 10 a minute per client in production; Development raises it to 100
  (`appsettings.Development.json`, `RateLimits:AuthPerMinute`) because every e2e test registers from one address.
- Two tabs on one origin share `localStorage`, so the refresh token: signing a second account in in another tab
  signs the first tab out (by design, see Session). Test two accounts side by side in a private window or another
  origin.
- A plain `<form>` without `[formGroup]` needs `FormsModule`: with only `ReactiveFormsModule`, `(ngSubmit)` never fires
  and the browser submits the form — a page reload.
- `fixture.whenStable()` also waits for unanswered requests: after flushing one that triggers another, wait with
  `vi.waitFor` on the next expectation instead.
