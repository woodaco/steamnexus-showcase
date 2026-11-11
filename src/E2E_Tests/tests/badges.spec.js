// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

test.describe('Badges: all five badges are seeded and awardable', () => {
  test('badge images and details resolve for ids 1..5 with distinct names', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    const names = new Set();
    for (let id = 1; id <= 5; id++) {
      const img = await page.request.get(`/api/Badge/GetBadgeImage/${id}`);
      expect(img.status(), `GetBadgeImage/${id} should be 200`).toBe(200);

      const details = await page.request.get(`/api/Badge/GetBadgeDetails/${id}`);
      expect(details.status(), `GetBadgeDetails/${id} should be 200`).toBe(200);
      const body = await details.json();
      expect(body.name, `badge ${id} should have a name`).toBeTruthy();
      names.add(body.name);
    }
    expect(names.size, 'all five badges should have distinct names').toBe(5);
  });

  test('CheckForNewBadges runs the award engine without error (eager-loaded, no N+1 crash)', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    const res = await page.request.get('/api/Badge/CheckForNewBadges');
    expect(res.status(), 'badge check should return 200').toBe(200);
    const body = await res.json();
    expect(Array.isArray(body), 'should return a list of awarded badge ids').toBeTruthy();
  });

  test('all five award conditions are earnable end-to-end (fixture qualifies DemoPlayer)', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    // The award engine awards every currently-qualifying, not-yet-held badge per call.
    await page.request.get('/api/Badge/CheckForNewBadges');
    await page.request.get('/api/Badge/CheckForNewBadges');

    await page.goto('/Dashboard', { waitUntil: 'domcontentloaded' });
    const earned = await page.locator('.badge-img').count();
    expect(earned, 'DemoPlayer should have earned all five badges').toBe(5);
  });
});
