import { test, expect } from '@playwright/test';

test.describe('Fixed assets amortization Sage report', () => {
  test('screen shows Sage report sections when authenticated', async ({ page }) => {
    test.skip(true, 'Requires authenticated E2E session in CI/local.');
    await page.goto('/accounting/fixed-assets/amortization-table');
    await expect(page.getByRole('heading', { name: /Tableau des amortissements/i })).toBeVisible();
    await expect(page.getByText(/TABLEAU DES AMORTISSEMENTS/i)).toBeVisible();
    await expect(page.getByText(/RÉCAPITULATIF PAR NATURE/i)).toBeVisible();
    await expect(page.getByText(/INFORMATIONS/i)).toBeVisible();
  });
});
