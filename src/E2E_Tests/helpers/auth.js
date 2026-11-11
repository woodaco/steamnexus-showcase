// @ts-check
const fs = require('fs');
const os = require('os');
const path = require('path');

const SECRETS_PATH = path.join(
  os.homedir(),
  '.microsoft/usersecrets/aspnet-SteamProject-8cc9c827-d5e8-48d0-969a-97836fd8127b/secrets.json'
);

/**
 * Reads user-secrets at runtime so credentials never appear in source or logs.
 * Falls back to env vars (E2E_SEED_PW / E2E_ADMIN_PW) for CI.
 */
function readSecrets() {
  try {
    const raw = fs.readFileSync(SECRETS_PATH, 'utf8').replace(/^﻿/, '');
    return JSON.parse(raw);
  } catch {
    return {};
  }
}

const secrets = readSecrets();

const CREDENTIALS = {
  user: {
    email: 'demo.player@example.com',
    password: process.env.E2E_SEED_PW || secrets.SeedUserPW,
    steamId: '76561190000000101',
  },
  user2: {
    email: 'TestUser2@mail.com',
    password: process.env.E2E_SEED_PW || secrets.SeedUserPW,
  },
  admin: {
    email: 'admin@example.com',
    password: process.env.E2E_ADMIN_PW || secrets.SeedAdminPW,
  },
};

/**
 * Logs in through the Identity UI. Returns once authenticated.
 * @param {import('@playwright/test').Page} page
 * @param {{email:string,password:string}} creds
 */
async function login(page, creds = CREDENTIALS.user) {
  if (!creds.password) {
    throw new Error('No seed password available (user-secrets not found and E2E_SEED_PW unset).');
  }
  await page.goto('/Identity/Account/Login');
  await page.getByLabel('Email').fill(creds.email);
  await page.getByLabel('Password').fill(creds.password);
  await page.getByRole('button', { name: /log in/i }).click();
  // Identity redirects away from the login page on success.
  await page.waitForURL((url) => !url.pathname.toLowerCase().includes('/account/login'), {
    timeout: 15_000,
  });
}

module.exports = { CREDENTIALS, login, readSecrets };
