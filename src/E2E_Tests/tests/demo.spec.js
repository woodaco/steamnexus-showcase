// @ts-check
const { test, expect } = require('@playwright/test');

/**
 * M6: read-only demo persona. Runs against E2E_BASE_URL (see playwright.config.js).
 * The demo is off by default (Demo:Enabled=false), so when the landing page has no
 * "Try the demo" button every test here skips with a message instead of failing.
 */
const BANNER = 'You are browsing as a demo persona, read only';

async function enterDemo(page) {
  await page.goto('/');
  const button = page.locator('#try-demo');
  if ((await button.count()) === 0) {
    test.skip(true, 'No "Try the demo" button on the landing page: Demo:Enabled is false on this server.');
  }
  await Promise.all([page.waitForURL(/\/Dashboard\/?$/i), button.click()]);
}

test.describe('M6: read-only demo persona', () => {
  test('the demo button lands on the Dashboard with the banner and 5 or more games', async ({ page }) => {
    await enterDemo(page);

    await expect(page.locator('#demo-banner')).toContainText(BANNER);
    const games = page.locator('#recentGamesCarousel .userCard');
    expect(await games.count(), 'seeded games on the Dashboard').toBeGreaterThanOrEqual(5);
    await expect(page.locator('a[href*="ShowMoreNews"]')).toHaveCount(0);
  });

  test('a POST from the demo session returns 403', async ({ page }) => {
    await enterDemo(page);
    const token = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();

    const res = await page.request.post('/Compete/Create', {
      form: { __RequestVerificationToken: token },
      maxRedirects: 0,
    });
    expect(res.status(), 'writes are refused for the demo persona').toBe(403);
  });

  test('/Admin is denied for the demo session', async ({ page }) => {
    await enterDemo(page);
    const res = await page.request.get('/Admin', { maxRedirects: 0 });
    expect(res.status()).toBe(403);
  });

  test('Exit demo signs the persona out', async ({ page }) => {
    await enterDemo(page);
    await Promise.all([page.waitForURL(/\/$/), page.locator('#demo-exit').click()]);
    await expect(page.locator('#demo-banner')).toHaveCount(0);
  });
});
