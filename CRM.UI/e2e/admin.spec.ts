import { Page, expect, test } from '@playwright/test';

import { login, unique } from './helpers';

const dialog = (page: Page) => page.locator('mat-dialog-container');

test('a role with a granted permission changes what its users can do', async ({ page, browser }) => {
  const roleName = unique('Deleters').slice(0, 40);
  const email = `e2e.user.${Date.now()}@example.com`;
  const password = 'Passw0rd-123';

  await login(page);

  // Create a role and grant "delete companies" in the permission matrix.
  await page.goto('/roles');
  await page.locator('app-page-header button').click();
  await dialog(page).locator('#roleName').fill(roleName);
  await dialog(page).locator('button[type="submit"]').click();
  await expect(page.locator('app-page-header h1')).toHaveText(roleName);

  await page.locator('mat-checkbox', { hasText: 'crm.companies.delete' }).locator('input').check();
  await page.locator('app-page-header button', { hasText: /Save|Kaydet/ }).click();
  await expect(page.locator('mat-snack-bar-container', { hasText: /Permissions saved|İzinler kaydedildi/ })).toBeVisible();

  // Create a user with the built-in User role plus the new one.
  await page.goto('/users');
  await page.locator('app-page-header button').click();
  await dialog(page).locator('#firstName').fill('E2E');
  await dialog(page).locator('#lastName').fill('User');
  await dialog(page).locator('#email').fill(email);
  await dialog(page).locator('#password').fill(password);
  await dialog(page).locator('#roleIds').click();
  await page.locator('mat-option', { hasText: /^\s*User\s*$/ }).click();
  await page.locator('mat-option', { hasText: roleName }).click();
  await page.keyboard.press('Escape');
  await dialog(page).locator('button[type="submit"]').click();
  await expect(page.locator('app-page-header h1')).toHaveText('E2E User');

  // The new user: CRM pages yes, administration no, and may delete companies.
  const context = await browser.newContext();
  const userPage = await context.newPage();
  await login(userPage, email, password);

  await expect(userPage.locator('.sidebar')).toContainText(/Companies|Şirketler/);
  await expect(userPage.locator('.sidebar')).not.toContainText(/Users|Kullanıcılar/);

  await userPage.goto('/companies');
  await expect(userPage.locator('tr.mat-mdc-row').first()).toBeVisible();
  await expect(userPage.locator('tr.mat-mdc-row').first().locator('button[aria-label="Delete"], button[aria-label="Sil"]')).toBeVisible();

  // Administration pages are blocked.
  await userPage.goto('/users');
  await expect(userPage).toHaveURL(/\/forbidden$/);

  await context.close();
});

test('the Admin role is shown read-only', async ({ page }) => {
  await login(page);
  await page.goto('/roles');
  await page.locator('tr.mat-mdc-row a', { hasText: /^Admin$/ }).click();

  await expect(page.locator('.notice')).toContainText(/always has every permission|her zaman tüm izinlere/);
  await expect(page.locator('mat-checkbox input').first()).toBeDisabled();
  await expect(page.locator('app-page-header button', { hasText: /Save|Kaydet/ })).toHaveCount(0);
});

test('you cannot delete yourself', async ({ page }) => {
  await login(page);
  await page.goto('/users');
  const me = page.locator('tr.mat-mdc-row', { has: page.locator('.you') });
  await expect(me).toHaveCount(1);
  await expect(me.locator('button[aria-label="Delete"], button[aria-label="Sil"]')).toHaveCount(0);
});
