import { test, expect } from '@playwright/test';

test.describe('Auth — mot de passe oublié', () => {
  test('forgot-password page loads and shows email field', async ({ page }) => {
    await page.goto('/auth/forgot-password');
    await expect(page.getByRole('heading', { name: /mot de passe oublié/i })).toBeVisible();
    await expect(page.locator('input[formcontrolname="email"]')).toBeVisible();
  });

  test('login page links to forgot-password', async ({ page }) => {
    await page.goto('/auth/login');
    await page.getByRole('link', { name: /mot de passe oublié/i }).click();
    await expect(page).toHaveURL(/\/auth\/forgot-password/);
  });

  test('forgot-password submit shows success message', async ({ page }) => {
    await page.goto('/auth/forgot-password');
    await page.fill('input[formcontrolname="email"]', 'test@example.com');
    await page.getByRole('button', { name: /envoyer le lien/i }).click();
    await expect(page.getByText(/si un compte existe/i)).toBeVisible({ timeout: 15000 });
  });

  test('reset-password without token shows warning', async ({ page }) => {
    await page.goto('/auth/reset-password');
    await expect(page.getByText(/lien de réinitialisation est invalide/i)).toBeVisible();
  });

  test('login page still has email and password fields', async ({ page }) => {
    await page.goto('/auth/login');
    await expect(page.locator('input[formcontrolname="email"]')).toBeVisible();
    await expect(page.locator('p-password[formcontrolname="password"] input')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
  });

  test('login form card shows all fields without an internal scrollbar', async ({ page }) => {
    await page.goto('/auth/login');

    const card = page.locator('.auth-form-card');
    await expect(card).toBeVisible();
    await expect(page.locator('input[formcontrolname="email"]')).toBeVisible();
    await expect(page.locator('p-password[formcontrolname="password"] input')).toBeVisible();
    await expect(page.getByRole('link', { name: /mot de passe oublié/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /se connecter/i })).toBeVisible();
    await expect(page.getByText('Vos données sont protégées')).toBeVisible();
    await expect(page.getByRole('link', { name: /créer un compte gratuitement/i })).toBeVisible();

    const { scrollHeight, clientHeight } = await card.evaluate((el) => ({
      scrollHeight: el.scrollHeight,
      clientHeight: el.clientHeight,
    }));
    expect(scrollHeight).toBeLessThanOrEqual(clientHeight + 1);
  });
});
