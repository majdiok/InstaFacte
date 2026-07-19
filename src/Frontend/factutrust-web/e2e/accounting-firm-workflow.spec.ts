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

/**
 * Parcours invitation → acceptation/refus (refonte 2026-07). Nécessite deux
 * comptes (admin société + FirmManager cabinet) et Features:AccountingFirms.
 * Documenté ici pour exécution manuelle en staging tant que les tenants de
 * test dédiés ne sont pas provisionnés.
 */
test.describe('Cabinet comptable — invitation & acceptation (staging)', () => {
  test.skip(true, 'Requires two dedicated test tenants (company + firm); run manually in staging');

  test('company sends a request with rich autocomplete, no dropdown overlap', async ({ page }) => {
    // 1. Login admin société → /settings/accounting-firm
    // 2. await expect(page.getByRole('heading', { name: /cabinet comptable/i })).toBeVisible();
    // 3. Type ≥2 chars in the autocomplete; the panel (appendTo body) lists
    //    name + city/governorate and does NOT overlap "Envoyer la demande".
    // 4. Select a firm, fill the optional notes, click "Envoyer la demande".
    // 5. Confirm in the confirmation dialog.
    // 6. await expect(page.getByText(/en attente d'acceptation/i)).toBeVisible();
    // 7. await expect(page.getByRole('button', { name: /annuler la demande/i })).toBeVisible();
  });

  test('company can cancel a pending request', async ({ page }) => {
    // On /settings/accounting-firm with a pending request:
    // click "Annuler la demande" → confirm → the search card reappears.
  });

  test('firm rejects with a reason; company sees it and can re-request', async ({ page }) => {
    // 1. Login FirmManager → /firm/invitations
    // 2. Click "Refuser" → dialog → type a reason → confirm.
    // 3. Sidebar "Invitations" badge decreases WITHOUT a page reload.
    // 4. Login admin société → /settings/accounting-firm shows the rejection
    //    banner with the reason and the search card is available again.
  });

  test('firm accepts; dossier appears and badge clears; company sees active link', async ({ page }) => {
    // 1. FirmManager → /firm/invitations → "Accepter" → confirm → toast.
    // 2. /firm/clients lists the new dossier; sidebar badge back to 0.
    // 3. Company → /settings/accounting-firm shows "Liaison active" (not "Active").
  });

  test('bell shows an in-app notification after each firm decision', async ({ page }) => {
    // After accept/reject, the recipient's header bell badge increments and the
    // dropdown lists the notification with a working navigation link.
  });
});
