// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

test.describe('B3: VoteController resolves and records a vote', () => {
  test('a participant can cast a competition vote (200), and DI/identity work', async ({ page }) => {
    await login(page, CREDENTIALS.user);

    await page.goto('/Compete', { waitUntil: 'domcontentloaded' });
    await page.waitForLoadState('networkidle').catch(() => {});

    const token = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    expect(token, 'antiforgery token should be present').toBeTruthy();

    // Each competition card is <div class="competitionDiv" id="@competition.Id">.
    const compId = await page.evaluate(() => {
      const el = document.querySelector('.competitionDiv');
      const id = el ? parseInt(el.id, 10) : NaN;
      return Number.isInteger(id) ? id : null;
    });
    expect(compId, 'a seeded competition id should be discoverable on /Compete').not.toBeNull();

    const res = await page.request.post('/api/Vote/competitionvote', {
      headers: { RequestVerificationToken: token, 'Content-Type': 'application/json' },
      data: { competitionId: compId, wantsToPlayAgain: true },
      maxRedirects: 0,
    });
    // Previously: DI could not construct the controller (500) and int.Parse(GUID) threw.
    expect(res.status(), `vote should be accepted, got ${res.status()}`).toBe(200);
  });

  test('B5: re-match voting shows a live tally and a rematch CTA at majority (Details, DB-backed)', async ({ browser }) => {
    async function endedCompId(page) {
      return page.evaluate(() => {
        const section = [...document.querySelectorAll('.competitions-section')]
          .find((s) => /complete/i.test(s.querySelector('h1')?.textContent || ''));
        const el = section && section.querySelector('.competitionDiv');
        return el ? parseInt(el.id, 10) : null;
      });
    }
    async function vote(page, compId) {
      const token = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
      return page.request.post('/api/Vote/competitionvote', {
        headers: { RequestVerificationToken: token, 'Content-Type': 'application/json' },
        data: { competitionId: compId, wantsToPlayAgain: true },
      });
    }

    // DemoPlayer finds an ended competition (2 seeded participants: DemoPlayer + TestUser2).
    const ctxA = await browser.newContext({ ignoreHTTPSErrors: true });
    const pageA = await ctxA.newPage();
    await login(pageA, CREDENTIALS.user);
    await pageA.goto('/Compete', { waitUntil: 'domcontentloaded' });
    const compId = await endedCompId(pageA);
    expect(compId, 'a seeded ended competition should exist').toBeTruthy();

    // The Details voting section shows a tally bounded by the participant count, and the dead
    // game-nomination UI is gone.
    await pageA.goto(`/Compete/Details/${compId}`);
    await expect(pageA.locator('#rematchTally')).toContainText('of 2');
    await expect(pageA.locator('#nominateGameBtn')).toHaveCount(0);
    await expect(pageA.locator('.voteForGameBtn')).toHaveCount(0);

    // Both participants vote to play again -> majority (votes are upserted, so this is re-run safe).
    const a = await vote(pageA, compId);
    expect(a.status(), 'participant vote should be accepted').toBe(200);

    const ctxB = await browser.newContext({ ignoreHTTPSErrors: true });
    const pageB = await ctxB.newPage();
    await login(pageB, CREDENTIALS.user2);
    await pageB.goto('/Compete', { waitUntil: 'domcontentloaded' });
    expect((await vote(pageB, compId)).status()).toBe(200);

    // DemoPlayer reloads -> "you voted" state, 2-of-2 tally, and the rematch CTA to start a new competition.
    await pageA.goto(`/Compete/Details/${compId}`);
    await expect(pageA.locator('#userVotedMsg')).toBeVisible();
    await expect(pageA.locator('#rematchTally')).toContainText('2 of 2');
    await expect(pageA.locator('#startRematchBtn')).toBeVisible();

    await ctxA.close();
    await ctxB.close();
  });
});
