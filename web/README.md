# web

The Angular app (ADR 0032). Rules: [`CLAUDE.md`](CLAUDE.md).

```bash
npm ci                 # once, and after a package change
npm start              # the app in Croatian on http://localhost:4200, the API proxied (start the API first)
npm run start:en       # the app in its source language (English)
npm test               # unit tests (Vitest)
npm run lint           # ESLint, with the end-to-end tests' type check
npm run format         # Prettier
npm run i18n           # extract the UI texts into src/locale/messages.xlf
npm run api            # regenerate the API client from a running API's OpenAPI document
npm run e2e            # end-to-end tests (Playwright, on the production build under the shipped CSP; needs Postgres
                       # and the API's user-secrets; E2E_PORT=8080 API_URL=http://localhost:8080 for the deployed stack)
```

The first `npm run e2e` on a machine needs the browser: `npx playwright install chromium`.
