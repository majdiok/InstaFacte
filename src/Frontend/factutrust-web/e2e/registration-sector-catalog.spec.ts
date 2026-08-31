import { test, expect, Page } from '@playwright/test';
import { seleniumConfig } from './selenium.config';

/**
 * Route-mocked coverage for the Phase 2 sector-rules registration wizard
 * (plan v1-erp-phase2-sector-rules.md, WP-F1…WP-F5). Unlike
 * `playwright.registration.spec.ts` (real backend, may `test.skip()` when it is
 * down), every test here mocks `GET /api/public/sector-catalog` and
 * `POST /api/auth/register` so the suite runs deterministically without a live
 * `.NET` backend / SQL Server.
 */

const config = seleniumConfig;

const AppModuleId = {
  Purchases: 6,
  Stock: 7,
  CRM: 9,
  Forecasting: 12
} as const;

function fakeCatalogPayload() {
  return {
    success: true,
    data: {
      segments: [
        { code: 'entreprise', labelFr: 'Entreprise', descriptionFr: '', iconKey: 'briefcase', sortOrder: 0, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] },
        {
          // Forecasting is deliberately NOT recommended here (only Purchases is): it must stay
          // an optional Step 3 toggle so the "enabling it auto-enables its Stock dependency" test
          // below observes a real transition instead of both modules already being on by default
          // (recommendedModules() self-closes dependencies, so a recommended Forecasting would
          // pull in Stock from the start).
          code: 'commerce', labelFr: 'Commerce', descriptionFr: '', iconKey: 'shopping-cart', sortOrder: 1,
          coreModuleIds: [], recommendedModuleIds: [AppModuleId.Purchases],
          domainCodes: ['alimentation-agroalimentaire', 'artisanat']
        },
        {
          code: 'services', labelFr: 'Services', descriptionFr: '', iconKey: 'handshake', sortOrder: 2,
          coreModuleIds: [], recommendedModuleIds: [AppModuleId.CRM], domainCodes: ['technologie-informatique']
        },
        { code: 'btp-construction', labelFr: 'BTP & Construction', descriptionFr: '', iconKey: 'hard-hat', sortOrder: 3, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] },
        { code: 'association', labelFr: 'Association', descriptionFr: '', iconKey: 'heart-handshake', sortOrder: 4, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] },
        { code: 'etablissement-educatif', labelFr: 'Établissement éducatif', descriptionFr: '', iconKey: 'graduation-cap', sortOrder: 5, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] }
      ],
      domains: [
        { code: 'alimentation-agroalimentaire', labelFr: 'Alimentation & Agroalimentaire', sortOrder: 0, additionalModuleIds: [] },
        { code: 'artisanat', labelFr: 'Artisanat', sortOrder: 1, additionalModuleIds: [] },
        { code: 'technologie-informatique', labelFr: 'Technologie & Informatique', sortOrder: 2, additionalModuleIds: [] },
        { code: 'autre', labelFr: 'Autre domaine', sortOrder: 3, additionalModuleIds: [] }
      ],
      modules: [
        { id: AppModuleId.Purchases, code: 'purchases', labelFr: 'Achats', isCore: false },
        { id: AppModuleId.Stock, code: 'stock', labelFr: 'Stock', isCore: false },
        { id: AppModuleId.CRM, code: 'crm', labelFr: 'CRM Commercial', isCore: false },
        { id: AppModuleId.Forecasting, code: 'forecasting', labelFr: 'Prévisions IA', isCore: false }
      ],
      moduleDependencies: [
        { moduleId: AppModuleId.Forecasting, requiredModuleId: AppModuleId.Stock }
      ]
    }
  };
}

async function mockSectorCatalog(page: Page): Promise<void> {
  await page.route('**/api/public/sector-catalog', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(fakeCatalogPayload()) })
  );
}

test.describe('Registration wizard — sector catalog (route-mocked, plan WP-F5)', () => {
  test.beforeEach(async ({ page }) => {
    await mockSectorCatalog(page);
    await page.goto('/auth/register');
    await page.waitForSelector('form', { timeout: 15000 });
  });

  test('domain list is filtered per segment, in catalog order, with "autre" appended', async ({ page }) => {
    await page.getByRole('radio', { name: /segment : commerce/i }).click();
    await expect(page.locator('.dom-item')).toHaveCount(3);
    const commerceLabels = await page.locator('.dom-item .dom-label').allTextContents();
    expect(commerceLabels).toEqual(['Alimentation & Agroalimentaire', 'Artisanat', 'Autre domaine']);

    await page.getByRole('radio', { name: /segment : services/i }).click();
    await expect(page.locator('.dom-item')).toHaveCount(2);
    const servicesLabels = await page.locator('.dom-item .dom-label').allTextContents();
    expect(servicesLabels).toEqual(['Technologie & Informatique', 'Autre domaine']);
  });

  test('switching segment clears a domain that is no longer offered and shows the notice', async ({ page }) => {
    await page.getByRole('radio', { name: /segment : commerce/i }).click();
    await page.locator('.dom-item', { hasText: 'Artisanat' }).click();
    await expect(page.locator('.dom-item.selected')).toContainText('Artisanat');
    await expect(page.locator('.domain-notice')).toHaveCount(0);

    // 'services' does not offer 'artisanat' (its only domainCodes entry is
    // 'technologie-informatique') — the previously selected domain must be cleared.
    await page.getByRole('radio', { name: /segment : services/i }).click();
    await expect(page.locator('.domain-notice')).toBeVisible();
    await expect(page.locator('.dom-item.selected')).toHaveCount(0);
  });

  test('enabling a module with a hard dependency in Step 3 auto-enables the dependency and shows the hint', async ({ page }) => {
    const testData = { ...config.testData.validUser, email: `test-${Date.now()}@example.com` };

    await page.getByRole('radio', { name: /segment : commerce/i }).click();
    await page.locator('.dom-item', { hasText: 'Artisanat' }).click();
    await page.locator('button.btn-next:has-text("Continuer")').click();

    await page.waitForSelector('#wiz-firstName', { timeout: 5000 });
    await page.fill('#wiz-firstName', testData.firstName);
    await page.fill('#wiz-lastName', testData.lastName);
    await page.fill('#wiz-email', testData.email);
    await page.locator('p-password[formcontrolname="password"] input').fill(testData.password);
    await page.locator('p-password[formcontrolname="confirmPassword"] input').fill(testData.confirmPassword);
    await page.fill('#wiz-companyName', testData.companyName);
    await page.locator('p-inputmask[formcontrolname="nif"] input').fill(testData.nif);
    await page.locator('p-select[formcontrolname="taxRegime"]').click();
    await page.locator('text=Régime réel').click();
    await page.fill('#wiz-companyEmail', testData.companyEmail);
    await page.locator('p-inputmask[formcontrolname="phone"] input').fill(testData.phone);
    await page.locator('button.btn-next:has-text("Continuer")').click();

    // Purchases is recommended (no dependency); Forecasting is optional here and
    // requires Stock, which is neither core nor recommended for 'commerce' in this
    // mock — enabling it must auto-enable Stock.
    await page.waitForSelector('.mod-group', { timeout: 5000 });
    await expect(page.locator('.mod-card', { hasText: 'Stock' })).not.toHaveClass(/\bon\b/);

    await page.locator('.mod-card', { hasText: 'Prévisions IA' }).locator('p-inputswitch').click();

    const stockCard = page.locator('.mod-card', { hasText: 'Stock' }).first();
    await expect(stockCard).toHaveClass(/\bon\b/);
    await expect(stockCard.locator('.dep-hint')).toBeVisible();

    // Trying to switch Stock back off is a no-op while Forecasting still requires it.
    await stockCard.locator('p-inputswitch').click();
    await expect(stockCard).toHaveClass(/\bon\b/);
  });

  test('final submit still posts companySegment/businessDomain/enabledModules', async ({ page }) => {
    const testData = { ...config.testData.validUser, email: `test-${Date.now()}@example.com` };

    let registerPayload: Record<string, unknown> | null = null;
    await page.route('**/api/auth/register', async (route) => {
      registerPayload = route.request().postDataJSON();
      // Fulfill with a definite (non-success) response: this test only asserts the
      // outgoing payload, not the post-success navigation flow (real backend only).
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: false, data: null, message: 'route-mocked fixture, not a real account', errors: [] })
      });
    });

    await page.getByRole('radio', { name: /segment : commerce/i }).click();
    await page.locator('.dom-item', { hasText: 'Artisanat' }).click();
    await page.locator('button.btn-next:has-text("Continuer")').click();

    await page.waitForSelector('#wiz-firstName', { timeout: 5000 });
    await page.fill('#wiz-firstName', testData.firstName);
    await page.fill('#wiz-lastName', testData.lastName);
    await page.fill('#wiz-email', testData.email);
    await page.locator('p-password[formcontrolname="password"] input').fill(testData.password);
    await page.locator('p-password[formcontrolname="confirmPassword"] input').fill(testData.confirmPassword);
    await page.fill('#wiz-companyName', testData.companyName);
    await page.locator('p-inputmask[formcontrolname="nif"] input').fill(testData.nif);
    await page.locator('p-select[formcontrolname="taxRegime"]').click();
    await page.locator('text=Régime réel').click();
    await page.fill('#wiz-companyEmail', testData.companyEmail);
    await page.locator('p-inputmask[formcontrolname="phone"] input').fill(testData.phone);
    await page.locator('button.btn-next:has-text("Continuer")').click();

    await page.waitForSelector('.mod-group', { timeout: 5000 });
    await page.locator('button.btn-next:has-text("Continuer")').click();

    await page.waitForSelector('#wiz-street', { timeout: 5000 });
    await page.fill('#wiz-street', testData.street);
    await page.fill('#wiz-streetLine2', testData.streetLine2);
    await page.fill('#wiz-city', testData.city);
    await page.locator('p-inputmask[formcontrolname="postalCode"] input').fill(testData.postalCode);
    await page.locator('p-select[formcontrolname="governorate"]').click();
    await page.locator(`text=${testData.governorate}`).click();
    await page.locator('label[for="wiz-acceptTerms"]').click();

    await page.locator('button.btn-submit:has-text("Créer mon espace")').click();
    await expect.poll(() => registerPayload).not.toBeNull();

    expect(registerPayload!.companySegment).toBe('commerce');
    expect(registerPayload!.businessDomain).toBe('artisanat');
    expect(Array.isArray(registerPayload!.enabledModules)).toBe(true);
    expect((registerPayload!.enabledModules as number[]).length).toBeGreaterThan(0);
  });
});
