import { expect, test } from '@playwright/test';

import { ADMIN_EMAIL, login } from './helpers';

test('protected pages redirect to login and come back after signing in', async ({ page }) => {
  await page.goto('/companies');
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fcompanies/);

  await page.locator('#email').fill(ADMIN_EMAIL);
  await page.locator('#password').fill('dev12345');
  await page.locator('form button[type="submit"]').click();

  await expect(page).toHaveURL(/\/companies$/);
});

test('wrong password shows the translated API error', async ({ page }) => {
  await page.goto('/login');
  await page.locator('#email').fill(ADMIN_EMAIL);
  await page.locator('#password').fill('definitely-wrong');
  await page.locator('form button[type="submit"]').click();

  await expect(page.locator('.alert')).toContainText(/E-posta veya şifre hatalı|Invalid email or password/);
});

test('signed-in user sees the menu and can sign out', async ({ page }) => {
  await login(page);

  await expect(page.locator('.sidebar')).toContainText(/Companies|Şirketler/);
  await expect(page.locator('.sidebar')).toContainText(/Users|Kullanıcılar/);

  await page.locator('.user-button').click();
  await page.getByRole('menuitem').filter({ hasText: /Sign out|Çıkış yap/ }).click();

  await expect(page).toHaveURL(/\/login/);
});
