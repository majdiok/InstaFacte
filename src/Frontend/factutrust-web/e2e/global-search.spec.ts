import { test, expect } from '@playwright/test';
import { getDemoCredentials, loginAsDemoUser, waitForAppReady } from './helpers/partnership-demo.helpers';

async function ensureAuthenticated(page: import('@playwright/test').Page): Promise<boolean> {
  const creds = getDemoCredentials();
  if (!creds) {
    return false;
  }
  await loginAsDemoUser(page);
  return true;
}

test.describe('Recherche globale', () => {
  test('Ctrl+K ouvre la palette modale', async ({ page }) => {
    if (!(await ensureAuthenticated(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }

    await waitForAppReady(page);
    await page.keyboard.press('Control+K');
    await expect(page.locator('.global-search__palette')).toBeVisible();
    await expect(page.locator('.global-search__input--palette')).toBeFocused();
  });

  test('recherche "factures" affiche la page Factures', async ({ page }) => {
    if (!(await ensureAuthenticated(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }

    await waitForAppReady(page);
    await page.keyboard.press('Control+K');
    const input = page.locator('.global-search__input--palette');
    await input.fill('factures');
    await expect(page.locator('.global-search__item-title', { hasText: /facture/i }).first()).toBeVisible();
    await page.locator('.global-search__item').filter({ hasText: /facture/i }).first().click();
    await page.waitForURL(/\/invoices/);
    expect(page.url()).toContain('/invoices');
  });

  test('Escape ferme la palette', async ({ page }) => {
    if (!(await ensureAuthenticated(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }

    await waitForAppReady(page);
    await page.keyboard.press('Control+K');
    await expect(page.locator('.global-search__palette')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.locator('.global-search__palette')).toHaveCount(0);
  });

  test('navigation clavier Enter sélectionne un résultat', async ({ page }) => {
    if (!(await ensureAuthenticated(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }

    await waitForAppReady(page);
    await page.keyboard.press('Control+K');
    await page.locator('.global-search__input--palette').fill('factures');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await page.waitForURL(/\/invoices/);
    expect(page.url()).toContain('/invoices');
  });

  test('redirige vers login si non authentifié', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForURL(/\/auth\/login/, { timeout: 10000 });
    expect(page.url()).toContain('/auth/login');
  });

  test('dropdown inline ne chevauche pas la navigation secondaire', async ({ page }) => {
    if (!(await ensureAuthenticated(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }

    await page.setViewportSize({ width: 1280, height: 800 });
    await waitForAppReady(page);
    await page.goto('/invoices');

    const secondaryNav = page.locator('app-secondary-nav .secondary-nav');
    await expect(secondaryNav).toBeVisible();

    const headerInput = page.locator('.global-search__input--header');
    await expect(headerInput).toBeVisible();
    await headerInput.focus();

    const dropdown = page.locator('body > .global-search__dropdown, .global-search__dropdown').first();
    await expect(dropdown).toBeVisible();

    const dropBox = await dropdown.boundingBox();
    const navBox = await secondaryNav.boundingBox();
    expect(dropBox).toBeTruthy();
    expect(navBox).toBeTruthy();
    expect(dropBox!.y).toBeGreaterThanOrEqual(navBox!.y + navBox!.height - 4);

    await page.keyboard.press('Escape');
    await expect(page.locator('.global-search__dropdown')).toHaveCount(0);
  });
});
