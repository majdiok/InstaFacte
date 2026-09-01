import { test, expect, Page } from '@playwright/test';
import { seleniumConfig } from './selenium.config';

/**
 * Route-mocked coverage for plan v1-configuration-dynamique.md §6.2's dedicated scenario:
 * choosing the "btp-construction" segment must show ONLY that segment's matrix domains
 * (Immobilier, Énergie & Environnement, Artisanat, Autre domaine — the exact
 * `SegmentDefinition.AllowedDomainCodes` set for `CompanySegments.BtpConstruction` in
 * `SectorConfigurationCatalog.cs`) with an accurate "N domaines adaptés" banner, and the
 * wizard must still complete registration with the right payload. Same route-mocking
 * convention as `registration-sector-catalog.spec.ts` (mocks `GET /api/public/sector-catalog`
 * and `POST /api/auth/register`) so this runs deterministically without a live backend.
 */

const config = seleniumConfig;

const AppModuleId = {
  Purchases: 6,
  Stock: 7,
  Projects: 16,
  Fiscal: 10,
  CRM: 9
} as const;

function fakeCatalogPayload() {
  return {
    success: true,
    data: {
      segments: [
        { code: 'entreprise', labelFr: 'Entreprise', descriptionFr: '', iconKey: 'briefcase', sortOrder: 0, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] },
        {
          code: 'commerce', labelFr: 'Commerce', descriptionFr: '', iconKey: 'shopping-cart', sortOrder: 1,
          coreModuleIds: [], recommendedModuleIds: [AppModuleId.CRM],
          domainCodes: ['alimentation-agroalimentaire', 'technologie-informatique']
        },
        { code: 'services', labelFr: 'Services', descriptionFr: '', iconKey: 'handshake', sortOrder: 2, coreModuleIds: [], recommendedModuleIds: [], domainCodes: ['technologie-informatique'] },
        {
          // Real matrix for CompanySegments.BtpConstruction (SectorConfigurationCatalog.cs):
          // AllowedDomainCodes = [Immobilier, EnergieEnvironnement, Artisanat, Autre].
          // BaseRecommendedModules = [Purchases, Stock, Projects, Fiscal] — CRM deliberately absent.
          code: 'btp-construction', labelFr: 'BTP & Construction', descriptionFr: '', iconKey: 'hard-hat', sortOrder: 3,
          coreModuleIds: [],
          recommendedModuleIds: [AppModuleId.Purchases, AppModuleId.Stock, AppModuleId.Projects, AppModuleId.Fiscal],
          domainCodes: ['immobilier', 'energie-environnement', 'artisanat']
        },
        { code: 'association', labelFr: 'Association', descriptionFr: '', iconKey: 'heart-handshake', sortOrder: 4, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] },
        { code: 'etablissement-educatif', labelFr: 'Établissement éducatif', descriptionFr: '', iconKey: 'graduation-cap', sortOrder: 5, coreModuleIds: [], recommendedModuleIds: [], domainCodes: [] }
      ],
      domains: [
        { code: 'alimentation-agroalimentaire', labelFr: 'Alimentation & Agroalimentaire', sortOrder: 0, additionalModuleIds: [] },
        { code: 'technologie-informatique', labelFr: 'Technologie & Informatique', sortOrder: 1, additionalModuleIds: [] },
        { code: 'immobilier', labelFr: 'Immobilier', sortOrder: 2, additionalModuleIds: [] },
        { code: 'energie-environnement', labelFr: 'Énergie & Environnement', sortOrder: 3, additionalModuleIds: [] },
        { code: 'artisanat', labelFr: 'Artisanat', sortOrder: 4, additionalModuleIds: [] },
        { code: 'autre', labelFr: 'Autre domaine', sortOrder: 5, additionalModuleIds: [] }
      ],
      modules: [
        { id: AppModuleId.Purchases, code: 'purchases', labelFr: 'Achats', isCore: false },
        { id: AppModuleId.Stock, code: 'stock', labelFr: 'Stock', isCore: false },
        { id: AppModuleId.Projects, code: 'projects', labelFr: 'Projets', isCore: false },
        { id: AppModuleId.Fiscal, code: 'fiscal', labelFr: 'Fiscal', isCore: false },
        { id: AppModuleId.CRM, code: 'crm', labelFr: 'CRM Commercial', isCore: false }
      ],
      moduleDependencies: []
    }
  };
}

async function mockSectorCatalog(page: Page): Promise<void> {
  await page.route('**/api/public/sector-catalog', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(fakeCatalogPayload()) })
  );
}

test.describe('Registration wizard — BTP & Construction segment (route-mocked, plan v1-configuration-dynamique §6.2)', () => {
  test.beforeEach(async ({ page }) => {
    await mockSectorCatalog(page);
    await page.goto('/auth/register');
    await page.waitForSelector('form', { timeout: 15000 });
  });

  test('choosing btp-construction shows only its matrix domains, in catalog order, with the accurate "N domaines adaptés" banner', async ({ page }) => {
    await page.getByRole('radio', { name: /segment : btp \u0026 construction/i }).click();

    // Only BTP's own matrix (Immobilier, Énergie & Environnement, Artisanat, Autre domaine) —
    // none of the OTHER segments' domains (Alimentation & Agroalimentaire, Technologie & Informatique).
    await expect(page.locator('.dom-item')).toHaveCount(4);
    const btpLabels = await page.locator('.dom-item .dom-label').allTextContents();
    expect(btpLabels).toEqual(['Immobilier', 'Énergie & Environnement', 'Artisanat', 'Autre domaine']);
    expect(btpLabels).not.toContain('Alimentation & Agroalimentaire');
    expect(btpLabels).not.toContain('Technologie & Informatique');

    // Banner is shown (4 of 6 catalog domains — genuinely restricted) and names the right segment.
    const chip = page.locator('.adapt-chip');
    await expect(chip).toBeVisible();
    await expect(chip).toHaveText('4 domaines adaptés à votre segment : BTP & Construction');
  });

  test('switching from btp-construction to a segment without its own matrix domain clears the selection', async ({ page }) => {
    await page.getByRole('radio', { name: /segment : btp \u0026 construction/i }).click();
    await page.locator('.dom-item', { hasText: 'Immobilier' }).click();
    await expect(page.locator('.dom-item.selected')).toContainText('Immobilier');

    // 'services' only offers 'technologie-informatique' (+ 'autre') — Immobilier is not among it.
    await page.getByRole('radio', { name: /segment : services/i }).click();
    await expect(page.locator('.domain-notice')).toBeVisible();
    await expect(page.locator('.dom-item.selected')).toHaveCount(0);
  });

  test('completing registration for btp-construction posts the right segment/domain/modules payload', async ({ page }) => {
    const testData = { ...config.testData.validUser, email: `test-${Date.now()}@example.com` };

    let registerPayload: Record<string, unknown> | null = null;
    await page.route('**/api/auth/register', async (route) => {
      registerPayload = route.request().postDataJSON();
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: false, data: null, message: 'route-mocked fixture, not a real account', errors: [] })
      });
    });

    await page.getByRole('radio', { name: /segment : btp \u0026 construction/i }).click();
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
    // CRM is not part of BTP's recommended set, so it surfaces only in "Autres modules"
    // (the optional group) and is NOT pre-enabled — it must stay off unless the user opts in.
    const crmCard = page.locator('.mod-card', { hasText: 'CRM Commercial' });
    await expect(crmCard).toHaveCount(1);
    await expect(crmCard).not.toHaveClass(/\bon\b/);
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

    expect(registerPayload!.companySegment).toBe('btp-construction');
    expect(registerPayload!.businessDomain).toBe('artisanat');
    expect(Array.isArray(registerPayload!.enabledModules)).toBe(true);
    expect(registerPayload!.enabledModules as number[]).not.toContain(AppModuleId.CRM);
  });
});
