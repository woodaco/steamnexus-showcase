// @ts-check
const { defineConfig, devices } = require('@playwright/test');

/**
 * Playwright config for SteamProject E2E tests.
 * The app is expected to already be running at BASE_URL (the autonomous harness
 * manages the dotnet process separately). Override with E2E_BASE_URL if needed.
 */
const BASE_URL = process.env.E2E_BASE_URL || 'http://localhost:5105';

module.exports = defineConfig({
  testDir: './tests',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  timeout: 45_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: BASE_URL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ignoreHTTPSErrors: true,
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});
