import { computeCompanyProfileCompletion } from './company-profile-completion.helpers';
import { Company, CompanyAddress } from '@core/services/company.service';

function makeCompany(
  overrides: Partial<Company> = {},
  addressOverrides: Partial<CompanyAddress> = {}
): Company {
  const address: CompanyAddress = {
    street: 'Rue Test',
    streetLine2: null,
    city: 'Tunis',
    postalCode: null,
    governorate: 'Tunis',
    country: 'Tunisie',
    fullAddress: 'Rue Test, Tunis',
    ...addressOverrides
  };
  return {
    id: '1',
    companyName: 'Test Co',
    tradeName: null,
    nif: '1234567/A/B/C/000',
    commerceRegistry: null,
    taxRegime: 0,
    taxRegimeDisplay: 'Régime réel',
    address,
    email: 'test@test.com',
    phone: '22222222',
    website: null,
    logoUrl: null,
    bankName: null,
    rib: null,
    iban: null,
    invoicePrefix: null,
    defaultPaymentTerms: null,
    invoiceFooter: null,
    warehouseName: null,
    cnssEmployerNumber: null,
    clientPortalEnabled: true,
    ...overrides
  };
}

describe('computeCompanyProfileCompletion (plan §3.5)', () => {
  it('returns 0 when no optional fields are filled', () => {
    expect(computeCompanyProfileCompletion(makeCompany())).toBe(0);
  });

  it('returns 100 when all optional fields are filled', () => {
    const company = makeCompany(
      {
        logoUrl: 'https://example.com/logo.png',
        rib: '1234567890',
        iban: 'TN5912345678901234567',
        bankName: 'Banque Test',
        website: 'https://example.com',
        tradeName: 'Test Co Trading',
        commerceRegistry: 'RC123456',
        cnssEmployerNumber: '1234567',
        invoiceFooter: 'Merci de votre confiance'
      },
      { streetLine2: 'Étage 2', postalCode: '1004' }
    );
    expect(computeCompanyProfileCompletion(company)).toBe(100);
  });

  it('returns a partial percentage when some fields are filled', () => {
    // 2 of 11 fields filled => ~18%
    const company = makeCompany({
      logoUrl: 'https://example.com/logo.png',
      rib: '1234567890'
    });
    const pct = computeCompanyProfileCompletion(company);
    expect(pct).toBeGreaterThan(0);
    expect(pct).toBeLessThan(100);
    expect(pct).toBe(Math.round((2 / 11) * 100));
  });

  it('treats whitespace-only strings as unfilled', () => {
    const company = makeCompany({
      rib: '   ',
      bankName: '  '
    });
    expect(computeCompanyProfileCompletion(company)).toBe(0);
  });

  it('returns 0 for null/undefined company', () => {
    expect(computeCompanyProfileCompletion(null)).toBe(0);
    expect(computeCompanyProfileCompletion(undefined)).toBe(0);
  });

  it('increments for address sub-fields (streetLine2, postalCode)', () => {
    const company = makeCompany({}, { streetLine2: 'Appartement 3', postalCode: '1004' });
    expect(computeCompanyProfileCompletion(company)).toBe(Math.round((2 / 11) * 100));
  });
});
