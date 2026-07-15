/**
 * E2E — Fix F-C3 verification: triggering "Créer une recommandation" from the
 * product-demand modal must call the backend with the product's id so that only
 * THIS product is regenerated, not the whole tenant.
 */
import { test, expect, mockV2Backend, ReplenishmentBoardPage } from './replenishment.fixtures';

test.describe('Replenishment V2 — product-scoped regeneration (fix F-C3)', () => {
  test('clicking the product name opens the demand modal', async ({ page }) => {
    await mockV2Backend(page);

    // Stub the product-demand forecast endpoint used by the modal.
    await page.route(/\/api\/forecasting\/product-demand\/.+/, async (route) => {
      await route.fulfill({
        json: {
          success: true,
          data: {
            productId: 'p-1', productCode: 'FRI-001', productName: 'Farine T55',
            horizon: 'month',
            periodStart: '2026-05-12', periodEnd: '2026-06-11',
            expectedQty: 30, lowQty: 20, highQty: 40,
            currentStockOnHand: 0,
            suggestedReplenishmentQty: 30,
            daysOfStockRemaining: 0,
            confidencePercent: 90,
            methodUsed: 'sma'
          }
        }
      });
    });

    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    await board.rowByCode('FRI-001').locator('.name-btn').click();

    const modal = page.locator('app-product-demand-modal');
    await expect(modal).toBeVisible();
    await expect(modal).toContainText('Farine T55');
  });

  test('triggering regeneration from the modal passes the productId (fix F-C3)', async ({ page }) => {
    const tracker = await mockV2Backend(page);

    await page.route(/\/api\/forecasting\/product-demand\/.+/, async (route) => {
      await route.fulfill({
        json: {
          success: true,
          data: {
            productId: 'p-1', productCode: 'FRI-001', productName: 'Farine T55',
            horizon: 'month',
            periodStart: '2026-05-12', periodEnd: '2026-06-11',
            expectedQty: 30, lowQty: 20, highQty: 40,
            currentStockOnHand: 0,
            suggestedReplenishmentQty: 30,
            daysOfStockRemaining: 0,
            confidencePercent: 90,
            methodUsed: 'sma'
          }
        }
      });
    });

    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    await board.rowByCode('FRI-001').locator('.name-btn').click();
    const modal = page.locator('app-product-demand-modal');
    await expect(modal).toBeVisible();

    await modal.getByRole('button', { name: /Créer une recommandation/ }).click();

    // Before the fix, the call was `POST /replenishment/generate` with NO productId — every
    // stock-managed product was regenerated. After the fix, productId is in the query string.
    expect(tracker.generateCalls.length).toBeGreaterThanOrEqual(1);
    const last = tracker.generateCalls[tracker.generateCalls.length - 1];
    expect(last.productId).toBe('p-1');
  });

  test('toast message references the product name (UX hint)', async ({ page }) => {
    await mockV2Backend(page);
    await page.route(/\/api\/forecasting\/product-demand\/.+/, async (route) => {
      await route.fulfill({
        json: {
          success: true,
          data: {
            productId: 'p-1', productCode: 'FRI-001', productName: 'Farine T55',
            horizon: 'month',
            periodStart: '2026-05-12', periodEnd: '2026-06-11',
            expectedQty: 30, lowQty: 20, highQty: 40,
            currentStockOnHand: 0,
            suggestedReplenishmentQty: 30,
            daysOfStockRemaining: 0,
            confidencePercent: 90,
            methodUsed: 'sma'
          }
        }
      });
    });

    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    await board.rowByCode('FRI-001').locator('.name-btn').click();
    await page.locator('app-product-demand-modal').getByRole('button', { name: /Créer une recommandation/ }).click();

    await expect(page.locator('.p-toast-message-success, .toast-success').first()).toContainText('Farine T55');
  });
});
