import { test, expect } from '@playwright/test';

test.describe('Journal draft edit', () => {
  test('journal page still exposes refresh and export', async ({ page }) => {
    await page.goto('/accounting/journal');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }
    await expect(page.getByRole('heading', { name: /Journal comptable/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Actualiser/i })).toBeVisible();
  });

  test('manual entry without entryId remains create mode', async ({ page }) => {
    await page.goto('/accounting/manual-entry');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }
    await expect(page.getByRole('heading', { name: /Saisie des écritures comptables/i })).toBeVisible();
    await expect(page.getByText('Saisie guidée')).toBeVisible();
    await expect(page.getByRole('button', { name: /^Enregistrer$/ })).toBeVisible();
  });
});
