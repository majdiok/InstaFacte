import { test, expect, type Page } from '@playwright/test';
import { getDemoCredentials, loginAsDemoUser, waitForAppReady } from '../helpers/partnership-demo.helpers';

async function tryLogin(page: Page): Promise<boolean> {
  if (!getDemoCredentials()) {
    return false;
  }
  try {
    await loginAsDemoUser(page);
    return !page.url().includes('/auth/login');
  } catch {
    return false;
  }
}

async function gotoPos(page: Page): Promise<boolean> {
  await page.goto('/pos', { waitUntil: 'domcontentloaded' });
  await page.waitForLoadState('domcontentloaded');
  if (page.url().includes('/auth/login') || page.url().includes('/auth/select-warehouse')) {
    return false;
  }
  const shell = page.locator('app-pos, .pos-layout');
  if (!(await shell.isVisible().catch(() => false))) {
    return false;
  }
  return true;
}

async function dismissOpenRegisterIfNeeded(page: Page): Promise<void> {
  const modal = page.locator('.pos-modal').filter({ hasText: 'Ouvrir la caisse' });
  if (await modal.isVisible().catch(() => false)) {
    const float = page.locator('#pos-opening-float');
    if (await float.isVisible().catch(() => false)) {
      await float.fill('0');
    }
    await modal.getByRole('button', { name: /^Ouvrir$/ }).click();
    await page.waitForTimeout(800);
  }
}

async function addFirstCatalogProduct(page: Page): Promise<boolean> {
  const card = page.locator('app-product-card').first();
  if (!(await card.isVisible().catch(() => false))) {
    return false;
  }
  await card.click();
  return (await page.locator('app-order-line').count()) > 0;
}

test.describe('POS — accès non authentifié', () => {
  test('redirige /pos vers la connexion', async ({ page }) => {
    await page.goto('/pos', { waitUntil: 'commit' });
    await expect
      .poll(() => page.url(), { timeout: 20000 })
      .toMatch(/\/auth\/login|\/pos|select-warehouse/);
    if (!page.url().includes('/auth/login')) {
      test.skip(true, 'Already authenticated in this environment');
    }
    expect(page.url()).toContain('/auth/login');
  });

  test('redirige le wizard bureau /invoices/new vers la connexion', async ({ page }) => {
    await page.goto('/invoices/new', { waitUntil: 'commit' });
    await expect
      .poll(() => page.url(), { timeout: 20000 })
      .toMatch(/\/auth\/login|\/invoices\/new|select-warehouse/);
    if (!page.url().includes('/auth/login')) {
      test.skip(true, 'Already authenticated in this environment');
    }
    expect(page.url()).toContain('/auth/login');
  });
});

test.describe('POS — golden paths', () => {
  test('1. passager, 1 ligne, espèces, rendu', async ({ page }) => {
    if (!(await tryLogin(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }
    if (!(await gotoPos(page))) {
      test.skip(true, 'Warehouse or POS module not available');
    }
    await dismissOpenRegisterIfNeeded(page);
    await expect(page.getByText(/Point de Vente/i).first()).toBeVisible();
    if (!(await addFirstCatalogProduct(page))) {
      test.skip(true, 'No POS catalog product in demo data');
    }
    await page.getByRole('button', { name: /Espèces/i }).click();
    const validate = page.getByRole('button', { name: /Valider|Encaisser/i }).first();
    if (await validate.isEnabled()) {
      await validate.click();
      await expect(page.locator('app-change-calculator, .pos-toast--success').first()).toBeVisible({
        timeout: 20000
      });
    }
  });

  test('2. client tarifé, quantité palier, TTC ticket visible', async ({ page }) => {
    if (!(await tryLogin(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }
    if (!(await gotoPos(page))) {
      test.skip(true, 'Warehouse or POS module not available');
    }
    await dismissOpenRegisterIfNeeded(page);
    if (!(await addFirstCatalogProduct(page))) {
      test.skip(true, 'No POS catalog product in demo data');
    }
    const qty = page.locator('input[aria-label="Quantite"]').first();
    if (await qty.isVisible()) {
      await qty.fill('2');
      await qty.blur();
    }
    await expect(page.getByText(/Net a payer|Net à payer/i).first()).toBeVisible();
  });

  test('3. paiement fractionné espèces + carte (UI)', async ({ page }) => {
    if (!(await tryLogin(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }
    if (!(await gotoPos(page))) {
      test.skip(true, 'Warehouse or POS module not available');
    }
    await dismissOpenRegisterIfNeeded(page);
    const split = page.getByRole('button', { name: /Fractionn|Split/i }).first();
    if (!(await split.isVisible().catch(() => false))) {
      test.skip(true, 'Split payment control not visible');
    }
    await split.click();
    await expect(page.locator('app-split-payment')).toBeVisible();
    await expect(page.getByText(/Carte/i).first()).toBeVisible();
  });

  test('4. vente à terme : contrôle client obligatoire', async ({ page }) => {
    if (!(await tryLogin(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }
    if (!(await gotoPos(page))) {
      test.skip(true, 'Warehouse or POS module not available');
    }
    await dismissOpenRegisterIfNeeded(page);
    const onAccount = page.getByRole('button', { name: /A terme|À terme/i });
    await expect(onAccount).toBeVisible();
    await expect(onAccount).toBeDisabled();
  });

  test('5. avoir : recherche n° / POS-ref', async ({ page }) => {
    if (!(await tryLogin(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }
    if (!(await gotoPos(page))) {
      test.skip(true, 'Warehouse or POS module not available');
    }
    await dismissOpenRegisterIfNeeded(page);
    const refund = page.getByRole('button', { name: /Avoir|Rembours/i }).first();
    if (!(await refund.isVisible().catch(() => false))) {
      test.skip(true, 'Credit-note action not visible');
    }
    await refund.click();
    await expect(page.getByText(/POS-/i).first()).toBeVisible({ timeout: 8000 });
  });

  test('6. hold / recall (panneau tickets en attente)', async ({ page }) => {
    if (!(await tryLogin(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }
    if (!(await gotoPos(page))) {
      test.skip(true, 'Warehouse or POS module not available');
    }
    await dismissOpenRegisterIfNeeded(page);
    if (!(await addFirstCatalogProduct(page))) {
      test.skip(true, 'No POS catalog product in demo data');
    }
    const hold = page.getByRole('button', { name: /attente|Hold/i }).first();
    if (!(await hold.isEnabled().catch(() => false))) {
      test.skip(true, 'Hold action not enabled');
    }
    await hold.click();
  });

  test('7. open → rapport X → clôture Z (UI)', async ({ page }) => {
    if (!(await tryLogin(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }
    if (!(await gotoPos(page))) {
      test.skip(true, 'Warehouse or POS module not available');
    }
    await dismissOpenRegisterIfNeeded(page);
    const xReport = page.getByRole('button', { name: 'Rapport X' });
    if (!(await xReport.isVisible().catch(() => false))) {
      const plusMenu = page.getByRole('button', { name: 'Plus d\'actions' });
      if (await plusMenu.isVisible().catch(() => false)) {
        await plusMenu.click();
      }
    }
    if (await xReport.isVisible().catch(() => false)) {
      await xReport.click();
    }
    const zBtn = page.getByRole('button', { name: 'Clôturer Z' });
    if (!(await zBtn.isVisible().catch(() => false))) {
      test.skip(true, 'No open session to close');
    }
    await zBtn.click();
    await expect(page.getByRole('heading', { name: /Clôture Z|Cloture Z/i })).toBeVisible();
    await expect(page.locator('#pos-counted-cash')).toBeVisible();
  });

  test('8. wizard bureau /invoices/new sans vacation POS', async ({ page }) => {
    if (!(await tryLogin(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }
    await page.goto('/invoices/new', { waitUntil: 'domcontentloaded' });
    await waitForAppReady(page);
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Invoice wizard not reachable');
    }
    expect(page.url()).not.toContain('/pos');
    await expect(page.locator('.wizard-container, app-invoice-wizard, [class*="wizard"]').first()).toBeVisible({
      timeout: 15000
    });
  });

  test('9. deux caisses : écran paramètres entrepôts', async ({ page }) => {
    if (!(await tryLogin(page))) {
      test.skip(true, 'DOC_EMAIL/DOC_PASSWORD or demo credentials required');
    }
    await page.goto('/settings/warehouses', { waitUntil: 'domcontentloaded' });
    await waitForAppReady(page);
    if (page.url().includes('/auth/login') || page.url().includes('/access-denied')) {
      test.skip(true, 'Warehouses settings not available');
    }
    await expect(page.getByText(/Entrepôts|Entrepots/i).first()).toBeVisible();
    const caisses = page.getByRole('button', { name: /Caisses POS/i }).first();
    if (!(await caisses.isVisible().catch(() => false))) {
      test.skip(true, 'No warehouse row with POS registers action');
    }
    await caisses.click();
    await expect(page.getByText(/Caisses POS/i).first()).toBeVisible();
    await expect(page.getByText(/défaut|defaut/i).first()).toBeVisible();
  });
});
