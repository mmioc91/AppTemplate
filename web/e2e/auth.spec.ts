import { test as visitor } from '@playwright/test';
import { expect, newUser, test } from './fixtures';

// Signing in end to end (ADR 0031, 0032): the tokens through the dev server's proxy, the guards, the return URL, the
// session restored from the stored refresh token, and the API's refusal told by its code.

visitor('registers and lands home, signed in', async ({ page }) => {
  // Arrange
  const user = newUser();
  await page.goto('/register');

  // Act
  await page.getByLabel('E-mail address').fill(user.email);
  await page.getByLabel('Password', { exact: true }).fill(user.password);
  await page.getByLabel('Name').fill(user.displayName);
  await page.getByRole('button', { name: 'Register' }).click();

  // Assert
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page.getByText(`Signed in as ${user.displayName}.`)).toBeVisible();
});

test('stays signed in across a reload', async ({ page }) => {
  // Arrange
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();

  // Act
  await page.reload();

  // Assert
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page).toHaveURL('/');
});

test('signs out, and back in to the page that was asked for', async ({ page, user }) => {
  // Arrange: signed out, then asking for the home page.
  await page.goto('/');
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL('/login');
  await page.goto('/');
  await expect(page).toHaveURL('/login?returnUrl=%2F');

  // Act
  await page.getByLabel('E-mail address').fill(user.email);
  await page.getByLabel('Password', { exact: true }).fill(user.password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  // Assert
  await expect(page.getByRole('heading', { name: 'Home' })).toBeVisible();
  await expect(page).toHaveURL('/');
});

test('tells a wrong password, and stays signed out', async ({ page, user }) => {
  // Arrange
  await page.goto('/');
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL('/login');

  // Act
  await page.getByLabel('E-mail address').fill(user.email);
  await page.getByLabel('Password', { exact: true }).fill(`${user.password}-not`);
  await page.getByRole('button', { name: 'Sign in' }).click();

  // Assert
  await expect(page.getByRole('alert')).toHaveText('Wrong e-mail address or password.');
  await expect(page).toHaveURL('/login');
});
