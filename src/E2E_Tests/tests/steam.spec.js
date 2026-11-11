// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

test.describe('C4: Steam profile lookup is null-safe', () => {
  test('looking up an invalid/private steamid returns 200 (blank user), not a 500', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    // steamid "1" yields an empty players array from Steam; must not NRE.
    const res = await page.request.get('/api/Steam/user?steamid=1', { maxRedirects: 0 });
    expect(res.status(), `expected 200, got ${res.status()}`).toBe(200);
  });
});
