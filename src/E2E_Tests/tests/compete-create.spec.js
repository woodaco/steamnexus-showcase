// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

test.describe('B4: compete create page initializes (SinId/typo crash fixed)', () => {
  test('opening /Compete/Create runs its init without a null/typo JS crash', async ({ page }) => {
    const errors = [];
    page.on('pageerror', (e) => errors.push(e.message));
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });

    await login(page, CREDENTIALS.user);
    await page.goto('/Compete/Create', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(3500); // allow init + the friends AJAX

    // Previously getElementById('SinId').value threw immediately (element is 'CurrentUserId'),
    // and document.GetElementById was a typo. Neither should crash the page now.
    const crashes = errors.filter((e) =>
      /Cannot read propert.*null|reading 'value'|SinId|GetElementById is not a function/i.test(e));
    expect(crashes, `create-page init crashed: ${crashes.join(' | ')}`).toHaveLength(0);

    await expect(page.locator('#categorySelect')).toBeVisible();
  });

  test('D3: the vaporware Speed Run affordance is removed from the UI', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    await page.goto('/Compete');
    await expect(page.locator('#speedRunCreateLink')).toHaveCount(0);
    await expect(page.getByText('Speed Run Challenge')).toHaveCount(0);
  });

  test('B5: the Compete catch-all (Initiate) route no longer 500s on bad input', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    const res = await page.request.get('/Compete/not-a-real-steamid', { maxRedirects: 0 });
    expect(res.status(), `bad Initiate input should not 500, got ${res.status()}`).not.toBe(500);
  });

  test('Security: Compete/Create POST without an antiforgery token is rejected (CSRF)', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    const res = await page.request.post('/Compete/Create', {
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      data: 'SteamId=x&GameAppId=1',
      maxRedirects: 0,
    });
    expect(res.status(), `tokenless POST should be 400, got ${res.status()}`).toBe(400);
  });

  test('B4: the Free-For-All create path is DB-backed (friends + own games load off fixtures)', async ({ page }) => {
    await login(page, CREDENTIALS.user); // DemoPlayer

    // /Compete/MyGames returns the user's own synced library.
    const myGamesRes = await page.request.get('/Compete/MyGames');
    expect(myGamesRes.status()).toBe(200);
    const myGames = JSON.parse(await myGamesRes.text());
    expect(myGames.length, 'DemoPlayer owns seeded games').toBeGreaterThan(0);

    await page.goto('/Compete/Create');
    await page.locator('#categorySelect').selectOption('Free-For-All');

    // Friends multi-select loads from /Compete/FriendsList (SumoSelect keeps the native select in the DOM).
    const groupSelect = page.locator('#groupSelector');
    await groupSelect.waitFor({ state: 'attached', timeout: 15000 });
    await expect(groupSelect.locator('option')).not.toHaveCount(0);

    // Game selector loads from /Compete/MyGames.
    const gameSelect = page.locator('#gameSelector');
    await gameSelect.waitFor({ state: 'attached', timeout: 15000 });
    await expect(gameSelect.locator('option')).not.toHaveCount(0);
  });

  test('D2: Compete index shows graceful empty states when the user has no competitions', async ({ page }) => {
    // The admin account participates in no competitions, so both sections fall to their empty state.
    await login(page, CREDENTIALS.admin);
    await page.goto('/Compete', { waitUntil: 'domcontentloaded' });

    await expect(page.locator('.competitionDiv')).toHaveCount(0);
    await expect(page.locator('.competitions-empty')).toHaveCount(2);
    await expect(page.locator('body')).toContainText('No active competitions');
    await expect(page.locator('body')).toContainText('No completed competitions');
  });

  test('B4: a duel competition is created end-to-end through the Create UI (DB-backed off fixtures)', async ({ page }) => {
    await login(page, CREDENTIALS.user); // DemoPlayer

    // How many competitions DemoPlayer already participates in.
    await page.goto('/Compete');
    const before = await page.locator('.competitionDiv').count();

    await page.goto('/Compete/Create');

    // Duel is the default category. Friends load from the DB (/Compete/FriendsList), not live Steam.
    const friendSelect = page.locator('#friendSelector');
    await friendSelect.waitFor({ state: 'visible', timeout: 15000 });
    // Pick the friend who shares a synced game with DemoPlayer (TestUser2 owns games[0] via fixtures).
    await friendSelect.selectOption({ label: 'TestUser2' });

    // Selecting the friend loads the shared games (/Compete/SharedGames) -> game selector appears.
    const gameSelect = page.locator('#gameSelector');
    await gameSelect.waitFor({ state: 'visible', timeout: 15000 });
    await expect(gameSelect.locator('option')).not.toHaveCount(0);

    // The game's achievements load (/Compete/GameAchievements) -> hidden inputs + the submit button.
    const submit = page.locator('#compCreateSubmit');
    await submit.waitFor({ state: 'visible', timeout: 15000 });
    expect(await page.locator('input[name="AchievementDisplayNames"]').count(),
      'achievements should load to compete over').toBeGreaterThan(0);

    // Give it a clear future window, then create.
    await page.fill('#CompEndTime', '2026-12-31T10:00');
    await submit.click();

    // Redirects back to /Compete with exactly one more competition card for DemoPlayer.
    await page.waitForURL(/\/Compete\/?$/, { timeout: 15000 });
    await expect(page.locator('.competitionDiv')).toHaveCount(before + 1);
  });
});
