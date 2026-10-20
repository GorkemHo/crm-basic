import { expect, test } from '@playwright/test';

import { login } from './helpers';

// Demo data is seeded when the API runs with Seed:DemoData (on in Development and CI).
const DEMO_PASSWORD = process.env['E2E_DEMO_PASSWORD'] ?? 'Demo1234!';

test('demo companies and their customers are listed', async ({ page }) => {
  await login(page);

  await page.goto('/companies');
  await page.locator('input[type="search"]').fill('Anadolu Lojistik');
  await expect(page.locator('tr.mat-mdc-row')).toHaveCount(1);
  await page.locator('tr.mat-mdc-row a', { hasText: 'Anadolu Lojistik' }).click();

  await page.getByRole('tab', { name: /Customers|Müşteriler/ }).click();
  await expect(page.locator('app-customer-table table')).toContainText('hakan.arslan@anadolu-lojistik.test');
});

test('the demo sales manager sees users but cannot manage them', async ({ page }) => {
  await login(page, 'ayse.yilmaz@demo.local', DEMO_PASSWORD);

  await expect(page.locator('.sidebar')).toContainText(/Users|Kullanıcılar/);
  await expect(page.locator('.sidebar')).not.toContainText(/Roles|Roller/);

  await page.goto('/users');
  await expect(page.locator('tr.mat-mdc-row').first()).toBeVisible();
  await expect(page.locator('app-page-header button')).toHaveCount(0);
});
