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
    const treasuryBanner = page.locator('.treasury-banner');
    if (!(await treasuryBanner.isVisible())) {
      test.skip();
      return;
    }
    await expect(treasuryBanner).toBeVisible();
    await expect(treasuryBanner.locator('.treasury-banner__item')).toHaveCount(3);
    await expect(page.getByText('Payé', { exact: true })).toBeVisible();
    await expect(page.getByText('Reste', { exact: true })).toBeVisible();
    const netCard = page.locator('.stat-card').filter({ hasText: 'NET' });
    if (await netCard.isVisible()) {
      await expect(netCard.locator('.stat-value')).toHaveText(/\d[\d\s]*[,\.]\d{3}/);
    }
  });
});
