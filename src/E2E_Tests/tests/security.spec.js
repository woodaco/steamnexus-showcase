// @ts-check
const { test, expect, request } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

const BASE = process.env.E2E_BASE_URL || 'http://localhost:5105';

/**
 * Phase A — security foundation. These assert the systemic auth-bypass is closed:
 * the API controllers (Steam/User/Messages) must NOT serve data to anonymous callers.
 * A request that is redirected to the login page (302/200 login HTML) or returns 401
 * is acceptable; a 200 with real data is a failure.
 */
test.describe('Security: anonymous access is denied on protected endpoints', () => {
  /** @type {import('@playwright/test').APIRequestContext} */
  let anon;
  test.beforeAll(async () => {
    anon = await request.newContext({ baseURL: BASE, ignoreHTTPSErrors: true, maxRedirects: 0 });
  });
  test.afterAll(async () => { await anon.dispose(); });

  const protectedEndpoints = [
    { method: 'get', path: '/api/Steam/user?steamid=76561190000000111' },
    { method: 'get', path: '/api/Steam/games?userSteamId=76561190000000111&userId=1' },
    { method: 'get', path: '/api/Steam/friends?steamid=76561190000000111&userId=1' },
    { method: 'get', path: '/api/Steam/sendInvite?email=abuse%40example.com' },
    { method: 'get', path: '/api/Messages/messagesBetween?fromId=1&toId=2' },
    { method: 'post', path: '/api/Steam/DeleteComp?compId=1' },
    { method: 'get', path: '/User' },
  ];

  for (const ep of protectedEndpoints) {
    test(`anonymous ${ep.method.toUpperCase()} ${ep.path} is not served (no 200 data)`, async () => {
      const res = ep.method === 'post' ? await anon.post(ep.path) : await anon.get(ep.path);
      // Acceptable: 401, or 302 redirect to login. Unacceptable: 200 (served) or 500 (reached code).
      const status = res.status();
      expect(status, `expected auth challenge, got ${status}`).not.toBe(200);
      expect(status, `endpoint threw instead of denying (got ${status})`).not.toBe(500);
      if (status === 302) {
        expect(res.headers()['location'] || '').toMatch(/Account\/Login/i);
      }
    });
  }

  test('authenticated send to a nonexistent recipient returns 404, not a 500 stack trace', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    await page.goto('/');
    const token = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const res = await page.request.post('/api/Messages/sendMessageTo', {
      headers: { RequestVerificationToken: token, 'Content-Type': 'application/json' },
      data: { toId: 999999, message: 'hi' },
      maxRedirects: 0,
    });
    expect(res.status(), `expected 404 for unknown recipient, got ${res.status()}`).toBe(404);
    const body = await res.text();
    expect(body).not.toMatch(/SqlException|StackTrace|InboxMessage_Fk_User|at SteamProject/i);
  });

  test('state-changing API POST requires an antiforgery token', async ({ page }) => {
    await login(page, CREDENTIALS.user);
    await page.goto('/');
    const token = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    expect(token, 'antiforgery token should be rendered in the layout').toBeTruthy();

    // Without the token header -> rejected (400).
    const noToken = await page.request.post('/api/Steam/UpdateTheme?theme=dark', { maxRedirects: 0 });
    expect(noToken.status(), `expected 400 without token, got ${noToken.status()}`).toBe(400);

    // With the token header -> accepted (200).
    const withToken = await page.request.post('/api/Steam/UpdateTheme?theme=dark', {
      headers: { RequestVerificationToken: token },
      maxRedirects: 0,
    });
    expect(withToken.status(), `expected 200 with token, got ${withToken.status()}`).toBe(200);
  });

  test('landing page is still public (200)', async () => {
    const res = await anon.get('/');
    expect(res.status()).toBe(200);
  });

  test('health check is public (200)', async () => {
    const res = await anon.get('/health');
    expect(res.status()).toBe(200);
  });
});
