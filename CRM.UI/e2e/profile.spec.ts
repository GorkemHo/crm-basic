import { Page, expect, test } from '@playwright/test';

import { login } from './helpers';

/** Registers a fresh user through the UI so tests never change the admin account. */
async function registerUser(page: Page): Promise<{ email: string; password: string }> {
  const email = `e2e.profile.${Date.now()}@example.com`;
  const password = 'Passw0rd-123';

  await page.goto('/register');
  await page.locator('#firstName').fill('Profile');
  await page.locator('#lastName').fill('Tester');
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);
  await page.locator('#confirmPassword').fill(password);
  await page.locator('form button[type="submit"]').click();
  await expect(page).toHaveURL(/\/login$/);

  return { email, password };
}

test('dashboard shows counts and latest records', async ({ page }) => {
  await login(page);

  await expect(page.locator('app-page-header h1')).toContainText(/Welcome|Hoş geldin/);
  await expect(page.locator('.kpi')).toHaveCount(4);
  // Latest activities, companies and customers.
  await expect(page.locator('.lists .card')).toHaveCount(3);
});

test('language preference is saved and survives a reload', async ({ page }) => {
  const { email, password } = await registerUser(page);
  await login(page, email, password);

  // New users default to Turkish.
  await expect(page.locator('.sidebar')).toContainText('Şirketler');

  await page.goto('/profile');
  await page.locator('mat-button-toggle', { hasText: 'English' }).click();
  await expect(page.locator('.sidebar')).toContainText('Companies');
  await expect(page.locator('mat-snack-bar-container', { hasText: /Language preference saved/ })).toBeVisible();

  await page.reload();
  await expect(page.locator('.sidebar')).toContainText('Companies');
});

test('theme switch toggles dark mode', async ({ page }) => {
  await login(page);
  const html = page.locator('html');
  const wasDark = (await html.getAttribute('class'))?.includes('app-dark') ?? false;

  await page.locator('.topbar button', { has: page.locator('text=/dark_mode|light_mode/') }).click();

  await expect(html).toHaveClass(wasDark ? /^(?!.*app-dark)/ : /app-dark/);
});

test('changing the password keeps you signed in and the new password works', async ({ page }) => {
  const { email, password } = await registerUser(page);
  await login(page, email, password);

  await page.goto('/profile');
  await page.locator('#currentPassword').fill('wrong-password');
  await page.locator('#newPassword').fill('NewPassw0rd-1');
  await page.locator('#confirmPassword').fill('NewPassw0rd-1');
  await page.locator('form button[type="submit"]').click();
  await expect(page.locator('mat-error')).toContainText(/current password|Mevcut şifre/);

  await page.locator('#currentPassword').fill(password);
  await page.locator('form button[type="submit"]').click();
  await expect(page.locator('mat-snack-bar-container', { hasText: /password was changed|Şifreniz değiştirildi/ })).toBeVisible();

  // Still signed in after a reload (the session was renewed with the new password).
  await page.reload();
  await expect(page.locator('app-shell')).toBeVisible();

  await page.context().clearCookies();
  await page.evaluate(() => localStorage.clear());
  await login(page, email, 'NewPassw0rd-1');
});
