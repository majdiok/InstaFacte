import { test, expect } from '@playwright/test';

/**
 * Contrôle d'intégrité — dashboard audit (route /accounting/health).
 * Nécessite un tenant avec AccountingAuditDashboardEnabled et données comptables.
 */
test.describe('Accounting audit control dashboard', () => {
  test.beforeEach(async ({ page }) => {
    // Utilise le même flux d'auth que les autres specs comptabilité cabinet
    await page.goto('/login');
  });

  test('health route loads audit dashboard or legacy fallback', async ({ page }) => {
    test.skip(true, 'À exécuter avec session cabinet authentifiée (cf. accounting-firm-workflow.spec.ts)');
    await page.goto('/accounting/health');
    await expect(
      page.getByRole('heading', { name: /Contrôle d'intégrité/i })
    ).toBeVisible({ timeout: 15000 });
  });
});
