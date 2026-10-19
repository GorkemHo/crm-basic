import { defineConfig } from '@playwright/test';

/**
 * End-to-end tests against a running stack: the API (https://localhost:7156) and `pnpm start` (:4200).
 * Locally the installed Chrome is used; in CI Playwright's own Chromium (`npx playwright install chromium`).
 *   E2E_EMAIL / E2E_PASSWORD: an admin account (defaults to the dev seed admin).
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? 'github' : 'list',
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4200',
    channel: process.env['CI'] ? undefined : 'chrome',
    locale: 'en-US',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
});
