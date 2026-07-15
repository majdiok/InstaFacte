/**
 * E2E — Replenishment V2 board: listing, filters, sorting, urgency badges.
 *
 * Requires authentication. The spec relies on a logged-in session being available
 * in a global setup hook (TODO: hook a real auth.setup.ts; for now, mark these tests
 * `.skip()` automatically when the redirect to `/auth/login` happens).
 */
import { test, expect, mockV2Backend, ReplenishmentBoardPage } from './replenishment.fixtures';

test.describe('Replenishment V2 — listing & filters', () => {
  test.beforeEach(async ({ page }) => {
    await mockV2Backend(page);
  });

  test('loads the V2 board and displays all recommendations', async ({ page }) => {
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    await expect(page.locator('#repl-table')).toBeVisible();
    await expect(page.locator('tbody tr')).toHaveCount(4);
    await expect(board.rowByCode('FRI-001')).toContainText('Farine T55');
    await expect(board.rowByCode('FRI-001')).toContainText('Alpha Distribution');
  });

  test('shows the KPI bar with the 5 tiles', async ({ page }) => {
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    const kpiBar = page.locator('app-replenishment-kpi-bar');
    await expect(kpiBar).toBeVisible();
    await expect(kpiBar.locator('.kpi-tile')).toHaveCount(5);
    await expect(kpiBar).toContainText('11');     // pendingCount
    await expect(kpiBar).toContainText('97,50 %'); // serviceLevelPercent (fr-FR)
  });

  test('renders urgency badges on rows according to status', async ({ page }) => {
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    const ruptureRow = board.rowByCode('FRI-001');
    await expect(ruptureRow).toHaveClass(/row-rupture/);

    const urgentRow = board.rowByCode('TAB5552');
    await expect(urgentRow).toHaveClass(/row-urgent/);
    await expect(urgentRow.locator('.urg-badge.urg-urgent')).toBeVisible();

    const normalRow = board.rowByCode('PORTE001');
    await expect(normalRow).not.toHaveClass(/row-(rupture|urgent)/);
  });

  test('flags rows without a preferred supplier with an inline warning', async ({ page }) => {
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    // PORTE001 has preferredSupplierId = null in the fixture dataset.
    await expect(board.rowByCode('PORTE001').locator('.supplier-missing')).toContainText('Non renseigné');
  });

  test('honours aria-sort when a column header is toggled', async ({ page }) => {
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    const ropHeader = page.locator('th .th-sort').filter({ hasText: 'Stock / ROP' });
    await ropHeader.click();
    // After the click the header's parent gains aria-sort=descending (default desc).
    await expect(ropHeader.locator('xpath=ancestor::th')).toHaveAttribute('aria-sort', 'descending');

    await ropHeader.click();
    await expect(ropHeader.locator('xpath=ancestor::th')).toHaveAttribute('aria-sort', 'ascending');
  });

  test('persists filter selection to localStorage', async ({ page }) => {
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    // Switch the status filter to "approved".
    await page.locator('app-replenishment-filters select').first().selectOption('approved');
    const persisted = await page.evaluate(() => localStorage.getItem('forecasting-repl-v2-filters'));
    expect(persisted).not.toBeNull();
    expect(JSON.parse(persisted!).status).toBe('approved');
  });
});
