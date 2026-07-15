import { test, expect } from '@playwright/test';

/**
 * End-to-end smoke for accounting firm workflow.
 * Requires Features:AccountingFirms:Enabled and a running API for authenticated flows.
 */
test.describe('Cabinet comptable — workflow', () => {
  test('register firm page is reachable', async ({ page }) => {
    await page.goto('/auth/register-firm');
    await expect(page.getByRole('heading', { name: /cabinet comptable/i })).toBeVisible();
  });

  test('login page loads for firm users', async ({ page }) => {
    await page.goto('/auth/login');
    await expect(page.getByLabel(/email/i)).toBeVisible();
  });
});

test.describe('Cabinet comptable — authenticated (staging)', () => {
  test.skip(true, 'Requires dedicated test tenants; run manually in staging');

  test('firm manager sees firm sidemenu without Ventes', async ({ page }) => {
    // Login as FirmManager test account, assert sidemenu labels
  });

  test('firm dashboard shows no access denied modal after login', async ({ page }) => {
    // Login as FirmManager → /firm/dashboard
    // await expect(page.getByRole('dialog', { name: /accès refusé/i })).not.toBeVisible();
  });

  test('return to cabinet from ledger without 403 modal', async ({ page }) => {
    // 1. Login as FirmManager → open Ste Bouzgarou dossier
    // 2. Navigate to /accounting/ledger
    // 3. Click header or sidebar "Retour au cabinet"
    // 4. await expect(page.getByRole('dialog', { name: /accès refusé/i })).not.toBeVisible();
    // 5. await expect(page).toHaveURL(/\/firm\/dashboard/);
    // 6. await expect(page.getByRole('navigation', { name: /navigation principale/i })).toContainText('Mes dossiers clients');
    // 7. Repeat with "Changer de dossier" → /firm/clients, no 403 modal
  });

  test('firm opens vat-declaration without 403 modal', async ({ page }) => {
    // Login as FirmManager → open client dossier → /accounting/vat-declaration
    // await expect(page.getByRole('dialog', { name: /accès refusé/i })).not.toBeVisible();
    // await expect(page.getByRole('heading', { name: /déclaration mensuelle/i })).toBeVisible();
    // await expect(page.getByText(/total à payer/i)).toBeVisible();
  });
});
