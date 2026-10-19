import { Page, expect } from '@playwright/test';

export const ADMIN_EMAIL = process.env['E2E_EMAIL'] ?? 'admin@crm.local';
export const ADMIN_PASSWORD = process.env['E2E_PASSWORD'] ?? 'dev12345';

export const unique = (prefix: string) => `${prefix} ${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;

/** Signs in through the login form and waits for the app shell. */
export async function login(page: Page, email = ADMIN_EMAIL, password = ADMIN_PASSWORD): Promise<void> {
  await page.goto('/login');
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);
  await page.locator('form button[type="submit"]').click();
  await expect(page.locator('app-shell')).toBeVisible();
}

/** Switches the UI to English via the language picker (persists the preference when signed in). */
export async function useEnglish(page: Page): Promise<void> {
  await page.evaluate(() => localStorage.setItem('crm.language', 'en'));
}
