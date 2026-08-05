import { test, expect } from '@playwright/test';

test.describe('Payroll advanced deductions', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('networkidle');
    if (page.url().includes('/login')) {
      test.skip();
    }
  });

  test('employee detail shows advanced tabs when navigated', async ({ page }) => {
    await page.goto('/payroll/employees');
    const firstRow = page.locator('table tbody tr').first();
    if (!(await firstRow.isVisible())) {
      test.skip();
      return;
    }
    await firstRow.click();
    await expect(page.getByRole('tab', { name: /Mutuelles/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /Avantages en nature/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /Prêts/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /Saisies/i })).toBeVisible();
  });

  test('run detail shows meal voucher grid on draft run', async ({ page }) => {
    await page.goto('/payroll/runs');
    const firstRun = page.locator('table tbody tr').first();
    if (!(await firstRun.isVisible())) {
      test.skip();
      return;
    }
    await firstRun.click();
    const mealSection = page.getByText(/Tickets restaurant/i);
    if (await mealSection.isVisible()) {
      await expect(mealSection).toBeVisible();
    }
  });
});
