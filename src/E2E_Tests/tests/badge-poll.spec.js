// @ts-check
const { test, expect } = require('@playwright/test');

test.describe('C2: badge polling is gated to authenticated users', () => {
  test('an anonymous page does not poll /api/Badge/CheckForNewBadges', async ({ page }) => {
    const calls = [];
    page.on('request', (r) => {
      if (r.url().includes('CheckForNewBadges')) calls.push(r.url());
    });
    await page.goto('/'); // public landing, not authenticated
    // Wait longer than the previous 6s interval to prove the poller never starts.
    await page.waitForTimeout(7500);
    expect(calls.length, 'anonymous pages must not poll the badge endpoint').toBe(0);
  });
});
