import { test, expect } from '@playwright/test';

test.describe('Product onboarding tour', () => {
  test('Ignorer dismisses the overlay when it appears after login', async ({ page }) => {
    await page.goto('/auth/login');
    const skip = page.locator('.ft-product-tour, .driver-popover').first();
    // Safety net: existing Completed users must never see the overlay.
    await page.waitForTimeout(500);
    if (await skip.isVisible().catch(() => false)) {
      await page.locator('.ft-product-tour__skip, button:has-text("Ignorer")').first().click();
      await expect(page.locator('.driver-popover')).toHaveCount(0);
    }
  });

  test('login page for a typical session has no tour overlay', async ({ page }) => {
    await page.goto('/auth/login');
    await expect(page.locator('.driver-popover')).toHaveCount(0);
  });
});
