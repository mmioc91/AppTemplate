import { defineConfig, devices } from '@playwright/test';

const ci = !!process.env['CI'];
const port = Number(process.env['E2E_PORT'] ?? 4400);
const apiUrl = process.env['API_URL'] ?? 'http://localhost:5062';

// End-to-end tests (ADR 0032, 0033): the main flows in a real browser against the real API and database, on the
// production build served the way the host serves it — one origin, the market's language at /, the source language
// under /en/ (where the tests run), under the Content-Security-Policy from deploy/Caddyfile (e2e/serve.mjs). Playwright
// starts what isn't running yet: the API with the http profile, and the build with its server. Locally the API uses
// the database from `docker compose up -d` and its user-secrets; CI starts the API itself on a fresh database.
// Against the deployed stack instead: E2E_PORT=8080 API_URL=http://localhost:8080 (deploy/README.md).
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: ci,
  // A flaky test is fixed, not retried: a retry would hide a race a user can meet too.
  retries: 0,
  workers: ci ? 2 : undefined,
  reporter: ci ? [['github'], ['html', { open: 'never' }]] : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: `http://localhost:${port}`,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'dotnet run --project ../src/AppTemplate.Api --launch-profile http',
      // Ready once the database answers and every migration is applied (ADR 0019).
      url: `${apiUrl}/health/ready`,
      // Reused when running: CI starts it first (ci-e2e.yml), to check the generated client against it, and keeps its
      // log; locally a running API is the developer's own.
      reuseExistingServer: true,
      timeout: 240_000,
    },
    {
      command: 'npm run build && node e2e/serve.mjs',
      url: `http://localhost:${port}`,
      reuseExistingServer: !ci,
      timeout: 240_000,
      env: { E2E_PORT: String(port), API_URL: apiUrl },
    },
  ],
});
