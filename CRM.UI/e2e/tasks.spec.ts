import { Page, expect, test } from '@playwright/test';

import { login, unique } from './helpers';

const DEMO_PASSWORD = process.env['E2E_DEMO_PASSWORD'] ?? 'Demo1234!';
const dialog = (page: Page) => page.locator('mat-dialog-container');
const task = (page: Page, title: string) => page.locator('app-task-list li.task', { hasText: title });

function today(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

test('a task assigned on a company page shows up for the assignee, who completes it', async ({ page, browser }) => {
  const company = unique('E2E Task Co');
  const title = unique('Call back about pricing');

  await login(page);
  await page.goto('/companies');
  await page.locator('app-page-header button').click();
  await dialog(page).locator('#companyName').fill(company);
  await dialog(page).locator('button[type="submit"]').click();
  await expect(page.locator('app-page-header h1')).toHaveText(company);

  await page.getByRole('tab', { name: /Tasks|Görevler/ }).click();
  await page.locator('app-task-list .new-task').click();
  await expect(dialog(page).locator('#taskCompany')).toHaveValue(company);
  await dialog(page).locator('#taskTitle').fill(title);
  await dialog(page).locator('#assigneeId').click();
  await page.locator('mat-option', { hasText: 'Mehmet Kaya' }).click();
  await dialog(page).locator('#dueDate').fill(today());
  await dialog(page).locator('#priority').click();
  await page.locator('mat-option', { hasText: /High|Yüksek/ }).click();
  await dialog(page).locator('button[type="submit"]').click();
  await expect(dialog(page)).toHaveCount(0);

  await expect(task(page, title)).toContainText('Mehmet Kaya');
  await expect(task(page, title)).toContainText(/Today|Bugün/);
  await expect(task(page, title)).toContainText(/High|Yüksek/);

  // The assignee sees it on the dashboard and marks it done.
  const context = await browser.newContext();
  const mehmet = await context.newPage();
  await login(mehmet, 'mehmet.kaya@demo.local', DEMO_PASSWORD);
  await expect(mehmet.locator('section.my-tasks')).toBeVisible();

  await mehmet.goto('/tasks');
  await expect(task(mehmet, title)).toBeVisible();
  await task(mehmet, title).locator('mat-checkbox input').check();
  await expect(task(mehmet, title)).toHaveCount(0);

  // Done tasks are listed under "Done".
  await mehmet.locator('#taskStatus').click();
  await mehmet.locator('mat-option', { hasText: /^\s*(Done|Tamamlandı)\s*$/ }).click();
  await expect(task(mehmet, title)).toHaveClass(/done/);
  await context.close();
});

test('my tasks and everyone\'s tasks', async ({ page }) => {
  const title = unique('Personal to-do');

  await login(page);
  await page.goto('/tasks');
  await page.locator('app-task-list .new-task').click();
  await dialog(page).locator('#taskTitle').fill(title);
  await dialog(page).locator('button[type="submit"]').click();
  await expect(task(page, title)).toBeVisible();

  // Demo tasks of other people are hidden under "Mine" and shown under "Everyone".
  await page.locator('input[type="search"]').fill('Filo takip teklifini');
  await expect(page.locator('app-task-list li.task')).toHaveCount(0);
  await page.locator('mat-button-toggle', { hasText: /Everyone|Herkes/ }).locator('button').click();
  await expect(task(page, 'Filo takip teklifini revize et')).toContainText('Ayşe Yılmaz');
});

test('the demo sales manager sees overdue and due-today counts on the dashboard', async ({ page }) => {
  await login(page, 'ayse.yilmaz@demo.local', DEMO_PASSWORD);

  const card = page.locator('section.my-tasks');
  await expect(card.locator('.count.overdue')).not.toHaveClass(/zero/);
  await expect(card.locator('.count.today')).not.toHaveClass(/zero/);
  await expect(card.locator('li.task').first()).toContainText('Filo takip teklifini revize et');
});
