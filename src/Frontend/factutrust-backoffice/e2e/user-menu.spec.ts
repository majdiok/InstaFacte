import { test, expect } from '@playwright/test';

/**
 * E2E menu utilisateur backoffice (port 4300).
 * Prérequis : backoffice démarré (`npm start` dans factutrust-backoffice) et session admin valide.
 */
test.describe('Backoffice user menu', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4300/tenants');
  });

  test('opens profile menu and navigates to 2FA page', async ({ page }) => {
    const trigger = page.locator('ft-user-menu .user-btn');
    await trigger.click();

    const menu = page.locator('.ft-user-menu-panel, .p-menu.p-menu-overlay').last();
    await expect(menu).toBeVisible();

    await menu.getByText('Authentification 2FA').click();
    await expect(page).toHaveURL(/\/me\/2fa$/);
  });

  test('logout redirects to login', async ({ page }) => {
    await page.locator('ft-user-menu .user-btn').click();
    const menu = page.locator('.ft-user-menu-panel, .p-menu.p-menu-overlay').last();
    await menu.getByText('Déconnexion').click();
    await expect(page).toHaveURL(/\/login/);
  });
});
