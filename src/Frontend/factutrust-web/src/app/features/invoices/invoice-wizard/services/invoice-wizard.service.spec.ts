import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of, Subject } from 'rxjs';
import { InvoiceWizardService, PLAN_QUOTA_ERROR_CODES } from './invoice-wizard.service';
import { ClientTaxType, InvoiceType, InvoiceWizardState, TunisianVatRate } from '../models/invoice-wizard.models';
import { computeWizardTotalsCheck } from './invoice-wizard-calculation.utils';
import { environment } from '@environments/environment';
import { SubscriptionInfo, SubscriptionService } from '@core/services/subscription.service';

const VALID_PRODUCT_ID = '550e8400-e29b-41d4-a716-446655440001';
const VALID_PRODUCT_ID_2 = '550e8400-e29b-41d4-a716-446655440002';
const VALID_CLIENT_ID = '550e8400-e29b-41d4-a716-446655440000';
const VALID_NIF = '1234567/A/B/C/000';

function setupMinimalValidWizardState(svc: InvoiceWizardService): void {
  svc.selectSeller({
    id: '550e8400-e29b-41d4-a716-446655440010',
    companyName: 'Test Seller',
    tradeName: null,
    address: {
      street: '1 rue Test',
      streetLine2: null,
      postalCode: null,
      city: 'Tunis',
      governorate: 'Tunis',
      country: 'Tunisie'
    },
    nif: VALID_NIF,
    commerceRegistry: null,
    vatCode: null,
    logo: null,
    phone: null,
    email: 'seller@test.com'
  });

  svc.selectClient({
    id: VALID_CLIENT_ID,
    isNewClient: false,
    name: 'Test Client',
    taxType: ClientTaxType.NonTaxSubject,
    address: {
      street: '2 rue Client',
      streetLine2: null,
      postalCode: null,
      city: 'Tunis',
      governorate: 'Tunis',
      country: 'Tunisie'
    },
    nif: null,
    email: 'client@test.com',
    phone: null,
    contactPerson: null
  });

  svc.updateMetadata({ invoiceNumber: 'FAC-2026-000001' });
}

function addValidLinkedLine(svc: InvoiceWizardService): void {
  svc.addLine({
    productId: VALID_PRODUCT_ID,
    designation: 'Tableau',
    quantity: 2,
    unitPriceHT: 25,
    vatRate: TunisianVatRate.Standard
  });
}

function addUnlinkedLine(svc: InvoiceWizardService): void {
  svc.addLine({
    productId: null,
    designation: '',
    quantity: 1,
    unitPriceHT: 0,
    vatRate: TunisianVatRate.Standard
  });
}

function createDefaultSubscription(overrides?: Partial<SubscriptionInfo>): SubscriptionInfo {
  return {
    id: 'sub-1',
    plan: 'Free',
    planDisplay: 'Gratuit',
    status: 'Active',
    statusDisplay: 'Actif',
    startDate: '2026-01-01',
    endDate: null,
    trialEndDate: null,
    monthlyPrice: null,
    annualPrice: null,
    currency: 'TND',
    invoicesThisMonth: 3,
    currentPeriodStart: '2026-07-01',
    usage: {
      invoices: { label: 'Factures ce mois', used: 3, limit: 10, isUnlimited: false },
      quotes: { label: 'Devis ce mois', used: 0, limit: 10, isUnlimited: false },
      clients: { label: 'Clients', used: 0, limit: 20, isUnlimited: false },
      products: { label: 'Produits', used: 0, limit: 50, isUnlimited: false },
      storage: { label: 'Stockage', used: 0, limit: 100, isUnlimited: false }
    },
    ...overrides
  };
}

function provideSubscriptionMock(
  info: SubscriptionInfo | ((forceRefresh?: boolean) => SubscriptionInfo) = createDefaultSubscription()
) {
  return {
    provide: SubscriptionService,
    useValue: {
      getCurrentSubscription: (forceRefresh?: boolean) => of({
        success: true,
        data: typeof info === 'function' ? info(forceRefresh) : info,
        message: null,
        errors: []
      }),
      subscriptionChanged$: new Subject<void>()
    }
  };
}

const defaultWizardProviders = [
  InvoiceWizardService,
  provideHttpClient(),
  provideHttpClientTesting(),
  provideSubscriptionMock()
];

describe('InvoiceWizardService client normalization', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: defaultWizardProviders
    });
  });

  it('constructs without throwing and exposes default FODEC rate in totals', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    expect(svc.totals().fodecRatePercent).toBe(1);
    expect(svc.totals().totalFodec).toBe(0);
  });

  it('calculateLineAmounts with FODEC applies canonical formula', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    svc.addLine({
      productId: VALID_PRODUCT_ID,
      designation: 'FODEC item',
      quantity: 10,
      unitPriceHT: 100,
      vatRate: TunisianVatRate.Standard,
      isFodecApplicable: true
    });

    const line = svc.lines()[0];
    expect(line.fodecAmount).toBe(10);
    expect(line.vatAmount).toBe(191.9);
    expect(line.totalTTC).toBe(1201.9);
    expect(svc.totals().totalFodec).toBe(10);
    expect(svc.totals().totalVat).toBe(191.9);
  });

  it('updateLine clears FODEC on custom line when isFodecApplicable set to false', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    svc.addLine({
      productId: null,
      designation: 'Prestation libre',
      quantity: 1,
      unitPriceHT: 1000,
      vatRate: TunisianVatRate.Standard,
      isFodecApplicable: true
    });
    expect(svc.lines()[0].fodecAmount).toBe(10);

    svc.updateLine(svc.lines()[0].id, { isFodecApplicable: false });
    expect(svc.lines()[0].isFodecApplicable).toBeFalse();
    expect(svc.lines()[0].fodecAmount).toBe(0);
    expect(svc.totals().totalFodec).toBe(0);
  });

  it('addLine normalizes corrupted designation objects to product name string', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    svc.addLine({
      productId: VALID_PRODUCT_ID,
      designation: { name: 'Refrigirateur beko', id: VALID_PRODUCT_ID } as unknown as string,
      quantity: 1,
      unitPriceHT: 900,
      vatRate: TunisianVatRate.Standard
    });

    expect(svc.lines()[0].designation).toBe('Refrigirateur beko');
    expect(typeof svc.lines()[0].designation).toBe('string');
  });

  it('updateLine normalizes corrupted designation objects to product name string', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    addValidLinkedLine(svc);
    const lineId = svc.lines()[0].id;

    svc.updateLine(lineId, {
      designation: { name: 'Tableau mis à jour', id: VALID_PRODUCT_ID } as unknown as string
    });

    expect(svc.lines()[0].designation).toBe('Tableau mis à jour');
    expect(typeof svc.lines()[0].designation).toBe('string');
  });

  it('recalculates document totals with FODEC on mixed VAT lines (600 HT → 713.050 TTC)', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    svc.addLine({
      productId: VALID_PRODUCT_ID,
      designation: 'Television',
      quantity: 1,
      unitPriceHT: 450,
      vatRate: TunisianVatRate.Standard,
      isFodecApplicable: true
    });
    svc.addLine({
      productId: VALID_PRODUCT_ID_2,
      designation: 'bureau',
      quantity: 1,
      unitPriceHT: 150,
      vatRate: TunisianVatRate.Intermediate,
      isFodecApplicable: true
    });

    expect(svc.totals().totalHT).toBe(600);
    expect(svc.totals().totalFodec).toBe(6);
    expect(svc.totals().totalVat).toBeCloseTo(106.05, 3);
    expect(svc.totals().fiscalStampAmount).toBe(1);
    expect(svc.totals().totalTTC).toBeCloseTo(713.05, 3);

    const vat19 = svc.totals().vatBreakdown.find(v => v.rate === TunisianVatRate.Standard);
    const vat13 = svc.totals().vatBreakdown.find(v => v.rate === TunisianVatRate.Intermediate);
    expect(vat19?.baseAmount).toBeCloseTo(454.5, 3);
    expect(vat19?.vatAmount).toBeCloseTo(86.355, 3);
    expect(vat13?.baseAmount).toBeCloseTo(151.5, 3);
    expect(vat13?.vatAmount).toBeCloseTo(19.695, 3);

    expect(computeWizardTotalsCheck({
      lines: svc.lines(),
      totals: svc.totals(),
      metadata: svc.metadata()
    } as InvoiceWizardState).isValid).toBeTrue();
  });

  it('applies FODEC only on product-linked lines in mixed invoice', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    svc.addLine({
      productId: VALID_PRODUCT_ID,
      designation: 'Produit FODEC',
      quantity: 1,
      unitPriceHT: 100,
      vatRate: TunisianVatRate.Standard,
      isFodecApplicable: true
    });
    svc.addLine({
      productId: null,
      designation: 'Prestation libre',
      quantity: 1,
      unitPriceHT: 100,
      vatRate: TunisianVatRate.Standard,
      isFodecApplicable: false
    });

    expect(svc.totals().totalFodec).toBe(1);
    expect(svc.totals().totalHT).toBe(200);
  });

  it('credit note signs FODEC and fiscal stamp negatively', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    svc.updateMetadata({ type: InvoiceType.CreditNote });
    svc.addLine({
      productId: VALID_PRODUCT_ID,
      designation: 'Retour',
      quantity: 1,
      unitPriceHT: 600,
      vatRate: TunisianVatRate.Standard,
      isFodecApplicable: true
    });

    expect(svc.totals().totalFodec).toBe(-6);
    expect(svc.totals().fiscalStampAmount).toBe(-1);
    expect(svc.totals().totalTTC).toBeLessThan(0);
  });

  it('selectClient normalizes PascalCase Id and sets isNewClient false when id resolves', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    const raw = {
      id: null,
      isNewClient: false,
      name: 'BelHedi',
      taxType: ClientTaxType.NonTaxSubject,
      address: {
        street: '5400',
        streetLine2: null,
        postalCode: null,
        city: 'Sousse',
        governorate: 'Jendouba',
        country: 'Tunisie'
      },
      nif: null,
      email: 'a@b.c',
      phone: null,
      contactPerson: null,
      Id: '550e8400-e29b-41d4-a716-446655440000'
    } as any;

    svc.selectClient(raw);

    const c = svc.client();
    expect(c?.id).toBe('550e8400-e29b-41d4-a716-446655440000');
    expect(c?.isNewClient).toBe(false);
  });

  it('selectClient preserves new client when isNewClient is true and id is null', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    svc.selectClient({
      id: null,
      isNewClient: true,
      name: 'New Co',
      taxType: ClientTaxType.NonTaxSubject,
      address: {
        street: '1',
        streetLine2: null,
        postalCode: null,
        city: 'Tunis',
        governorate: 'Tunis',
        country: 'Tunisie'
      },
      nif: null,
      email: 'n@n.n',
      phone: null,
      contactPerson: null
    });
    expect(svc.client()?.isNewClient).toBe(true);
    expect(svc.client()?.id).toBeNull();
  });

  it('selectClient(null) clears client', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    svc.selectClient({
      id: '550e8400-e29b-41d4-a716-446655440000',
      isNewClient: false,
      name: 'X',
      taxType: ClientTaxType.NonTaxSubject,
      address: {
        street: '1',
        streetLine2: null,
        postalCode: null,
        city: 'Tunis',
        governorate: 'Tunis',
        country: 'Tunisie'
      },
      nif: null,
      email: 'a@a.a',
      phone: null,
      contactPerson: null
    });
    svc.selectClient(null);
    expect(svc.client()).toBeNull();
  });
});

describe('InvoiceWizardService saveDraft (autosave)', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: defaultWizardProviders
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('saveDraft posts to wizard drafts and stores draftId', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    const draftId = 'draft-abc-123';

    let resultId = '';
    svc.saveDraft().subscribe((id) => {
      resultId = id;
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/invoices/wizard/drafts`);
    expect(req.request.method).toBe('POST');
    req.flush({ success: true, data: { id: draftId } });

    expect(resultId).toBe(draftId);
    expect(svc.wizardState().draftId).toBe(draftId);
    expect(svc.wizardState().isDirty).toBe(false);
    expect(svc.wizardState().lastSaved).toBeTruthy();
  });
});

describe('InvoiceWizardService line validation', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: defaultWizardProviders
    });
  });

  it('validateInvoice blocks submission when a line has no productId', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    setupMinimalValidWizardState(svc);
    addValidLinkedLine(svc);
    addUnlinkedLine(svc);

    const result = svc.validateInvoice();
    const linesCheck = result.checks.find(c => c.id === 'lines-product-link');

    expect(linesCheck?.status).toBe('ERROR');
    expect(linesCheck?.isBlocking).toBe(true);
    expect(result.canProceed).toBe(false);
  });

  it('validateInvoice allows submission when all lines are linked to products', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    setupMinimalValidWizardState(svc);
    addValidLinkedLine(svc);

    const result = svc.validateInvoice();
    const linesCheck = result.checks.find(c => c.id === 'lines-product-link');

    expect(linesCheck?.status).toBe('VALID');
    expect(result.canProceed).toBe(true);
  });

  it('billing step blocks next when a line is not linked to a product', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    svc.selectSeller({
      id: '550e8400-e29b-41d4-a716-446655440010',
      companyName: 'Test Seller',
      tradeName: null,
      address: {
        street: '1 rue Test',
        streetLine2: null,
        postalCode: null,
        city: 'Tunis',
        governorate: 'Tunis',
        country: 'Tunisie'
      },
      nif: VALID_NIF,
      commerceRegistry: null,
      vatCode: null,
      logo: null,
      phone: null,
      email: 'seller@test.com'
    });
    svc.updateMetadata({ invoiceNumber: 'FAC-2026-000001' });
    svc.nextStep();
    svc.selectClient({
      id: VALID_CLIENT_ID,
      isNewClient: false,
      name: 'Test Client',
      taxType: ClientTaxType.NonTaxSubject,
      address: {
        street: '2 rue Client',
        streetLine2: null,
        postalCode: null,
        city: 'Tunis',
        governorate: 'Tunis',
        country: 'Tunisie'
      },
      nif: null,
      email: 'client@test.com',
      phone: null,
      contactPerson: null
    });
    svc.nextStep();

    const billingIdx = svc.steps().findIndex(s => s.key === 'billing');
    expect(svc.currentStep()).toBe(billingIdx);

    addValidLinkedLine(svc);
    addUnlinkedLine(svc);

    const billingStep = svc.steps()[billingIdx];
    expect(billingStep.isValid).toBe(false);
    expect(svc.canGoNext()).toBe(false);
  });

  it('submitInvoice emits an observable error when lines are invalid', (done) => {
    const svc = TestBed.inject(InvoiceWizardService);
    setupMinimalValidWizardState(svc);
    addValidLinkedLine(svc);
    addUnlinkedLine(svc);

    svc.submitInvoice().subscribe({
      next: () => done.fail('Expected submission to fail'),
      error: (error: Error) => {
        expect(error.message).toContain('validation');
        expect(svc.submissionError()).toContain('validation');
        done();
      }
    });
  });
});

describe('InvoiceWizardService subscription quota', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        InvoiceWizardService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideSubscriptionMock(createDefaultSubscription({
          invoicesThisMonth: 10,
          canCreateInvoice: false,
          invoiceBlockCode: 'PLAN_INVOICE_QUOTA_EXCEEDED',
          invoiceBlockMessage: 'Limite mensuelle atteinte (10/10 factures). Passez à un forfait supérieur ou attendez le prochain cycle.',
          usage: {
            invoices: { label: 'Factures ce mois', used: 10, limit: 10, isUnlimited: false },
            quotes: { label: 'Devis ce mois', used: 0, limit: 10, isUnlimited: false },
            clients: { label: 'Clients', used: 0, limit: 20, isUnlimited: false },
            products: { label: 'Produits', used: 0, limit: 50, isUnlimited: false },
            storage: { label: 'Stockage', used: 0, limit: 100, isUnlimited: false }
          }
        }))
      ]
    });
  });

  it('validateInvoice blocks submission when monthly invoice quota is reached', (done) => {
    const svc = TestBed.inject(InvoiceWizardService);
    setupMinimalValidWizardState(svc);
    addValidLinkedLine(svc);

    svc.ensureSubscriptionLoaded().subscribe(() => {
      const result = svc.validateInvoice();
      const quotaCheck = result.checks.find(c => c.id === 'invoice-quota');

      expect(quotaCheck?.status).toBe('ERROR');
      expect(quotaCheck?.description).toContain('10/10');
      expect(result.canProceed).toBe(false);
      done();
    });
  });

  it('isPlanQuotaErrorCode recognizes backend plan error codes', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    expect(svc.isPlanQuotaErrorCode('PLAN_INVOICE_QUOTA_EXCEEDED')).toBeTrue();
    expect(svc.isPlanQuotaErrorCode('Validation.Client')).toBeFalse();
    expect(PLAN_QUOTA_ERROR_CODES).toContain('PLAN_SUBSCRIPTION_CANCELLED');
  });

  it('monthlyUnlimited_active_passesQuotaCheck', (done) => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        InvoiceWizardService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideSubscriptionMock(createDefaultSubscription({
          plan: 'Monthly',
          planDisplay: 'Mensuel',
          status: 'Active',
          canCreateInvoice: true,
          usage: {
            invoices: { label: 'Factures ce mois', used: 0, limit: 2147483647, isUnlimited: true },
            quotes: { label: 'Devis ce mois', used: 0, limit: 2147483647, isUnlimited: true },
            clients: { label: 'Clients', used: 0, limit: 2147483647, isUnlimited: true },
            products: { label: 'Produits', used: 0, limit: 2147483647, isUnlimited: true },
            storage: { label: 'Stockage', used: 0, limit: 2147483647, isUnlimited: true }
          }
        }))
      ]
    });

    const svc = TestBed.inject(InvoiceWizardService);
    setupMinimalValidWizardState(svc);
    addValidLinkedLine(svc);

    svc.ensureSubscriptionLoaded({ forceRefresh: true }).subscribe(() => {
      const quotaCheck = svc.validateInvoice().checks.find(c => c.id === 'invoice-quota');
      expect(quotaCheck?.status).toBe('VALID');
      expect(quotaCheck?.description).toContain('illimité');
      done();
    });
  });

  it('monthlySuspended_blocksWithBackendMessage', (done) => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        InvoiceWizardService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideSubscriptionMock(createDefaultSubscription({
          plan: 'Monthly',
          planDisplay: 'Mensuel',
          status: 'Suspended',
          statusDisplay: 'Suspendu',
          canCreateInvoice: false,
          invoiceBlockCode: 'PLAN_SUBSCRIPTION_SUSPENDED',
          invoiceBlockMessage: 'Votre compte est suspendu. Régularisez votre abonnement dans Paramètres > Abonnement.',
          usage: {
            invoices: { label: 'Factures ce mois', used: 0, limit: 2147483647, isUnlimited: true },
            quotes: { label: 'Devis ce mois', used: 0, limit: 2147483647, isUnlimited: true },
            clients: { label: 'Clients', used: 0, limit: 2147483647, isUnlimited: true },
            products: { label: 'Produits', used: 0, limit: 2147483647, isUnlimited: true },
            storage: { label: 'Stockage', used: 0, limit: 2147483647, isUnlimited: true }
          }
        }))
      ]
    });

    const svc = TestBed.inject(InvoiceWizardService);
    setupMinimalValidWizardState(svc);
    addValidLinkedLine(svc);

    svc.ensureSubscriptionLoaded({ forceRefresh: true }).subscribe(() => {
      const quotaCheck = svc.validateInvoice().checks.find(c => c.id === 'invoice-quota');
      expect(quotaCheck?.status).toBe('ERROR');
      expect(quotaCheck?.description).toContain('suspendu');
      done();
    });
  });

  it('refreshSubscription_afterStaleSuspended_allowsMonthlyActive', (done) => {
    const suspended = createDefaultSubscription({
      plan: 'Monthly',
      status: 'Suspended',
      statusDisplay: 'Suspendu',
      canCreateInvoice: false,
      invoiceBlockMessage: 'Votre compte est suspendu.',
      usage: {
        invoices: { label: 'Factures ce mois', used: 0, limit: 2147483647, isUnlimited: true },
        quotes: { label: 'Devis ce mois', used: 0, limit: 10, isUnlimited: true },
        clients: { label: 'Clients', used: 0, limit: 20, isUnlimited: true },
        products: { label: 'Produits', used: 0, limit: 50, isUnlimited: true },
        storage: { label: 'Stockage', used: 0, limit: 100, isUnlimited: true }
      }
    });
    const active = createDefaultSubscription({
      plan: 'Monthly',
      planDisplay: 'Mensuel',
      status: 'Active',
      statusDisplay: 'Actif',
      canCreateInvoice: true,
      usage: {
        invoices: { label: 'Factures ce mois', used: 0, limit: 2147483647, isUnlimited: true },
        quotes: { label: 'Devis ce mois', used: 0, limit: 2147483647, isUnlimited: true },
        clients: { label: 'Clients', used: 0, limit: 2147483647, isUnlimited: true },
        products: { label: 'Produits', used: 0, limit: 2147483647, isUnlimited: true },
        storage: { label: 'Stockage', used: 0, limit: 2147483647, isUnlimited: true }
      }
    });

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        InvoiceWizardService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideSubscriptionMock((forceRefresh?: boolean) => forceRefresh ? active : suspended)
      ]
    });

    const svc = TestBed.inject(InvoiceWizardService);
    setupMinimalValidWizardState(svc);
    addValidLinkedLine(svc);

    svc.ensureSubscriptionLoaded({ forceRefresh: false }).subscribe(() => {
      const blocked = svc.validateInvoice().checks.find(c => c.id === 'invoice-quota');
      expect(blocked?.status).toBe('ERROR');

      svc.ensureSubscriptionLoaded({ forceRefresh: true }).subscribe(() => {
        const allowed = svc.validateInvoice().checks.find(c => c.id === 'invoice-quota');
        expect(allowed?.status).toBe('VALID');
        done();
      });
    });
  });
});

describe('InvoiceWizardService initForCreditNote', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: defaultWizardProviders
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('preserves seller across initForCreditNote reset', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    setupMinimalValidWizardState(svc);
    const sellerId = svc.seller()?.id;

    const invoiceId = '550e8400-e29b-41d4-a716-446655440000';
    let completed = false;
    svc.initForCreditNote(invoiceId).subscribe({
      next: () => { completed = true; }
    });

    const invoiceReq = httpMock.expectOne(`${environment.apiUrl}/invoices/${invoiceId}`);
    invoiceReq.flush({
      success: true,
      data: {
        id: invoiceId,
        clientId: VALID_CLIENT_ID,
        lines: []
      }
    });

    const clientReq = httpMock.expectOne(`${environment.apiUrl}/clients/${VALID_CLIENT_ID}`);
    clientReq.flush({
      success: true,
      data: {
        id: VALID_CLIENT_ID,
        name: 'Test Client',
        type: 'Individual',
        email: 'client@test.com',
        phone: null,
        nif: null,
        address: {
          street: '2 rue Client',
          streetLine2: null,
          postalCode: null,
          city: 'Tunis',
          governorate: 'Tunis'
        }
      }
    });

    expect(completed).toBeTrue();
    expect(svc.seller()?.id).toBe(sellerId);
    expect(svc.metadata().type).toBe(InvoiceType.CreditNote);
    expect(svc.metadata().linkedInvoiceId).toBe(invoiceId);
  });

  it('copies FODEC, allocated discount, empty productId and zero quantity from the source invoice', () => {
    const svc = TestBed.inject(InvoiceWizardService);
    setupMinimalValidWizardState(svc);

    const invoiceId = '550e8400-e29b-41d4-a716-446655440000';
    let completed = false;
    svc.initForCreditNote(invoiceId).subscribe({
      next: () => { completed = true; }
    });

    const invoiceReq = httpMock.expectOne(`${environment.apiUrl}/invoices/${invoiceId}`);
    invoiceReq.flush({
      success: true,
      data: {
        id: invoiceId,
        clientId: VALID_CLIENT_ID,
        warehouseId: '550e8400-e29b-41d4-a716-446655440099',
        totalAmount: 952,
        fiscalStampAmount: 1,
        lines: [
          {
            id: 'line-1',
            lineNumber: 1,
            productId: '00000000-0000-0000-0000-000000000000',
            productCode: 'CUSTOM',
            productName: 'Prestation remisée',
            productDescription: null,
            quantity: 1,
            unit: 'Unité',
            unitPrice: 1000,
            vatRatePercent: 19,
            discountPercent: 0,
            discountAmount: 0,
            allocatedGlobalDiscount: 200,
            subTotal: 800,
            isFodecApplicable: true,
            vatAmount: 152,
            total: 952
          },
          {
            id: 'line-2',
            lineNumber: 2,
            productId: VALID_PRODUCT_ID,
            productCode: 'ART',
            productName: 'Ligne quantité nulle',
            productDescription: null,
            quantity: 0,
            unit: 'Unité',
            unitPrice: 10,
            vatRatePercent: 19,
            discountPercent: null,
            discountAmount: 0,
            subTotal: 0,
            isFodecApplicable: false,
            vatAmount: 0,
            total: 0
          }
        ]
      }
    });

    const clientReq = httpMock.expectOne(`${environment.apiUrl}/clients/${VALID_CLIENT_ID}`);
    clientReq.flush({
      success: true,
      data: {
        id: VALID_CLIENT_ID,
        name: 'Test Client',
        type: 'Individual',
        email: 'client@test.com',
        phone: null,
        nif: null,
        address: {
          street: '2 rue Client',
          streetLine2: null,
          postalCode: null,
          city: 'Tunis',
          governorate: 'Tunis'
        }
      }
    });

    expect(completed).toBeTrue();
    const [discounted, zeroQty] = svc.lines();
    expect(discounted.productId).toBeNull();
    expect(discounted.isFodecApplicable).toBeTrue();
    expect(discounted.priceOverridden).toBeTrue();
    expect(discounted.discountType).toBe('PERCENT');
    expect(discounted.discountValue).toBeCloseTo(20, 5);
    expect(discounted.totalHT).toBeCloseTo(800, 3);
    expect(zeroQty.quantity).toBe(0);
    expect(svc.metadata().warehouseId).toBe('550e8400-e29b-41d4-a716-446655440099');
    expect(svc.wizardState().linkedInvoiceCommercialTtc).toBe(951);
  });

  it('propagates HTTP error from initForCreditNote', (done) => {
    const svc = TestBed.inject(InvoiceWizardService);
    const invoiceId = '550e8400-e29b-41d4-a716-446655440000';

    svc.initForCreditNote(invoiceId).subscribe({
      next: () => fail('should not succeed'),
      error: err => {
        expect(err).toBeTruthy();
        done();
      }
    });

    const invoiceReq = httpMock.expectOne(`${environment.apiUrl}/invoices/${invoiceId}`);
    invoiceReq.flush({ message: 'Not found' }, { status: 404, statusText: 'Not Found' });
  });
});
