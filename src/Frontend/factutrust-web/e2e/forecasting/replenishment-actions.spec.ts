/**
 * E2E — secondary V2 actions: override quantity/supplier, undo, decision history, CSV export.
 */
import {
  test, expect, mockV2Backend, ReplenishmentBoardPage
} from './replenishment.fixtures';

test.describe('Replenishment V2 — Override / Undo / History / Export', () => {
  test('override modal sends manualQty + manualSupplierId to the backend', async ({ page }) => {
    const tracker = await mockV2Backend(page);
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    await board.rowByCode('FRI-001').getByRole('button', { name: /Modifier la quantité/ }).click();
    const modal = page.locator('app-override-quantity-modal [role="dialog"]');
    await expect(modal).toBeVisible();

    await modal.locator('input[type="number"]').fill('250');
    await modal.locator('select').selectOption('sup-B');
    await modal.getByRole('button', { name: 'Enregistrer' }).click();

    expect(tracker.overrideCalls.length).toBe(1);
    expect(tracker.overrideCalls[0]).toEqual({
      id: 'rec-1', manualQty: 250, manualSupplierId: 'sup-B'
    });
  });

  test('undo triggers POST /undo and replaces the row with the new status', async ({ page }) => {
    // Pre-set a recommendation to Approved so the "Undo" icon is rendered.
    const tracker = await mockV2Backend(page, {
      dataset: () => [{
        id: 'rec-99', productId: 'p-99', productCode: 'UND-1', productName: 'Stylo',
        warehouseId: 'wh-1', warehouseName: 'Magasin01',
        generatedAt: new Date().toISOString(),
        currentStockOnHand: 5, recommendedQty: 50, rop: 20, safetyStock: 10,
        leadTimeDays: 7, dailyDemand: 5, reasonCodes: [],
        status: 'approved', linkedPurchaseOrderId: null, processedAt: null, productUnit: 'Pièce',
        preferredSupplierId: 'sup-A', preferredSupplierName: 'Alpha',
        quantityOnOrder: 0, effectiveQty: 5, manualQtyOverride: null, manualSupplierOverride: null,
        daysOfStockRemaining: 1, userNotes: null, urgencyLevel: 'Urgent'
      }]
    });

    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    // Switch the filter to "Approved" so the row is visible.
    await page.locator('app-replenishment-filters select').first().selectOption('approved');
    await page.waitForTimeout(200);

    await board.rowByCode('UND-1').getByRole('button', { name: /Annuler la décision/ }).click();
    await expect(page.locator('.p-toast-message-success, .toast-success').first()).toContainText('Annulée');
    expect(tracker.undoCalls).toContain('rec-99');
  });

  test('history modal opens with the decision timeline', async ({ page }) => {
    const tracker = await mockV2Backend(page, {
      history: (id) => ([
        {
          id: 'h1', recommendationId: id,
          fromStatus: 'pending', toStatus: 'approved', actionType: 'Approve',
          reason: null, actorUserId: 'admin', actedAt: '2026-05-10T09:00:00Z',
          payloadJson: null
        },
        {
          id: 'h2', recommendationId: id,
          fromStatus: 'approved', toStatus: 'ordered', actionType: 'LinkPO',
          reason: null, actorUserId: 'admin', actedAt: '2026-05-10T09:05:00Z',
          payloadJson: '{"poNumber":"BC-2026-000001"}'
        }
      ])
    });

    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    await board.rowByCode('FRI-001').getByRole('button', { name: /historique/i }).click();
    const modal = page.locator('app-decision-history-modal [role="dialog"]');
    await expect(modal).toBeVisible();
    await expect(modal.locator('.entry')).toHaveCount(2);
    await expect(modal).toContainText('Approuvée');
    await expect(modal).toContainText('BC créé');
    expect(tracker.historyCalls).toContain('rec-1');
  });

  test('clicking "Exporter CSV" downloads a CSV file via the export service', async ({ page }) => {
    await mockV2Backend(page);
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: /Exporter CSV/ }).click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/\.csv$/);
  });

  test('skip-link becomes visible on focus and jumps to the table', async ({ page }) => {
    await mockV2Backend(page);
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    const skipLink = page.locator('.skip-link');
    await skipLink.focus();
    await expect(skipLink).toBeVisible();

    await skipLink.click();
    await expect(page.locator('#repl-table')).toBeFocused();
  });
});
