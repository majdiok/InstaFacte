import { test, expect, Locator } from '@playwright/test';

async function expectCardFitsContent(card: Locator): Promise<void> {
  await expect(card).toBeVisible();
  await expect(card).toHaveClass(/auth-form-card--wide/);
  await expect(card).not.toHaveClass(/auth-form-card--scrollable/);

  const { scrollHeight, clientHeight } = await card.evaluate((el) => ({
    scrollHeight: el.scrollHeight,
    clientHeight: el.clientHeight,
  }));
  expect(scrollHeight).toBeLessThanOrEqual(clientHeight + 1);
}

test.describe('Auth — layout carte inscription', () => {
  test('register form card shows step 1 without an internal scrollbar', async ({ page }) => {
    await page.goto('/auth/register');

    const card = page.locator('.auth-form-card');
    await expectCardFitsContent(card);

    await expect(page.getByRole('heading', { name: /créer votre compte/i })).toBeVisible();
    await expect(page.locator('#firstName')).toBeVisible();
    await expect(page.locator('#email')).toBeVisible();
    await expect(page.locator('p-password[formcontrolname="password"] input')).toBeVisible();
    await expect(page.getByRole('button', { name: /étape suivante/i })).toBeVisible();
    await expect(page.getByRole('link', { name: /conditions d'utilisation/i })).toBeVisible();
  });

  test('register-firm form card is wide without an internal scrollbar', async ({ page }) => {
    await page.goto('/auth/register-firm');

    const card = page.locator('.auth-form-card');
    await expectCardFitsContent(card);
    await expect(page.getByRole('heading', { name: /cabinet comptable/i })).toBeVisible();
  });
});
