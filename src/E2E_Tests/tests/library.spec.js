// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

test.describe('B10: library hide reflects the real backend result and persists', () => {
  test('hiding a game removes it from the grid only on success and persists across reload', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    await page.goto('/Library', { waitUntil: 'domcontentloaded' });
    await page.waitForLoadState('networkidle').catch(() => {});

    const hideButtons = page.locator('.hide-btn');
    const before = await hideButtons.count();
    test.skip(before < 1, 'seeded user has no visible games to hide');

    // The card (.gameContainer) carries the Game.Id in its name attribute (used to restore later).
    const firstCard = page.locator('.gameContainer:has(.hide-btn)').first();
    const gameId = await firstCard.getAttribute('name');

    await hideButtons.first().click();
    // The success handler must only hide the card after a 2xx response.
    await expect(firstCard).toBeHidden({ timeout: 5000 });

    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForLoadState('networkidle').catch(() => {});
    const after = await page.locator('.hide-btn').count();
    expect(after, 'a hidden game should be gone from the grid after reload (persisted)').toBe(before - 1);

    // Restore state so the test is idempotent across runs.
    if (gameId) {
      const token = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
      await page.request.post(`/api/Steam/unhide?id=${gameId}`, {
        headers: { RequestVerificationToken: token },
      });
    }
  });
});
