// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

test.describe('C5: admin ban is no longer a destructive GET', () => {
  // demo.player@example.com is promoted to the admin role via AdminEmails in Development.
  test('admin can load the dashboard; GET to the ban action is rejected (no mutation)', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    const dash = await page.goto('/Admin');
    expect(dash?.status(), 'admin should reach /Admin').toBe(200);

    // The ban action used to be a GET <a> link. A GET must now be rejected (405/404),
    // proving it cannot be triggered by a crafted link or <img src>.
    const res = await page.request.get('/Admin/Delete?id=76561190000000101', { maxRedirects: 0 });
    expect([404, 405], `GET to ban must be rejected, got ${res.status()}`).toContain(res.status());
  });

  test('admin dashboard shows live metrics matching the seeded data', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    await page.goto('/Admin');
    const users = parseInt((await page.locator('#metric-users').textContent())?.trim() || '0', 10);
    const games = parseInt((await page.locator('#metric-games').textContent())?.trim() || '0', 10);
    expect(users, 'user count should be > 0').toBeGreaterThan(0);
    expect(games, 'game count should be > 0 (Results.csv seeds games)').toBeGreaterThan(0);
  });

  test('C5: destructive admin bulk actions reject GET (POST-only now)', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    for (const path of ['/Admin/LoadGames', '/Admin/LoadGameInfo']) {
      const res = await page.request.get(path, { maxRedirects: 0 });
      expect([404, 405], `${path} GET must be rejected, got ${res.status()}`).toContain(res.status());
    }
  });

  // Admin Unban round-trip (the audit found Unban did not exist; a banned id was permanently locked out).
  // Assumes a fresh app/fixture state (SeedDomainFixtures seeds the demo banned id on startup).
  test('admin can unban a blacklisted SteamId (POST + antiforgery round-trip)', async ({ page }) => {
    const BANNED = '76561190000000666'; // dedicated demo fixture id, used by no real user
    await login(page, CREDENTIALS.user);
    await page.goto('/Admin/ViewBannedIds');
    await expect(page.locator('.admin-table'), 'seeded banned id should be listed').toContainText(BANNED);

    // GET to Unban must be rejected — it is a POST-only, antiforgery-protected action.
    const getRes = await page.request.get(`/Admin/Unban?id=${BANNED}`, { maxRedirects: 0 });
    expect([404, 405], `GET to Unban must be rejected, got ${getRes.status()}`).toContain(getRes.status());

    // Submit the real per-row unban form (carries the antiforgery token); accept the confirm() dialog.
    page.once('dialog', (d) => d.accept());
    const row = page.locator('tr', { hasText: BANNED });
    await row.locator('button[type="submit"]').click();

    // After the redirect: success banner shown and no unban form remains for that id.
    await expect(page.locator('.alert-success')).toBeVisible();
    await expect(page.locator(`form.unban-form input[value="${BANNED}"]`)).toHaveCount(0);
  });
});
