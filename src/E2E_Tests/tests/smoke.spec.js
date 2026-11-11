// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

test.describe('Smoke: app boots and serves fixtures', () => {
  test('anonymous visit to a protected page redirects to login', async ({ page }) => {
    // `/` is the public landing page; /Compete requires authentication.
    await page.goto('/Compete');
    await expect(page).toHaveURL(/Account\/Login/i);
    await expect(page.getByRole('heading', { name: /please log in/i })).toBeVisible();
  });

  test('login page renders with the expected form', async ({ page }) => {
    await page.goto('/Identity/Account/Login');
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByLabel('Password')).toBeVisible();
    await expect(page.getByRole('button', { name: /log in/i })).toBeVisible();
    // B12: Steam external sign-in is reachable from the canonical login page.
    await expect(page.getByRole('button', { name: /sign in with steam/i })).toBeVisible();
  });

  test('seeded user can log in and reach an authenticated page', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    // We should no longer be on the login page, and the auth nav should be present.
    await expect(page).not.toHaveURL(/Account\/Login/i);
    await expect(page.getByText(/please log in/i)).toHaveCount(0);
  });
});
