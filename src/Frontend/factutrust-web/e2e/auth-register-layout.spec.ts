import { test, expect, Locator, Page } from '@playwright/test';

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

/**
 * Route-mocks `GET /api/public/sector-catalog` (plan WP-F1/WP-F5) so this spec is
 * deterministic regardless of whether the real backend has the sector-rules DB
 * feature on. `status: 404` reproduces the frontend kill-switch/off case (silent
 * static fallback — the original Phase 1 assertions below stay byte-identical).
 */
async function mockSectorCatalog(page: Page, status: 404): Promise<void>;
async function mockSectorCatalog(page: Page, body: unknown): Promise<void>;
async function mockSectorCatalog(page: Page, statusOrBody: 404 | unknown): Promise<void> {
  await page.route('**/api/public/sector-catalog', (route) => {
    if (statusOrBody === 404) {
      return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(statusOrBody) });
  });
}

test.describe('Auth — layout inscription', () => {
  test('register wizard shows sidebar, horizontal stepper and step 1 content (static fallback)', async ({ page }) => {
    await mockSectorCatalog(page, 404);
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
    // The 404 mock forces the static fallback catalog (plan WP-F1 kill-switch
    // semantics): the full 10-domain list shows regardless of segment, exactly
    // as in Phase 1 — original assertions preserved.
    await expect(page.locator('.seg-card')).toHaveCount(6);
    await expect(page.locator('.dom-item')).toHaveCount(10);

    // The button's accessible name comes from its `aria-label` ("Étape suivante"),
    // its visible text is "Continuer".
    await expect(page.getByRole('button', { name: /étape suivante/i })).toContainText(/continuer/i);
  });

  test('register wizard filters the domain list to the selected segment when the sector catalog is remote', async ({ page }) => {
    await mockSectorCatalog(page, {
      success: true,
      data: {
        segments: [
          { code: 'entreprise', labelFr: 'Entreprise', descriptionFr: '', iconKey: 'briefcase', sortOrder: 0, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] },
          { code: 'commerce', labelFr: 'Commerce', descriptionFr: '', iconKey: 'shopping-cart', sortOrder: 1, coreModuleIds: [], recommendedModuleIds: [], domainCodes: ['alimentation-agroalimentaire', 'artisanat'] },
          { code: 'services', labelFr: 'Services', descriptionFr: '', iconKey: 'handshake', sortOrder: 2, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] },
          { code: 'btp-construction', labelFr: 'BTP & Construction', descriptionFr: '', iconKey: 'hard-hat', sortOrder: 3, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] },
          { code: 'association', labelFr: 'Association', descriptionFr: '', iconKey: 'heart-handshake', sortOrder: 4, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] },
          { code: 'etablissement-educatif', labelFr: 'Établissement éducatif', descriptionFr: '', iconKey: 'graduation-cap', sortOrder: 5, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] }
        ],
        domains: [
          { code: 'alimentation-agroalimentaire', labelFr: 'Alimentation & Agroalimentaire', sortOrder: 0, additionalModuleIds: [] },
          { code: 'artisanat', labelFr: 'Artisanat', sortOrder: 1, additionalModuleIds: [] },
          { code: 'autre', labelFr: 'Autre domaine', sortOrder: 2, additionalModuleIds: [] }
        ],
        modules: [],
        moduleDependencies: []
      }
    });
    await page.goto('/auth/register');

    await expect(page.locator('.seg-card')).toHaveCount(6);

    // Before any segment is picked, the remote catalog can't tell which domains
    // apply — the disabled placeholder shows instead of an (empty/misleading) grid.
    await expect(page.locator('.dom-grid--placeholder')).toBeVisible();
    await expect(page.locator('.dom-item')).toHaveCount(0);

    await page.getByRole('radio', { name: /segment : commerce/i }).click();

    // Commerce's two domainCodes plus the always-appended "autre" option.
    await expect(page.locator('.dom-item')).toHaveCount(3);
    await expect(page.locator('.dom-item')).toContainText(['Alimentation & Agroalimentaire', 'Artisanat', 'Autre domaine']);
  });

  test('register wizard hides the sidebar below the 1080px breakpoint and the horizontal stepper below 768px', async ({
    page,
  }) => {
    await mockSectorCatalog(page, 404);
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
