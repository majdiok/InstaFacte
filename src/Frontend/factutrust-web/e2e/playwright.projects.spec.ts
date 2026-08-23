import { test, expect } from '@playwright/test';

/**
 * Smoke tests module Projets — nécessite frontend sur :4200.
 * Les scénarios authentifiés sont ignorés si la page de connexion s'affiche.
 */
test.describe('Projets module smoke', () => {
  test('dashboard route responds or redirects to login', async ({ page }) => {
    const response = await page.goto('/projects/dashboard');
    expect(response?.status()).toBeLessThan(500);
    const url = page.url();
    expect(url.includes('/projects/dashboard') || url.includes('/auth/login')).toBeTruthy();
  });

  test('projects list route responds or redirects to login', async ({ page }) => {
    const response = await page.goto('/projects');
    expect(response?.status()).toBeLessThan(500);
    const url = page.url();
    expect(url.includes('/projects') || url.includes('/auth/login')).toBeTruthy();
  });

  test('time entry route responds or redirects to login', async ({ page }) => {
    const response = await page.goto('/projects/time');
    expect(response?.status()).toBeLessThan(500);
    const url = page.url();
    expect(url.includes('/projects/time') || url.includes('/auth/login')).toBeTruthy();
  });
});
