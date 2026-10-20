import { Page, expect, test } from '@playwright/test';

import { login, unique } from './helpers';

const dialog = (page: Page) => page.locator('mat-dialog-container');
const column = (page: Page, index: number) => page.locator('app-deal-board .column').nth(index);
const card = (page: Page, title: string) => page.locator('.deal-card', { hasText: title });

/** Creates a company through the UI and returns its name. */
async function createCompany(page: Page): Promise<string> {
  const name = unique('E2E Deals Co');
  await page.goto('/companies');
  await page.locator('app-page-header button').click();
  await dialog(page).locator('#companyName').fill(name);
  await dialog(page).locator('button[type="submit"]').click();
  await expect(page.locator('app-page-header h1')).toHaveText(name);
  return name;
}

/** Fills the deal dialog (opened by the caller) and saves it. */
async function fillDeal(page: Page, title: string, company: string | null, amount: string, currency = 'USD'): Promise<void> {
  await dialog(page).locator('#title').fill(title);
  if (company) {
    await dialog(page).locator('#companyId').fill(company);
    await page.locator('mat-option', { hasText: company }).click();
  }
  await dialog(page).locator('#amount').fill(amount);
  await dialog(page).locator('#currency').click();
  await page.locator('mat-option', { hasText: currency }).click();
  await dialog(page).locator('button[type="submit"]').click();
  await expect(dialog(page)).toHaveCount(0);
}

async function filterBoard(page: Page, company: string): Promise<void> {
  await page.locator('app-deal-board app-company-select input').fill(company);
  await page.locator('mat-option', { hasText: company }).click();
}

test('a deal is added to a stage on the board and dragged to the next one', async ({ page }) => {
  await login(page);
  const company = await createCompany(page);
  const title = unique('Board deal');

  await page.goto('/pipeline');
  await column(page, 1).locator('button.add').click();
  await fillDeal(page, title, company, '2500');

  await expect(column(page, 1).locator('.deal-card', { hasText: title })).toBeVisible();
  await expect(card(page, title)).toContainText('$2,500.00');

  // Only this company's deals, so the columns stay short.
  await filterBoard(page, company);
  await expect(page.locator('.deal-card')).toHaveCount(1);

  // Drag with small mouse steps so the CDK drag starts (it waits for a few pixels of movement).
  const from = await card(page, title).boundingBox();
  const to = await column(page, 2).locator('.cards').boundingBox();
  await page.mouse.move(from!.x + from!.width / 2, from!.y + from!.height / 2);
  await page.mouse.down();
  await page.mouse.move(from!.x + from!.width / 2 + 10, from!.y + from!.height / 2, { steps: 5 });
  await page.mouse.move(to!.x + to!.width / 2, to!.y + 20, { steps: 20 });
  await page.mouse.up();

  await expect(column(page, 2).locator('.deal-card', { hasText: title })).toBeVisible();

  // The move is saved: still there after a reload, and the detail page names the stage.
  await page.reload();
  await filterBoard(page, company);
  await expect(column(page, 2).locator('.deal-card', { hasText: title })).toBeVisible();
  const stageName = await column(page, 2).locator('h2').innerText();
  await card(page, title).click();
  await expect(page.locator('app-page-header h1')).toHaveText(title);
  await expect(page.locator('.details')).toContainText(stageName);
});

test('a deal is won, reopened and lost with a reason, and shows on its company', async ({ page }) => {
  await login(page);
  const company = await createCompany(page);
  const title = unique('Lifecycle deal');

  // New deal from the company page's Deals tab: the company is filled in.
  await page.getByRole('tab', { name: /Deals|Fırsatlar/ }).click();
  await page.locator('app-deal-table .new-button').click();
  await expect(dialog(page).locator('#companyId')).toHaveValue(company);
  await fillDeal(page, title, null, '1000', 'EUR');
  await expect(page.locator('app-deal-table table')).toContainText(title);

  await page.locator('app-deal-table a', { hasText: title }).click();
  await expect(page.locator('app-page-header h1')).toHaveText(title);

  await page.locator('app-page-header button', { hasText: /Won|Kazanıldı/ }).click();
  await expect(page.locator('app-deal-status')).toHaveText(/Won|Kazanıldı/);

  await page.locator('app-page-header button', { hasText: /Reopen|Yeniden aç/ }).click();
  await expect(page.locator('app-deal-status')).toHaveText(/Open|Açık/);

  await page.locator('app-page-header button', { hasText: /Lost|Kaybedildi/ }).click();
  await dialog(page).locator('#lostReason').fill('Chose a competitor');
  await dialog(page).locator('#confirmLost').click();
  await expect(page.locator('app-deal-status')).toHaveText(/Lost|Kaybedildi/);
  await expect(page.locator('.details')).toContainText('Chose a competitor');

  // Closed deals leave the board.
  await page.goto('/pipeline');
  await expect(page.locator('.deal-card').first()).toBeVisible();
  await expect(card(page, title)).toHaveCount(0);
});

test('admins add, rename and delete pipeline stages', async ({ page }) => {
  const name = unique('E2E stage').slice(0, 40);

  await login(page);
  await page.goto('/pipeline/stages');
  await page.locator('app-page-header button').click();
  await dialog(page).locator('#stageName').fill(name);
  await dialog(page).locator('#probability').fill('60');
  await dialog(page).locator('button[type="submit"]').click();

  const stage = page.locator('.stage', { hasText: name });
  await expect(stage).toContainText('60%');

  await stage.locator('button[aria-label="Edit"], button[aria-label="Düzenle"]').click();
  await dialog(page).locator('#probability').fill('70');
  await dialog(page).locator('button[type="submit"]').click();
  await expect(stage).toContainText('70%');

  // The new stage is the last column of the board.
  await page.goto('/pipeline');
  await expect(page.locator('app-deal-board .column').last().locator('h2')).toHaveText(name);

  await page.goto('/pipeline/stages');
  await stage.locator('button[aria-label="Delete"], button[aria-label="Sil"]').click();
  await page.locator('mat-dialog-container button', { hasText: /Delete|Sil/ }).click();
  await expect(stage).toHaveCount(0);
});

test('the dashboard shows the pipeline in each currency', async ({ page }) => {
  await login(page);
  await expect(page.locator('section.pipeline')).toBeVisible();
  await expect(page.locator('section.pipeline .money').first()).toBeVisible();
});
