// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

test.describe('B6: friend block/unblock management', () => {
  test('blocking a friend removes them from the list and persists; unblock restores', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    await page.goto('/Home/Friends', { waitUntil: 'domcontentloaded' });

    const cards = page.locator('.friend-card');
    const before = await cards.count();
    test.skip(before < 1, 'seeded user has no friends to block');

    const friendId = await page.locator('.block-friend').first().getAttribute('data-friend-id');
    const token = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();

    const block = await page.request.post(`/api/Steam/blockFriend?friendId=${friendId}`, {
      headers: { RequestVerificationToken: token },
    });
    expect(block.status(), 'block should succeed').toBe(200);

    await page.reload({ waitUntil: 'domcontentloaded' });
    const after = await page.locator('.friend-card').count();
    expect(after, 'blocked friend should be gone from the list').toBe(before - 1);

    // Restore state.
    const unblock = await page.request.post(`/api/Steam/unblockFriend?friendId=${friendId}`, {
      headers: { RequestVerificationToken: token },
    });
    expect(unblock.status(), 'unblock should succeed').toBe(200);
    await page.reload({ waitUntil: 'domcontentloaded' });
    expect(await page.locator('.friend-card').count(), 'unblock restores the friend').toBe(before);
  });

  test('B6: a blocked friend appears in the Blocked section with a working unblock button (UI)', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    await page.goto('/Home/Friends', { waitUntil: 'domcontentloaded' });
    const activeBefore = await page.locator('.friend-card:not(.blocked-card)').count();
    test.skip(activeBefore < 1, 'seeded user has no friends to block');

    const friendId = await page.locator('.block-friend').first().getAttribute('data-friend-id');
    const token = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    await page.request.post(`/api/Steam/blockFriend?friendId=${friendId}`, { headers: { RequestVerificationToken: token } });

    // After reload the friend shows in the Blocked section with an unblock button (previously: no UI at all).
    await page.reload({ waitUntil: 'domcontentloaded' });
    const unblockBtn = page.locator(`.unblock-friend[data-friend-id="${friendId}"]`);
    await expect(unblockBtn, 'blocked friend should be unblockable from the UI').toBeVisible();

    // Clicking the UI button unblocks + restores the friend to the active list.
    await unblockBtn.click();
    await page.waitForLoadState('domcontentloaded');
    await expect(page.locator(`.unblock-friend[data-friend-id="${friendId}"]`)).toHaveCount(0);
    expect(await page.locator('.friend-card:not(.blocked-card)').count()).toBe(activeBefore);
  });

  test('another user cannot block a friend they do not own (IDOR)', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    await page.goto('/Home/Friends');
    const friendId = await page.locator('.block-friend').first().getAttribute('data-friend-id');
    const token = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    // friendId 999999 is not owned by anyone -> 404, never a silent success.
    const res = await page.request.post(`/api/Steam/blockFriend?friendId=999999`, {
      headers: { RequestVerificationToken: token },
    });
    expect([404], `non-owned friend block must 404, got ${res.status()}`).toContain(res.status());
  });
});
