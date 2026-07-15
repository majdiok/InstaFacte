import {
  buildChartSegments,
  buildCompanyFromAuth,
  buildDocLinks,
  buildTaxRows,
  canSubmitDeclaration,
  clampExtrasValue,
  computeTotalToPay,
  hasSuggestionMismatch,
  resolveCompanyDisplay,
  round3,
  shiftPeriod,
  statusLabel,
  suggestedTaxAmount
} from './vat-declaration.view-model';
import { User } from '@core/services/auth.service';

describe('vat-declaration.view-model', () => {
  const baseDto = {
    year: 2026,
    month: 5,
    collectedVat19: 657.02,
    collectedVat13: 67.6,
    collectedVat7: 601.65,
    deductibleVatGoods: 0,
    deductibleVatAssets: 0,
    previousCredit: 0,
    vatDue: 1326.27,
    creditToCarry: 0,
    currency: 'TND',
    status: 0,
    fodec: 125.73,
    droitTimbre: 7,
    tcl: 27.799,
    tfp: 0,
    foprolos: 0,
    withholdingTax: 0,
    acomptes: 0,
    totalToPay: 1486.799,
    version: 1,
    isRectificative: false,
    monthlyDeclarationV2Enabled: true,
    filingDeadline: '2026-06-22',
    declarationTypeDisplay: 'Déclaration mensuelle unique',
    collectedVatBreakdown: [
      { ratePercent: 19, taxableBase: 3458, vatAmount: 657.02 },
      { ratePercent: 13, taxableBase: 520, vatAmount: 67.6 },
      { ratePercent: 7, taxableBase: 8595, vatAmount: 601.65 }
    ],
    deductiblePurchasesTaxableBase: 0,
    salesTaxableBase: 12573,
    salesGrossBase: 13899.27,
    fodecTaxableBase: 8000
  };

  const extras = {
    fodec: 125.73,
    droitTimbre: 7,
    tcl: 27.799,
    tfp: 0,
    foprolos: 0,
    withholdingTax: 0,
    acomptes: 0
  };

  it('computeTotalToPay matches backend formula', () => {
    expect(computeTotalToPay(baseDto, extras)).toBeCloseTo(1486.799, 3);
  });

  it('computeTotalToPay floors at zero', () => {
    expect(computeTotalToPay({ ...baseDto, vatDue: 0 }, { ...extras, acomptes: 999 })).toBe(0);
  });

  it('clampExtrasValue rejects negatives', () => {
    expect(clampExtrasValue(-5)).toBe(0);
    expect(clampExtrasValue(12.5)).toBe(12.5);
  });

  it('shiftPeriod navigates across year boundary', () => {
    expect(shiftPeriod(2026, 1, -1)).toEqual({ year: 2025, month: 12 });
    expect(shiftPeriod(2026, 12, 1)).toEqual({ year: 2027, month: 1 });
  });

  it('buildTaxRows includes V2 taxes when enabled', () => {
    const rows = buildTaxRows(baseDto, extras, 1486.799);
    expect(rows.some(r => r.taxLabel === 'FODEC')).toBe(true);
    expect(rows.find(r => r.taxLabel === 'TOTAL À PAYER')?.netAmount).toBeCloseTo(1486.799, 3);
  });

  it('round3 arrondit au millime', () => {
    expect(round3(565.2999)).toBe(565.3);
    expect(round3(133.04142)).toBe(133.041);
  });

  it('suggestedTaxAmount calcule Base × Taux et renvoie null si donnée absente', () => {
    // FODEC 1 % sur base HT, TCL 0,2 % sur base TTC.
    expect(suggestedTaxAmount(56530, 1)).toBe(565.3);
    expect(suggestedTaxAmount(66520.7, 0.2)).toBe(133.041);
    expect(suggestedTaxAmount(null, 1)).toBeNull();
    expect(suggestedTaxAmount(1000, null)).toBeNull();
  });

  it('hasSuggestionMismatch détecte un écart > 0,001 entre saisi et suggéré', () => {
    expect(hasSuggestionMismatch(292.6, 565.3)).toBe(true);
    expect(hasSuggestionMismatch(565.3, 565.3)).toBe(false);
    expect(hasSuggestionMismatch(null, 565.3)).toBe(false);
  });

  it('buildTaxRows renseigne la suggestion FODEC/TCL depuis les taux configurés', () => {
    const rows = buildTaxRows(baseDto, extras, 1486.799, 1, 0.2);
    const fodec = rows.find(r => r.taxLabel === 'FODEC');
    const tcl = rows.find(r => r.taxLabel === 'TCL');
    expect(fodec?.suggestedAmount).toBeCloseTo(round3((baseDto.fodecTaxableBase ?? baseDto.salesTaxableBase) * 0.01), 3);
    expect(fodec?.taxableBase).toBe(baseDto.fodecTaxableBase);
    expect(tcl?.suggestedAmount).toBeCloseTo(round3(baseDto.salesGrossBase * 0.002), 3);
  });

  it('buildTaxRows ajoute une ligne "Crédit de TVA à reporter" quand creditToCarry > 0', () => {
    const withCredit = { ...baseDto, vatDue: 0, creditToCarry: 340.5 };
    const rows = buildTaxRows(withCredit, extras, 0);
    const credit = rows.find(r => r.taxLabel === 'Crédit de TVA à reporter');
    expect(credit).toBeTruthy();
    expect(credit?.deductibleAmount).toBe(340.5);
    // Absente quand il n'y a pas de crédit.
    expect(buildTaxRows(baseDto, extras, 1486.799).some(r => r.taxLabel === 'Crédit de TVA à reporter')).toBe(false);
  });

  it('buildChartSegments filters zero values', () => {
    const segments = buildChartSegments(baseDto, extras, 1486.799);
    expect(segments.every(s => s.value > 0)).toBe(true);
    expect(segments.some(s => s.label === 'TVA due')).toBe(true);
  });

  it('statusLabel maps known statuses', () => {
    expect(statusLabel(0)).toBe('Brouillon');
    expect(statusLabel(1)).toBe('Soumise');
    expect(statusLabel(2)).toBe('Verrouillée');
  });

  it('resolveCompanyDisplay prefers DTO over company and auth', () => {
    const dto = {
      ...baseDto,
      companyName: 'DTO Société',
      nif: '111/A/B/C/000',
      taxRegimeDisplay: 'Réel',
      tradeName: 'Commerce'
    };
    const company = {
      companyName: 'Settings Société',
      nif: '222/A/B/C/000',
      taxRegimeDisplay: 'Forfaitaire',
      tradeName: 'Autre'
    } as never;
    const auth = { contextCompanyName: 'Auth Société', companyName: 'Native' } as User;

    const resolved = resolveCompanyDisplay(dto, company, auth);
    expect(resolved.companyName).toBe('DTO Société');
    expect(resolved.nif).toBe('111/A/B/C/000');
    expect(resolved.taxRegimeDisplay).toBe('Réel');
    expect(resolved.tradeName).toBe('Commerce');
  });

  it('resolveCompanyDisplay falls back to auth when DTO and company are absent', () => {
    const auth = { contextCompanyName: 'Client Dossier', companyName: 'Cabinet' } as User;
    const resolved = resolveCompanyDisplay(null, null, auth);
    expect(resolved.companyName).toBe('Client Dossier');
    expect(resolved.nif).toBeNull();
  });

  it('buildCompanyFromAuth uses contextCompanyName for delegated cabinet', () => {
    const auth = { contextCompanyName: 'Ste Bouzgarou', companyName: 'Cabinet ABC' } as User;
    const display = buildCompanyFromAuth(auth);
    expect(display.companyName).toBe('Ste Bouzgarou');
    expect(display.nif).toBeNull();
  });

  it('canSubmitDeclaration is true only for a draft (status 0)', () => {
    expect(canSubmitDeclaration(0)).toBeTrue();
    expect(canSubmitDeclaration(1)).toBeFalse(); // soumise
    expect(canSubmitDeclaration(2)).toBeFalse(); // verrouillée
  });

  it('buildDocLinks includes the fiscal schedule link with period query params', () => {
    const links = buildDocLinks(baseDto as never, 2026, 7);
    const schedule = links.find(l => l.route === '/accounting/fiscal-schedule');

    expect(schedule).toBeTruthy();
    expect(schedule!.queryParams).toEqual({ fiscalYear: '2026', periodMonth: '7' });
    // Déclaration en brouillon (status 0) → suivi neutre ; soumise (status 1) → « Déposée ».
    expect(schedule!.statusLabel).toBe('À suivre');
    const submitted = buildDocLinks({ ...baseDto, status: 1 } as never, 2026, 7)
      .find(l => l.route === '/accounting/fiscal-schedule');
    expect(submitted!.statusLabel).toBe('Déposée');
    expect(submitted!.status).toBe('ok');
  });
});
