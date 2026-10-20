import { Page, expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';

import { login, unique } from './helpers';

const dialog = (page: Page) => page.locator('mat-dialog-container');
const file = (page: Page, name: string) => page.locator('app-attachments-panel li.file', { hasText: name });

async function createCompany(page: Page): Promise<string> {
  const name = unique('E2E Files Co');
  await page.goto('/companies');
  await page.locator('app-page-header button').click();
  await dialog(page).locator('#companyName').fill(name);
  await dialog(page).locator('button[type="submit"]').click();
  await expect(page.locator('app-page-header h1')).toHaveText(name);
  return name;
}

test('a file is uploaded to a company, downloaded and deleted', async ({ page }) => {
  await login(page);
  await createCompany(page);
  await page.getByRole('tab', { name: /Files|Dosyalar/ }).click();
  await expect(page.locator('app-attachments-panel')).toContainText(/No files yet|Henüz dosya yok/);

  await page.locator('app-attachments-panel input[type="file"]').setInputFiles({
    name: 'teklif.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from('Teklif v1'),
  });
  await expect(file(page, 'teklif.txt')).toContainText(/9 (byte|B)/);

  const [download] = await Promise.all([page.waitForEvent('download'), file(page, 'teklif.txt').locator('button.name').click()]);
  expect(download.suggestedFilename()).toBe('teklif.txt');
  expect(readFileSync(await download.path()).toString()).toBe('Teklif v1');

  // A company with files cannot be deleted.
  await page.locator('app-page-header button', { hasText: /Delete|Sil/ }).click();
  await dialog(page).locator('button.danger').click();
  await expect(page.locator('mat-snack-bar-container', { hasText: /has files|Dosyası olan/ })).toBeVisible();

  await file(page, 'teklif.txt').locator('button[aria-label="Delete"], button[aria-label="Sil"]').click();
  await dialog(page).locator('button.danger').click();
  await expect(file(page, 'teklif.txt')).toHaveCount(0);
});

test('unsafe file types are refused with a message', async ({ page }) => {
  await login(page);
  await createCompany(page);
  await page.getByRole('tab', { name: /Files|Dosyalar/ }).click();

  await page.locator('app-attachments-panel input[type="file"]').setInputFiles({
    name: 'page.html',
    mimeType: 'text/html',
    buffer: Buffer.from('<script>alert(1)</script>'),
  });

  await expect(page.locator('mat-snack-bar-container', { hasText: /cannot be uploaded|yüklenemez/ })).toBeVisible();
  await expect(file(page, 'page.html')).toHaveCount(0);
});
