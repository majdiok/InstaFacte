/**
 * E2E — Approve / Dismiss flows on the V2 board.
 * Covers fix F-M7 (mandatory reason for dismissal) and F-M1 (idempotent approve).
 */
import { test, expect, mockV2Backend, ReplenishmentBoardPage } from './replenishment.fixtures';

test.describe('Replenishment V2 — Approve / Dismiss', () => {
  test('approve sends a POST and removes the row from the pending list', async ({ page }) => {
    const tracker = await mockV2Backend(page);
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    await board.clickApprove('FRI-001');
    await expect(page.locator('.p-toast-message-success, .toast-success').first()).toContainText('approuvée');
    await expect(board.rowByCode('FRI-001')).toHaveCount(0);
    expect(tracker.approveCalls).toContain('rec-1');
  });

  test('dismiss opens the reason modal and refuses an empty reason (fix F-M7)', async ({ page }) => {
    const tracker = await mockV2Backend(page);
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    await board.clickDismiss('TAB5552');

    const modal = page.locator('app-dismiss-reason-modal [role="dialog"]');
    await expect(modal).toBeVisible();
    await expect(modal).toHaveAttribute('aria-modal', 'true');

    // Switching to "Other" then clearing the textarea must disable the Confirm button.
    await modal.locator('select').selectOption('Other');
    const confirmBtn = modal.getByRole('button', { name: /Confirmer l'abandon/ });
    await expect(confirmBtn).toBeDisabled();

    // Type a justification — the button enables, click confirms with that reason.
    await modal.locator('textarea').fill('Fournisseur en rupture jusqu\'à mars');
    await expect(confirmBtn).toBeEnabled();
    await confirmBtn.click();

    await expect(modal).toHaveCount(0); // modal closed
    expect(tracker.dismissCalls).toEqual([
      { id: 'rec-2', reason: 'Fournisseur en rupture jusqu\'à mars' }
    ]);
  });

  test('dismiss with a standard code sends the French label as reason', async ({ page }) => {
    const tracker = await mockV2Backend(page);
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    await board.clickDismiss('FRI-001');
    const modal = page.locator('app-dismiss-reason-modal [role="dialog"]');
    await modal.locator('select').selectOption('BudgetExhausted');
    await modal.getByRole('button', { name: /Confirmer l'abandon/ }).click();

    expect(tracker.dismissCalls[0].reason).toBe('Budget épuisé');
  });

  test('Escape closes the dismiss modal without firing the dismiss POST', async ({ page }) => {
    const tracker = await mockV2Backend(page);
    const board = new ReplenishmentBoardPage(page);
    await board.goto();
    if (page.url().includes('/auth/login')) test.skip(true, 'Authentication required');

    await board.clickDismiss('FRI-001');
    await page.keyboard.press('Escape');
    await expect(page.locator('app-dismiss-reason-modal')).toHaveCount(0);
    expect(tracker.dismissCalls).toEqual([]);
  });
});
