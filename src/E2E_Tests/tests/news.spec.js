// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

test.describe('C1/C3: news path is async and does not 500', () => {
  test('ShowMoreNews renders for a seeded owned game', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    await page.goto('/Library', { waitUntil: 'domcontentloaded' });
    await page.waitForLoadState('networkidle').catch(() => {});

    // Game cards carry the Steam AppId in their value attribute.
    const appId = await page.evaluate(() => {
      const el = document.querySelector('.gameContainer[value]');
      return el ? el.getAttribute('value') : null;
    });
    test.skip(!appId, 'no seeded game AppId found on the library page');

    const res = await page.goto(`/Home/ShowMoreNews?appId=${appId}`);
    expect(res?.status(), 'ShowMoreNews should render, not 500').toBe(200);
    await expect(page.locator('body')).not.toContainText(/An unhandled exception|Stack trace/i);
  });

  test('C3: the paid OpenAI news endpoint is per-user rate limited (429 under burst)', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    await page.goto('/');

    // Use a non-existent appId so each request short-circuits BEFORE any OpenAI call (no cost): the
    // rate limiter runs ahead of the action, so a burst still trips the per-user limit.
    const statuses = [];
    for (let i = 0; i < 25; i++) {
      const r = await page.request.get('/api/News/GetRecentGameNews?appId=999999999', { maxRedirects: 0 });
      statuses.push(r.status());
    }
    const limited = statuses.filter((s) => s === 429).length;
    expect(limited, `a 25-request burst should hit the rate limit; statuses=${statuses.join(',')}`).toBeGreaterThan(0);
  });
});
