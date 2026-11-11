// @ts-check
// M1 "Fix the public surface" (docs/SHOWCASE_PLAN.md section 5). Everything here is anonymous,
// so it needs no seeded users and can run against the live site:
//   E2E_BASE_URL=https://<site> npx playwright test tests/public-surface.spec.js
const { test, expect } = require('@playwright/test');

test.describe('Public surface (anonymous visitor)', () => {
  test('landing page has meta description and og:image, and no stale footer', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('meta[name="description"]')).toHaveAttribute('content', /\S/);
    await expect(page.locator('meta[property="og:image"]')).toHaveAttribute('content', /\/assets\/og\.png$/);
    const html = await page.content();
    expect(html).not.toContain('support@steamnexus.com');
    expect(html).not.toContain('© 2024');
    await expect(page.locator('footer')).toContainText(`© ${new Date().getFullYear()}`);
  });

  test('robots.txt is reachable', async ({ request }) => {
    const res = await request.get('/robots.txt');
    expect(res.status()).toBe(200);
  });

  test('og image is reachable', async ({ request }) => {
    const res = await request.get('/assets/og.png');
    expect(res.status()).toBe(200);
  });

  test('unknown URL is a 404 page, not a login redirect', async ({ request }) => {
    const res = await request.get('/doesnotexist', { maxRedirects: 0 });
    expect(res.status()).toBe(404);
    expect(res.headers()['location']).toBeUndefined();
  });

  test('anonymous API call is 401, not a redirect', async ({ request }) => {
    const res = await request.get('/api/Steam/user', { maxRedirects: 0 });
    expect(res.status()).toBe(401);
  });

  test('at 375px the h1 is inside the viewport and clear of any sidebar', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/');
    const h1 = await page.locator('h1').first().boundingBox();
    expect(h1).not.toBeNull();
    if (!h1) return;
    expect(h1.x).toBeGreaterThanOrEqual(0);
    expect(h1.x + h1.width).toBeLessThanOrEqual(375);
    const sidebar = page.locator('.vert-nav');
    if (await sidebar.count() && await sidebar.isVisible()) {
      const s = await sidebar.boundingBox();
      if (s) {
        const intersects = !(s.x + s.width <= h1.x || h1.x + h1.width <= s.x || s.y + s.height <= h1.y || h1.y + h1.height <= s.y);
        expect(intersects).toBe(false);
      }
    }
  });

  test('no visible nav link sends an anonymous visitor to the login page', async ({ page, request }) => {
    await page.goto('/');
    const hrefs = await page.locator('header a[href]:visible, nav a[href]:visible').evaluateAll(
      (els) => els.map((e) => e.getAttribute('href')).filter((h) => h && h.startsWith('/')));
    for (const href of new Set(hrefs)) {
      const res = await request.get(href, { maxRedirects: 0 });
      const location = res.headers()['location'] || '';
      const toLogin = res.status() >= 300 && res.status() < 400 && /\/Identity\/Account\/Login/i.test(location);
      expect(toLogin, `${href} redirects to login`).toBe(false);
    }
  });

  for (const path of ['/', '/Home/Privacy']) {
    test(`every link on ${path} resolves below 400`, async ({ page, request }) => {
      await page.goto(path);
      const hrefs = await page.locator('a[href]').evaluateAll(
        (els) => els.map((e) => /** @type {HTMLAnchorElement} */ (e).href).filter((h) => /^https?:/.test(h)));
      for (const href of new Set(hrefs)) {
        const res = await request.get(href);
        expect(res.status(), href).toBeLessThan(400);
      }
    });
  }
});
