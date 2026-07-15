import { test, expect } from '@playwright/test';

test.describe('Caisse de trésorerie (routing)', () => {
  test('doit rediriger vers /auth/login si l’utilisateur n’est pas authentifié', async ({ page }) => {
    await page.goto('/payments/cash-desk');
    await page.waitForURL(/\/auth\/login/, { timeout: 10000 });

    await expect(page.locator('h1')).toHaveText(/Bienvenue/i);
    expect(page.url()).toContain('/auth/login');
  });
});

