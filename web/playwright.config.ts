import { defineConfig, devices } from '@playwright/test';

const ci = !!process.env['CI'];

// End-to-end tests (ADR 0032): the main flows in a real browser against the real API and database. Playwright starts
// what isn't running yet: the API with the http profile — the one the dev server's proxy points at — and the app in its
// source language (English) on a port of its own, so it doesn't meet `npm start`'s translated one on 4200. Locally it
// uses the API and database already running (the database from `docker compose up -d`, the connection string and the
// JWT signing key from user-secrets); CI starts the API itself on a fresh database, with both in the environment.
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: ci,
  // A flaky test is fixed, not retried: a retry would hide a race a user can meet too.
  retries: 0,
  workers: ci ? 2 : undefined,
  reporter: ci ? [['github'], ['html', { open: 'never' }]] : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'http://localhost:4300',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'dotnet run --project ../src/AppTemplate.Api --launch-profile http',
      // Ready once the database answers and every migration is applied (ADR 0019).
      url: 'http://localhost:5062/health/ready',
      // Reused when running: CI starts it first (ci-e2e.yml), to check the generated client against it, and keeps its
      // log; locally a running API is the developer's own.
      reuseExistingServer: true,
      timeout: 240_000,
    },
    {
      command: 'npx ng serve --configuration en --port 4300',
      url: 'http://localhost:4300',
      reuseExistingServer: !ci,
      timeout: 240_000,
    },
  ],
});
