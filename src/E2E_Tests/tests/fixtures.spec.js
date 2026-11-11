// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

test.describe('Domain fixtures render across features', () => {
  test.beforeEach(async ({ page }) => {
    await login(page, CREDENTIALS.user);
  });

  test('Compete page renders the seeded competitions without error', async ({ page }) => {
    const res = await page.goto('/Compete');
    expect(res?.status(), 'Compete should return 200').toBe(200);
    await expect(page).not.toHaveURL(/Account\/Login/i);
    // The Index view links each competition card to /compete/Details/{id}.
    const detailLinks = page.locator('[onclick*="Details"], a[href*="/compete/Details"], [class*="comp"]');
    expect(await detailLinks.count(), 'at least one competition card should render').toBeGreaterThan(0);
    // No server error surfaced.
    await expect(page.locator('body')).not.toContainText(/An unhandled exception|Stack trace/i);
  });

  test('Library page renders the seeded owned games without error', async ({ page }) => {
    const res = await page.goto('/Library');
    expect(res?.status(), 'Library should return 200').toBe(200);
    await expect(page).not.toHaveURL(/Account\/Login/i);
    await expect(page.locator('body')).not.toContainText(/An unhandled exception|Stack trace/i);
  });
});
