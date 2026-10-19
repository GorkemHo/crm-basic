import { Page, expect, test } from '@playwright/test';

import { login, unique } from './helpers';

const dialog = (page: Page) => page.locator('mat-dialog-container');
const headerButton = (page: Page, text: RegExp) => page.locator('app-page-header button', { hasText: text });

/** Creates a company from the list page; ends on its detail page. */
async function createCompany(page: Page, name: string): Promise<void> {
  await page.goto('/companies');
  await page.locator('app-page-header button').first().click();
  await dialog(page).locator('#companyName').fill(name);
  await dialog(page).locator('button[type="submit"]').click();
  await expect(page.locator('app-page-header h1')).toHaveText(name);
}

test('create, find, edit, view history and delete a company', async ({ page }) => {
  const name = unique('E2E Company');
  const renamed = `${name} (renamed)`;

  await login(page);
  await createCompany(page, name);

  // It can be found through the list's search box.
  await page.goto('/companies');
  await page.locator('input[type="search"]').fill(name);
  await expect(page.locator('tr.mat-mdc-row')).toHaveCount(1);
  await page.locator('tr.mat-mdc-row a', { hasText: name }).click();

  // Edit
  await headerButton(page, /Edit|Düzenle/).click();
  await dialog(page).locator('#companyName').fill(renamed);
  await dialog(page).locator('button[type="submit"]').click();
  await expect(page.locator('app-page-header h1')).toHaveText(renamed);

  // History shows the rename
  await page.getByRole('tab', { name: /History|Değişiklik geçmişi/ }).click();
  await expect(page.locator('app-history-timeline')).toContainText(renamed);

  // Delete
  await headerButton(page, /Delete|Sil/).click();
  await dialog(page).locator('button.danger').click();
  await expect(page).toHaveURL(/\/companies$/);
});

test('validation errors are shown on the field', async ({ page }) => {
  await login(page);
  await page.goto('/companies');
  await page.locator('app-page-header button').first().click();
  await dialog(page).locator('button[type="submit"]').click();

  await expect(dialog(page).locator('mat-error')).toContainText(/required|zorunlu/);
});

test('add a customer from the company page; a company with customers cannot be deleted', async ({ page }) => {
  const company = unique('E2E Customer Co');
  const email = `e2e.${Date.now()}@example.com`;

  await login(page);
  await createCompany(page, company);

  await page.getByRole('tab', { name: /Customers|Müşteriler/ }).click();
  await page.locator('app-customer-table .new-button').click();
  await expect(dialog(page).locator('#companyId')).toHaveValue(company);
  await dialog(page).locator('#firstName').fill('Grace');
  await dialog(page).locator('#lastName').fill('Hopper');
  await dialog(page).locator('#email').fill(email);
  await dialog(page).locator('button[type="submit"]').click();

  await expect(page.locator('app-customer-table table')).toContainText(email);

  // The API's conflict is shown, translated.
  await headerButton(page, /Delete|Sil/).click();
  await dialog(page).locator('button.danger').click();
  await expect(page.locator('mat-snack-bar-container')).toContainText(/cannot be deleted|silinemez/);
});

test('customer list filters by company', async ({ page }) => {
  const company = unique('E2E Filter Co');
  const email = `e2e.filter.${Date.now()}@example.com`;

  await login(page);
  await createCompany(page, company);
  await page.getByRole('tab', { name: /Customers|Müşteriler/ }).click();
  await page.locator('app-customer-table .new-button').click();
  await dialog(page).locator('#firstName').fill('Ada');
  await dialog(page).locator('#lastName').fill('Lovelace');
  await dialog(page).locator('#email').fill(email);
  await dialog(page).locator('button[type="submit"]').click();
  await expect(page.locator('app-customer-table table')).toContainText(email);

  await page.goto('/customers');
  await page.locator('app-company-select input').fill(company);
  await page.locator('mat-option', { hasText: company }).click();

  await expect(page.locator('tr.mat-mdc-row')).toHaveCount(1);
  await expect(page.locator('tr.mat-mdc-row')).toContainText(email);
});
