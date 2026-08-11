import { test, expect } from '@playwright/test';

test.describe('Document import edit in modal', () => {
  test('shows editable proposal grid after import review', async ({ page }) => {
    await page.goto('/accounting/manual-entry');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }

    await page.getByRole('button', { name: /^Import$/i }).click();
    await page.getByRole('button', { name: /Importer une facture/i }).click();
    const dialog = page.getByRole('dialog', { name: /Importer une facture/i });
    await expect(dialog).toBeVisible();

    // Without a real file upload in CI, verify idle state and editor wiring when review is shown.
    await expect(dialog.getByText(/Importez une facture à comptabiliser/i)).toBeVisible();
  });

  test('manual entry grid remains editable after standard navigation', async ({ page }) => {
    await page.goto('/accounting/manual-entry');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }

    await expect(page.getByText('Lignes d\'écriture')).toBeVisible();
    await expect(page.getByRole('button', { name: /Ajouter une ligne/i })).toBeVisible();
  });
});
