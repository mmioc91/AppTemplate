import { BrowserContext, Page, test as base, expect } from '@playwright/test';
import type { RegisterRequest, TokenPairResponse } from '../src/app/core/api/api-client';
import { REFRESH_TOKEN_KEY } from '../src/app/core/auth/storage-keys';

export { expect };

// Someone new for each test, with a password made up on the spot: it is kept only in the run's report and traces (a
// fill step shows its value), for an account in a test database. Tests share nothing, so they run side by side without
// cleaning the database up.
export function newUser(): RegisterRequest & { email: string; password: string; displayName: string } {
  return { email: `e2e-${crypto.randomUUID()}@example.test`, password: crypto.randomUUID(), displayName: 'Ana' };
}

// The app in its source language: the tests read the English texts (ADR 0032).
export const app = (path = ''): string => `/en/${path}`;

// Every page of a test runs under the host's Content-Security-Policy (e2e/serve.mjs); anything it blocks fails the
// test, so the app never ships something the policy would break (ADR 0033).
async function watchPolicy(context: BrowserContext): Promise<string[]> {
  const violations: string[] = [];
  await context.exposeBinding('reportCspViolation', (_source, violation: string) => violations.push(violation));
  await context.addInitScript(() =>
    document.addEventListener('securitypolicyviolation', (event) =>
      (window as unknown as { reportCspViolation(violation: string): void }).reportCspViolation(
        `${event.effectiveDirective} ${event.blockedURI}`,
      ),
    ),
  );
  return violations;
}

interface Fixtures {
  contentSecurityPolicy: void;
  // The user every test starts signed in as: registered through the API, its refresh token put where the app keeps
  // it, so the app's start restores the session as after any visit.
  user: ReturnType<typeof newUser>;
}

// A visitor: no account, the policy watched.
export const visitor = base.extend<Pick<Fixtures, 'contentSecurityPolicy'>>({
  contentSecurityPolicy: [
    async ({ context }, use) => {
      const violations = await watchPolicy(context);
      await use();
      expect(violations, 'Content-Security-Policy violations').toEqual([]);
    },
    { auto: true },
  ],
});

// Automatic: every `test` starts signed in, whether or not it reads `user`; a visitor's flow uses `visitor`.
export const test = visitor.extend<Pick<Fixtures, 'user'>>({
  user: [
    async ({ page }, use) => {
      const user = newUser();
      const response = await page.request.post('/api/v1/auth/register', { data: user });
      expect(response.status()).toBe(201);
      const tokens = (await response.json()) as TokenPairResponse;
      await signInBrowser(page, tokens.refreshToken);
      await use(user);
    },
    { auto: true },
  ],
});

// The app's origin must be open to reach its storage; the sign-in page needs no session.
async function signInBrowser(page: Page, refreshToken: string): Promise<void> {
  await page.goto(app('login'));
  await page.evaluate(([key, token]) => localStorage.setItem(key, token), [REFRESH_TOKEN_KEY, refreshToken]);
}
