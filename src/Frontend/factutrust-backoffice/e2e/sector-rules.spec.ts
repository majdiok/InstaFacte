import { test, expect } from '@playwright/test';

/**
 * E2E page « Règles sectorielles » (port 4300).
 * Prérequis : backoffice démarré (`npm start`) + backend disponible + session admin valide
 * disposant de la permission `platform.sector-rules:read`.
 *
 * Comme `e2e/user-menu.spec.ts`, ce fichier n'est pas inclus dans tsconfig.app.json/spec.json
 * et ne participe donc pas à `ng build`/`ng test` — Playwright n'est pas encore une dépendance
 * du projet (voir README de la Phase 2 pour l'installation).
 */
test.describe('Backoffice — Règles sectorielles', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4300/sector-rules');
  });

  test('affiche les 7 onglets et la bannière de source des règles', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Règles sectorielles' })).toBeVisible();
    await expect(page.locator('.source-banner')).toContainText(/version \d+/);

    for (const label of ['Segments', 'Domaines', 'Associations', 'Règles de modules', 'Dépendances', 'Paramètres', 'Modèles de données']) {
      await expect(page.getByRole('tab', { name: new RegExp(label) })).toBeVisible();
    }
  });

  test('resynchronise depuis le catalogue avec confirmation par mot-clé', async ({ page }) => {
    const resyncBtn = page.getByRole('button', { name: 'Resynchroniser depuis le catalogue' });
    if (!(await resyncBtn.isVisible())) {
      test.skip(true, 'Utilisateur de test sans rôle PlatformAdmin — bouton masqué par design.');
      return;
    }
    await resyncBtn.click();
    await page.getByRole('textbox').fill('RESYNC');
    await page.getByRole('button', { name: 'Resynchroniser', exact: true }).click();
    await expect(page.getByText('Resynchronisation terminée')).toBeVisible();
  });

  test('la dépendance circulaire est rejetée avec le message backend', async ({ page }) => {
    await page.getByRole('tab', { name: 'Dépendances' }).click();
    // NB: nécessite une paire de modules déjà en dépendance inverse dans les données de test —
    // marqué best-effort, à ajuster selon le jeu de données seedé.
    test.skip(true, 'Nécessite un jeu de données de test avec une dépendance existante A→B pour tenter B→A.');
  });
});
