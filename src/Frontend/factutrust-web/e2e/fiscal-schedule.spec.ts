import { test, expect } from '@playwright/test';

/**
 * Smoke tests for fiscal schedule UI shell.
 * Authenticated create/save flows require staging credentials and are skipped by default.
 */
test.describe('Echeancier fiscal — public shell', () => {
  test('login page loads', async ({ page }) => {
    await page.goto('/auth/login');
    await expect(page.getByLabel(/email/i)).toBeVisible();
  });
});

test.describe('Echeancier fiscal — authenticated (staging)', () => {
  test.skip(true, 'Requires dedicated accounting test tenant; run manually in staging');

  test('toolbar shows Sage-like actions including Planifier les rappels', async ({ page }) => {
    await page.goto('/accounting/fiscal-schedule');
    await expect(page.getByRole('button', { name: /planifier les rappels/i })).toBeVisible();
    await expect(page.getByRole('link', { name: /aide/i })).toBeVisible();
  });

  test('filters include Appliquer les filtres', async ({ page }) => {
    await page.goto('/accounting/fiscal-schedule');
    await expect(page.getByRole('button', { name: /appliquer les filtres/i })).toBeVisible();
  });

  test('create modal opens with visible form', async ({ page }) => {
    await page.goto('/accounting/fiscal-schedule');
    await page.getByRole('button', { name: /creer/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByLabel(/type d'obligation/i)).toBeVisible();
  });

  test('generate schedule populates rows', async ({ page }) => {
    await page.goto('/accounting/fiscal-schedule');
    await page.getByRole('button', { name: /generer l'echeancier/i }).click();
    await expect(page.getByText(/echeance\(s\) generee/i)).toBeVisible();
  });
});
