import { Page, test as base, expect } from '@playwright/test';
import type { RegisterRequest, TokenPairResponse } from '../src/app/core/api/api-client';
import { REFRESH_TOKEN_KEY } from '../src/app/core/auth/storage-keys';

export { expect };

// Someone new for each test, with a password made up on the spot: it is kept only in the run's report and traces (a
// fill step shows its value), for an account in a test database. Tests share nothing, so they run side by side without
// cleaning the database up.
export function newUser(): RegisterRequest & { email: string; password: string; displayName: string } {
  return { email: `e2e-${crypto.randomUUID()}@example.test`, password: crypto.randomUUID(), displayName: 'Ana' };
}

interface Fixtures {
  // The user every test starts signed in as: registered through the API, its refresh token put where the app keeps
  // it, so the app's start restores the session as after any visit.
  user: ReturnType<typeof newUser>;
}

// Automatic: every `test` starts signed in, whether or not it reads `user`; a flow for a visitor uses Playwright's own
// `test` instead.
export const test = base.extend<Fixtures>({
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
  await page.goto('/login');
  await page.evaluate(([key, token]) => localStorage.setItem(key, token), [REFRESH_TOKEN_KEY, refreshToken]);
}
