import { test, expect } from '@playwright/test';

/**
 * E2E onglet « Configuration sectorielle » du détail tenant (port 4300).
 * Prérequis : backoffice + backend démarrés, session admin valide, un tenant Phase 2 existant.
 * Marqué `test.skip` par défaut : nécessite un `tenantId` réel du jeu de données de test, non
 * disponible de façon stable en environnement de build — à activer manuellement en local avec
 * un tenant seedé (cf. plan WP-F9 : "mark test.skip when backend rules API unavailable").
 */
test.describe('Backoffice — tenant / Configuration sectorielle', () => {
  const TENANT_ID = process.env['E2E_TENANT_ID'];

  test.skip(!TENANT_ID, 'E2E_TENANT_ID non défini — voir README pour renseigner un tenant de test.');

  test.beforeEach(async ({ page }) => {
    await page.goto(`http://localhost:4300/tenants/${TENANT_ID}?tab=sector`);
  });

  test('affiche le secteur/domaine courants et permet une prévisualisation', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Configuration sectorielle actuelle' })).toBeVisible();

    const modifyBtn = page.getByRole('button', { name: 'Modifier' });
    if (!(await modifyBtn.isVisible())) {
      test.skip(true, 'Utilisateur de test sans permission platform.sector-rules:apply.');
      return;
    }
    await modifyBtn.click();
    await page.getByLabel('Nouveau secteur').selectOption({ index: 1 });
    await page.getByRole('button', { name: 'Prévisualiser la reconfiguration' }).click();
    await expect(page.getByText(/Prévisualisation/)).toBeVisible();

    const applyBtn = page.getByRole('button', { name: 'Appliquer la reconfiguration' });
    await expect(applyBtn).toBeDisabled();
    await page.getByRole('checkbox').check();
    await expect(applyBtn).toBeEnabled();
  });
});
