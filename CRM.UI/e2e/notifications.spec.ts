import { Browser, Page, expect, test } from '@playwright/test';

import { login, unique } from './helpers';

const DEMO_PASSWORD = process.env['E2E_DEMO_PASSWORD'] ?? 'Demo1234!';
const dialog = (page: Page) => page.locator('mat-dialog-container');

/** The admin assigns a new task to a demo user. */
async function assignTask(page: Page, assignee: string, title: string): Promise<void> {
  await page.goto('/tasks');
  await page.locator('app-task-list .new-task').click();
  await dialog(page).locator('#taskTitle').fill(title);
  await dialog(page).locator('#assigneeId').click();
  await page.locator('mat-option', { hasText: assignee }).click();
  await dialog(page).locator('button[type="submit"]').click();
  await expect(dialog(page)).toHaveCount(0);
}

/** Signs a demo user in, in a separate browser context. */
async function signInAs(browser: Browser, email: string): Promise<Page> {
  const page = await (await browser.newContext()).newPage();
  await login(page, email, DEMO_PASSWORD);
  return page;
}

/** Notifications arrive through the outbox a moment after the change. */
async function waitForNotification(page: Page, text: string): Promise<void> {
  await expect(async () => {
    await page.goto('/notifications');
    await expect(page.locator('.notification', { hasText: text })).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 30_000 });
}

test('assigning a task notifies the assignee, who opens it from the bell', async ({ page, browser }) => {
  const first = unique('Check the invoice');
  const second = unique('Update the price list');

  await login(page);
  await assignTask(page, 'Elif Demir', first);
  await assignTask(page, 'Elif Demir', second);

  const elif = await signInAs(browser, 'elif.demir@demo.local');
  await waitForNotification(elif, second);
  await waitForNotification(elif, first);

  // Elif's UI is in English.
  const item = elif.locator('.notification', { hasText: first });
  await expect(item).toContainText(`assigned you a task: ${first}`);
  await expect(item).toHaveClass(/unread/);

  // Opening one from the bell marks it read and goes to the tasks.
  await expect(elif.locator('app-notification-bell .mat-badge-content')).toBeVisible();
  await elif.locator('app-notification-bell button.bell').click();
  await elif.locator('.notification-menu .item', { hasText: first }).click();
  await expect(elif).toHaveURL(/\/tasks$/);

  await elif.goto('/notifications');
  await expect(item).not.toHaveClass(/unread/);
  await expect(elif.locator('.notification', { hasText: second })).toHaveClass(/unread/);

  // Mark all read clears the badge.
  await elif.locator('app-page-header button', { hasText: /Mark all as read|Tümünü okundu yap/ }).click();
  await expect(elif.locator('.notification', { hasText: second })).not.toHaveClass(/unread/);
  await expect(elif.locator('app-notification-bell .mat-badge-content')).toBeHidden();
  await elif.context().close();
});

test('notifications are written in the reader\'s language', async ({ page, browser }) => {
  const title = unique('Teklif hazırla');

  await login(page);
  await assignTask(page, 'Ayşe Yılmaz', title);

  // Ayşe uses Turkish; the admin's name is the actor.
  const ayse = await signInAs(browser, 'ayse.yilmaz@demo.local');
  await waitForNotification(ayse, title);
  await expect(ayse.locator('.notification', { hasText: title })).toContainText(`size bir görev atadı: ${title}`);

  await ayse.locator('app-notification-bell button.bell').click();
  await expect(ayse.locator('.notification-menu .item', { hasText: title })).toBeVisible();
  await ayse.context().close();
});
