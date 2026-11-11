// @ts-check
const { test, expect } = require('@playwright/test');

test.describe('B11: registration creates the account atomically', () => {
  test('a new user can register and is taken to the confirmation step', async ({ page }) => {
    const email = `reg_${Date.now()}@example.com`;
    await page.goto('/Identity/Account/Register');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill('Passw0rd!Test123');
    await page.getByLabel('Confirm Password').fill('Passw0rd!Test123');
    await page.getByRole('button', { name: /register/i }).click();

    // RequireConfirmedAccount=true -> redirect to RegisterConfirmation (account + domain row created).
    await page.waitForURL(/RegisterConfirmation/i, { timeout: 15000 });
    await expect(page).toHaveURL(/RegisterConfirmation/i);
  });

  test('D5: logging in before confirming shows the unconfirmed-email message, not a generic error', async ({ page }) => {
    const email = `unconf_${Date.now()}@example.com`;
    const pw = 'Passw0rd!Test123';
    await page.goto('/Identity/Account/Register');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill(pw);
    await page.getByLabel('Confirm Password').fill(pw);
    await page.getByRole('button', { name: /register/i }).click();
    await page.waitForURL(/RegisterConfirmation/i, { timeout: 15000 });

    // Now try to log in before confirming -> should get the specific unconfirmed message.
    await page.goto('/Identity/Account/Login');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill(pw);
    await page.getByRole('button', { name: /log in/i }).click();

    await expect(page.getByText(/hasn't been confirmed/i)).toBeVisible();
  });
});
