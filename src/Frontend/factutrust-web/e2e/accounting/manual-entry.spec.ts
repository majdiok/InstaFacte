import { test, expect } from '@playwright/test';

test.describe('Manual entry screen', () => {
  test('loads manual entry page structure', async ({ page }) => {
    await page.goto('/accounting/manual-entry');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }
    await expect(page.getByRole('heading', { name: /Saisie des écritures comptables/i })).toBeVisible();
    await expect(page.getByText('Saisie standard')).toBeVisible();
    await expect(page.getByText('Saisie guidée')).toBeVisible();
  });

  test('shows neutral balance state on initial load', async ({ page }) => {
    await page.goto('/accounting/manual-entry');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }
    await expect(page.getByText('Saisie standard')).toBeVisible();
    await expect(page.getByText(/Saisissez vos lignes/i)).toBeVisible();
    await expect(page.getByText(/Écart\s*:/i)).not.toBeVisible();
  });

  test('autocomplete panel attaches to body when typing account', async ({ page }) => {
    await page.goto('/accounting/manual-entry');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }
    const accountInput = page.locator('.entry-lines-table input[placeholder="N° compte"]').first();
    await accountInput.waitFor({ state: 'visible', timeout: 15000 });
    await accountInput.fill('4');
    const panel = page.locator('body > .p-autocomplete-panel, body .me-autocomplete-panel').first();
    await expect(panel).toBeVisible({ timeout: 10000 });
  });

  test('toggles TVA column via options menu', async ({ page }) => {
    await page.goto('/accounting/manual-entry');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }
    await page.getByRole('button', { name: "Options d'écriture" }).click();
    const vatCheckbox = page.getByLabel('Colonne TVA (aide à la saisie)');
    await vatCheckbox.uncheck();
    const headers = page.locator('.entry-lines-table th');
    await expect(headers.filter({ hasText: 'TVA' })).toHaveCount(0);
    await page.getByRole('button', { name: "Options d'écriture" }).click();
    await vatCheckbox.check();
    await expect(headers.filter({ hasText: 'TVA' })).toHaveCount(1);
  });

  test('uses accounting amount inputs for debit and credit columns', async ({ page }) => {
    await page.goto('/accounting/manual-entry');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }
    const amountInputs = page.locator('app-accounting-amount-input');
    await expect(amountInputs.first()).toBeVisible({ timeout: 15000 });
    expect(await amountInputs.count()).toBeGreaterThanOrEqual(4);
  });

  test('shows balanced entry message when debits equal credits', async ({ page }) => {
    await page.goto('/accounting/manual-entry');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }

    const debitInput = page.locator('#debit-0 input').first();
    const creditInput = page.locator('#credit-1 input').first();
    await debitInput.waitFor({ state: 'visible', timeout: 15000 });

    await debitInput.fill('100,500');
    await debitInput.blur();
    await creditInput.fill('100,500');
    await creditInput.blur();

    await expect(page.getByText(/Écriture équilibrée/i)).toBeVisible({ timeout: 10000 });
  });

  test('shows imbalance gap when totals differ', async ({ page }) => {
    await page.goto('/accounting/manual-entry');
    if (page.url().includes('/auth/login')) {
      test.skip(true, 'Authentication required');
    }

    const debitInput = page.locator('#debit-0 input').first();
    const creditInput = page.locator('#credit-1 input').first();
    await debitInput.waitFor({ state: 'visible', timeout: 15000 });

    await debitInput.fill('100');
    await debitInput.blur();
    await creditInput.fill('50');
    await creditInput.blur();

    await expect(page.getByText(/Écart/i)).toBeVisible({ timeout: 10000 });
  });
});
