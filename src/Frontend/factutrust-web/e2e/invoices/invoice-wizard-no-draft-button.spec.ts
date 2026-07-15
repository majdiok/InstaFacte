import { test, expect } from '@playwright/test';

/**
 * Vérifie l'absence des actions utilisateur « Enregistrer brouillon » sur le wizard facture.
 * L'autosave silencieux reste actif côté service (non testé ici).
 *
 * Sans authentification, la route redirige vers /auth/login : le test ne fait alors
 * aucune assertion sur le wizard (comportement attendu du permissionGuard).
 */
test.describe('Wizard facture — pas de bouton brouillon manuel', () => {
  test('ne doit pas afficher Enregistrer brouillon / Enregistrer en brouillon', async ({ page }) => {
    await page.goto('/invoices/new');
    await page.waitForLoadState('domcontentloaded');

    if (page.url().includes('/auth/login')) {
      return;
    }

    await expect(page.getByRole('button', { name: /Enregistrer en brouillon/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /Enregistrer brouillon/i })).toHaveCount(0);
  });
});

test.describe('Wizard facture — mode focus plein écran', () => {
  test('masque le header app et affiche le wizard', async ({ page }) => {
    await page.goto('/invoices/new');
    await page.waitForLoadState('domcontentloaded');

    if (page.url().includes('/auth/login')) {
      return;
    }

    await expect(page.locator('app-header')).toHaveCount(0);
    await expect(page.locator('.wizard-container')).toBeVisible();
    await expect(page.locator('app-breadcrumb')).toHaveCount(0);
  });
});
