import { test, expect } from '@playwright/test';
import { getDemoCredentials, loginAsDemoUser, waitForAppReady } from './helpers/partnership-demo.helpers';

async function ensureAuthenticated(page: import('@playwright/test').Page): Promise<boolean> {
  const creds = getDemoCredentials();
  if (!creds) {
    return false;
  }
  await loginAsDemoUser(page);
  return true;
}

function assertFullyInViewport(
  box: { x: number; y: number; width: number; height: number },
  viewport: { width: number; height: number }
): void {
  expect(box.width).toBeGreaterThan(0);
  expect(box.height).toBeGreaterThan(0);
  expect(box.x).toBeGreaterThanOrEqual(-1);
  expect(box.y).toBeGreaterThanOrEqual(-1);
  expect(box.x + box.width).toBeLessThanOrEqual(viewport.width + 1);
  expect(box.y + box.height).toBeLessThanOrEqual(viewport.height + 1);
}

async function openFirstDetailFromList(
  page: import('@playwright/test').Page,
  listPath: string,
  detailUrlPattern: RegExp
): Promise<boolean> {
  await page.goto(listPath);
  await waitForAppReady(page);

  const detailLink = page.locator(`table tbody tr a[href*="${listPath}/"], a[href*="${listPath}/"]`).first();
  if ((await detailLink.count()) === 0) {
    // Fallback: click first row that navigates
    const row = page.locator('table tbody tr').first();
    if ((await row.count()) === 0) {
      return false;
    }
    await row.click();
  } else {
    await detailLink.click();
  }

  try {
    await page.waitForURL(detailUrlPattern, { timeout: 15000 });
    return true;
  } catch {
    return false;
  }
}

async function assertActionsMenuFullyVisible(
  page: import('@playwright/test').Page,
  viewport: { width: number; height: number }
): Promise<void> {
  const trigger = page.getByRole('button', { name: /Actions|Menu des actions/i }).first();
  await expect(trigger).toBeVisible({ timeout: 10000 });
  await trigger.click();

  const menu = page.locator('.ft-document-actions-menu.p-menu, .p-menu.p-menu-overlay, .p-menu').last();
  await expect(menu).toBeVisible({ timeout: 5000 });

  const box = await menu.boundingBox();
  expect(box).toBeTruthy();
  expect(box!.width).toBeGreaterThanOrEqual(200);
  assertFullyInViewport(box!, viewport);

  // At least one full (non-truncated) label should be readable
  const itemLabels = menu.locator('.p-menuitem-text, .p-menuitem-link span, .p-menuitem-content');
  await expect(itemLabels.first()).toBeVisible();
  const text = (await itemLabels.first().innerText()).trim();
  expect(text.length).toBeGreaterThan(3);
  expect(text.endsWith('...')).toBeFalsy();
}

const detailCases: Array<{ name: string; listPath: string; detailPattern: RegExp }> = [
  { name: 'invoice detail', listPath: '/invoices', detailPattern: /\/invoices\/[0-9a-f-]{36}/i },
  { name: 'delivery note detail', listPath: '/delivery-notes', detailPattern: /\/delivery-notes\/[0-9a-f-]{36}/i },
  { name: 'sales order detail', listPath: '/sales-orders', detailPattern: /\/sales-orders\/[0-9a-f-]{36}/i }
];

test.describe('Document detail Actions menus', () => {
  for (const viewport of [
    { width: 1280, height: 800 },
    { width: 768, height: 1024 }
  ]) {
    for (const detailCase of detailCases) {
      test(`${detailCase.name} Actions menu is fully visible at ${viewport.width}x${viewport.height}`, async ({
        page
      }) => {
        if (!(await ensureAuthenticated(page))) {
          test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
        }

        await page.setViewportSize(viewport);
        await waitForAppReady(page);

        const opened = await openFirstDetailFromList(page, detailCase.listPath, detailCase.detailPattern);
        if (!opened) {
          test.skip(true, `No ${detailCase.name} available in demo data`);
        }

        // Some pages may have no secondary actions for the current status
        const trigger = page.getByRole('button', { name: /Actions|Menu des actions/i });
        if ((await trigger.count()) === 0) {
          test.skip(true, `No Actions menu on this ${detailCase.name} (status has no secondary actions)`);
        }

        await assertActionsMenuFullyVisible(page, viewport);
      });
    }
  }
});
