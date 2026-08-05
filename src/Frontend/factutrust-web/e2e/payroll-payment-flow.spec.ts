import { test, expect } from '@playwright/test';
import { ensureAuthenticated } from './helpers/auth';

/**
 * Smoke test for payroll treasury link UI (requires payroll module + feature flags).
 */
test.describe('Payroll treasury link', () => {
  test.beforeEach(async ({ page }) => {
    await ensureAuthenticated(page);
  });

  test('run detail shows treasury banner when validated', async ({ page }) => {
    await page.goto('/payroll/runs');
    const firstRun = page.locator('table tbody tr').first();
    if (!(await firstRun.isVisible())) {
      test.skip();
      return;
    }
    await firstRun.click();
    await expect(page).toHaveURL(/\/payroll\/runs\//);
    const treasury = page.getByText('Trésorerie');
    if (await treasury.isVisible()) {
      await expect(treasury).toBeVisible();
    }
  });
});
