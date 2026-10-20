import { Page, expect, test } from '@playwright/test';

import { login, unique } from './helpers';

const dialog = (page: Page) => page.locator('mat-dialog-container');
const entry = (page: Page, subject: string) => page.locator('app-activity-timeline li.activity', { hasText: subject });

async function createCompany(page: Page): Promise<string> {
  const name = unique('E2E Activity Co');
  await page.goto('/companies');
  await page.locator('app-page-header button').click();
  await dialog(page).locator('#companyName').fill(name);
  await dialog(page).locator('button[type="submit"]').click();
  await expect(page.locator('app-page-header h1')).toHaveText(name);
  return name;
}

test('a call is logged, edited and deleted on the company page', async ({ page }) => {
  await login(page);
  const company = await createCompany(page);
  const subject = unique('Intro call');

  await page.getByRole('tab', { name: /Activities|Aktiviteler/ }).click();
  await expect(page.locator('app-activity-timeline')).toContainText(/No activities yet|Henüz aktivite yok/);

  await page.locator('app-activity-timeline .log-button').click();
  await expect(dialog(page).locator('#activityCompany')).toHaveValue(company);
  await dialog(page).locator('mat-button-toggle[data-type="call"] button').click();
  await dialog(page).locator('#subject').fill(subject);
  await dialog(page).locator('#durationMinutes').fill('15');
  await dialog(page).locator('#notes').fill('Talked about next steps');
  await dialog(page).locator('button[type="submit"]').click();
  await expect(dialog(page)).toHaveCount(0);

  await expect(entry(page, subject)).toContainText(/Call|Arama/);
  await expect(entry(page, subject)).toContainText(/15 (min|dk)/);
  await expect(entry(page, subject)).toContainText('Talked about next steps');

  // The author may edit and delete it.
  const edited = `${subject} (edited)`;
  await entry(page, subject).locator('.entry-menu').click();
  await page.locator('.mat-mdc-menu-panel button', { hasText: /Edit|Düzenle/ }).click();
  await dialog(page).locator('#subject').fill(edited);
  await dialog(page).locator('button[type="submit"]').click();
  await expect(entry(page, edited)).toBeVisible();

  await entry(page, edited).locator('.entry-menu').click();
  await page.locator('.mat-mdc-menu-panel button', { hasText: /Delete|Sil/ }).click();
  await page.locator('mat-dialog-container button', { hasText: /Delete|Sil/ }).click();
  await expect(entry(page, edited)).toHaveCount(0);
});

test('an activity logged on a deal shows on the deal and in the activity list', async ({ page }) => {
  await login(page);
  const company = await createCompany(page);
  const dealTitle = unique('Activity deal');
  const subject = unique('Proposal meeting');

  // A deal for the company, from its Deals tab.
  await page.getByRole('tab', { name: /Deals|Fırsatlar/ }).click();
  await page.locator('app-deal-table .new-button').click();
  await dialog(page).locator('#title').fill(dealTitle);
  await dialog(page).locator('#amount').fill('500');
  await dialog(page).locator('button[type="submit"]').click();
  await page.locator('app-deal-table a', { hasText: dealTitle }).click();
  await expect(page.locator('app-page-header h1')).toHaveText(dealTitle);

  await page.getByRole('tab', { name: /Activities|Aktiviteler/ }).click();
  await page.locator('app-activity-timeline .log-button').click();
  await expect(dialog(page).locator('#activityDeal')).toContainText(dealTitle);
  await dialog(page).locator('mat-button-toggle[data-type="meeting"] button').click();
  await dialog(page).locator('#subject').fill(subject);
  await dialog(page).locator('button[type="submit"]').click();
  await expect(entry(page, subject)).toBeVisible();
  // On the deal page the deal link is left out, the company link is shown.
  await expect(entry(page, subject).locator('.links')).toContainText(company);
  await expect(entry(page, subject).locator('.links')).not.toContainText(dealTitle);

  await page.goto('/activities');
  await page.locator('input[type="search"]').fill(subject);
  await expect(page.locator('app-activity-timeline li.activity')).toHaveCount(1);
  await entry(page, subject).locator('.links a', { hasText: dealTitle }).click();
  await expect(page.locator('app-page-header h1')).toHaveText(dealTitle);
});

test('demo activities are listed and filtered by type', async ({ page }) => {
  await login(page);
  await page.goto('/activities');

  await page.locator('#typeFilter').click();
  await page.locator('mat-option', { hasText: /Meeting|Toplantı/ }).click();
  await expect(entry(page, 'Filo takip ihtiyaç analizi toplantısı')).toBeVisible();
  await expect(page.locator('app-activity-timeline li.activity:not([data-type="meeting"])')).toHaveCount(0);
});

test('the dashboard lists the latest activities', async ({ page }) => {
  await login(page);
  await expect(page.locator('.recent-activities .row').first()).toBeVisible();
});
