// @ts-check
// M2 "Landing page as a portfolio piece" (docs/SHOWCASE_PLAN.md section 2). Anonymous, so it can
// run against the live site:
//   E2E_BASE_URL=https://<site> npx playwright test tests/landing-portfolio.spec.js
const { test, expect } = require('@playwright/test');

test.describe('Landing page as a portfolio piece', () => {
  test('says it is a four-person capstone, later refactored solo by Cole Woodard', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('h1')).toHaveCount(1);
    const hero = page.locator('.land-hero');
    await expect(hero).toContainText('capstone');
    await expect(hero).toContainText('four-person team');
    await expect(hero).toContainText('refactored solo by Cole Woodard');
    await expect(page.locator('#my-role')).toContainText('draft, to be confirmed');
    await expect(page.locator('#built-with li')).not.toHaveCount(0);
  });

  test('has no carousel and no testimonial', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('.carousel')).toHaveCount(0);
    await expect(page.locator('.testimonials')).toHaveCount(0);
  });

  test('#screenshots has 3+ loaded images, each with alt text', async ({ page }) => {
    await page.goto('/');
    const imgs = page.locator('#screenshots img');
    expect(await imgs.count()).toBeGreaterThanOrEqual(3);
    for (const img of await imgs.all()) {
      await img.scrollIntoViewIfNeeded();
      await expect(img).toHaveAttribute('alt', /\S{3,}/);
      await expect.poll(() => img.evaluate((el) => el.complete && el.naturalWidth)).toBeGreaterThan(0);
    }
  });
});
