import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';

import { login } from './helpers';

const DEMO_PASSWORD = process.env['E2E_DEMO_PASSWORD'] ?? 'Demo1234!';

test('reports show sales history per currency, the pipeline, activities and tasks', async ({ page }) => {
  await login(page);
  await page.goto('/reports');

  // Demo history: deals won over several months, in all three currencies.
  await expect(page.locator('[data-kpi="won"] strong')).not.toHaveText('0');
  await expect(page.locator('[data-kpi="winRate"] strong')).toContainText('%');
  for (const currency of ['EUR', 'TRY', 'USD']) {
    await expect(page.locator(`.multiple[data-currency="${currency}"] app-bar-chart`)).toBeVisible();
  }

  // The numbers behind a chart are one click away.
  const table = page.locator('.multiple[data-currency="TRY"] details');
  await table.locator('summary').click();
  await expect(table.locator('tbody tr')).toHaveCount(6);

  // Hovering a column shows its values.
  await page.locator('.multiple[data-currency="TRY"] rect.hit').last().hover();
  await expect(page.locator('.multiple[data-currency="TRY"] .tooltip')).toContainText('TRY');

  await expect(page.locator('.pipeline-table tbody tr').first()).toBeVisible();
  await expect(page.locator('.task-table')).toContainText('Ayşe Yılmaz');

  // A shorter period has fewer months.
  await page.locator('mat-button-toggle', { hasText: /3 months|3 ay/ }).locator('button').click();
  await expect(table.locator('tbody tr')).toHaveCount(3);
});

test('a report table downloads as CSV', async ({ page }) => {
  await login(page);
  await page.goto('/reports');
  await expect(page.locator('.task-table tbody tr').first()).toBeVisible();

  const section = page.locator('section', { has: page.locator('.task-table') });
  const [download] = await Promise.all([page.waitForEvent('download'), section.locator('header button').click()]);

  expect(download.suggestedFilename()).toMatch(/^tasks_\d{4}-\d{2}-\d{2}_\d{4}-\d{2}-\d{2}\.csv$/);
  const csv = readFileSync(await download.path()).toString('utf8');
  expect(csv.startsWith('﻿')).toBe(true);
  expect(csv).toContain('Ayşe Yılmaz;');
});

test('reports are hidden from people without the reporting permission', async ({ page }) => {
  await login(page, 'mehmet.kaya@demo.local', DEMO_PASSWORD);
  await expect(page.locator('.sidebar')).not.toContainText(/Reports|Raporlar/);

  await page.goto('/reports');
  await expect(page).toHaveURL(/\/forbidden$/);
});
