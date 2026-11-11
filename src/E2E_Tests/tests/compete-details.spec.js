// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

test.describe('D2: competition Details renders cleanly (markup + JS hygiene)', () => {
  test('Details page loads with no JS crash and unique element ids', async ({ page }) => {
    const errors = [];
    page.on('pageerror', (e) => errors.push(e.message));

    await login(page, CREDENTIALS.user);
    await page.goto('/Compete', { waitUntil: 'domcontentloaded' });
    const compId = await page.evaluate(() => {
      const el = document.querySelector('.competitionDiv');
      return el ? el.id : null;
    });
    test.skip(!compId, 'no seeded competition found');

    await page.goto(`/compete/Details/${compId}`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1500);

    // competeDetails.js used to TypeError on the missing delete button for non-creators and had a
    // broken vote handler; the page must initialize without throwing.
    expect(errors, `Details JS crashed: ${errors.join(' | ')}`).toHaveLength(0);

    // Duplicate id #CompletionHistory was invalid HTML; there must be at most one now.
    const dupCount = await page.locator('#CompletionHistory').count();
    expect(dupCount, 'CompletionHistory id must be unique').toBeLessThanOrEqual(1);
  });
});
