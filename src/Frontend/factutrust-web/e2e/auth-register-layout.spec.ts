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

test.describe('Auth — layout inscription', () => {
  test('register wizard shows sidebar, horizontal stepper and step 1 content', async ({ page }) => {
    await page.goto('/auth/register');

    // The wizard uses its own `.wizard`/`.sidebar`/`.panel-card` layout, not
    // the legacy `AuthShellComponent` `.auth-form-card` wrapper.
    await expect(page.locator('.wizard')).toBeVisible();

    const sidebar = page.locator('.sidebar');
    await expect(sidebar).toBeVisible();
    await expect(sidebar.locator('.sidebar-title')).toContainText(/créez votre espace/i);
    await expect(sidebar.locator('.step-item')).toHaveCount(4);

    const panelCard = page.locator('.panel-card');
    await expect(panelCard).toBeVisible();

    const hstepper = panelCard.locator('.hstepper');
    await expect(hstepper).toBeVisible();
    await expect(hstepper.locator('.hstep')).toHaveCount(4);
    await expect(hstepper.locator('.hstep.active')).toContainText(/type de société/i);

    // Step 1 content: segment cards + domain list, per the wizard's step 1.
    await expect(page.locator('.seg-card')).toHaveCount(6);
    await expect(page.locator('.dom-item')).toHaveCount(10);

    // The button's accessible name comes from its `aria-label` ("Étape suivante"),
    // its visible text is "Continuer".
    await expect(page.getByRole('button', { name: /étape suivante/i })).toContainText(/continuer/i);
  });

  test('register wizard hides the sidebar below the 1080px breakpoint and the horizontal stepper below 768px', async ({
    page,
  }) => {
    await page.goto('/auth/register');

    // Above both breakpoints: sidebar and horizontal stepper are both visible.
    await page.setViewportSize({ width: 1280, height: 900 });
    await expect(page.locator('.sidebar')).toBeVisible();
    await expect(page.locator('.hstepper')).toBeVisible();

    // Between 768px and 1080px: sidebar collapses, horizontal stepper still shown.
    await page.setViewportSize({ width: 1000, height: 900 });
    await expect(page.locator('.sidebar')).toBeHidden();
    await expect(page.locator('.hstepper')).toBeVisible();

    // Below 768px: horizontal stepper collapses too.
    await page.setViewportSize({ width: 600, height: 900 });
    await expect(page.locator('.sidebar')).toBeHidden();
    await expect(page.locator('.hstepper')).toBeHidden();

    // The step content itself remains usable at narrow widths.
    await expect(page.locator('.seg-card').first()).toBeVisible();
  });

  test('register-firm form card is wide without an internal scrollbar', async ({ page }) => {
    await page.goto('/auth/register-firm');

    const card = page.locator('.auth-form-card');
    await expectCardFitsContent(card);
    await expect(page.getByRole('heading', { name: /cabinet comptable/i })).toBeVisible();
  });
});
