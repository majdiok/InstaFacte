import {
  Currency,
  InvoiceType,
  InvoiceWizardState,
  TunisianVatRate
} from '../models/invoice-wizard.models';
import {
  calculateLineFodecAmounts,
  computeWizardTotalsCheck,
  DEFAULT_FODEC_RATE_PERCENT
} from './invoice-wizard-calculation.utils';

function minimalState(overrides?: Partial<InvoiceWizardState>): InvoiceWizardState {
  return {
    currentStep: 0,
    steps: [],
    metadata: {
      type: InvoiceType.Invoice,
      invoiceNumber: 'FAC-2026-000001',
      issueDate: new Date(),
      dueDate: null,
      currency: Currency.TND,
      internalReference: null,
      linkedInvoiceId: null,
      warehouseId: null
    },
    seller: null,
    client: null,
    lines: [],
    totals: {
      subTotalHT: 0,
      totalDiscount: 0,
      totalHT: 0,
      totalFodec: 0,
      vatBreakdown: [],
      totalVat: 0,
      fiscalStampAmount: 0,
      totalTTC: 0,
      currency: Currency.TND,
      fodecRatePercent: DEFAULT_FODEC_RATE_PERCENT
    },
    legalMentions: { vatMention: '', exemptionMention: null, customMention: null },
    payment: {
      method: 'BANK_TRANSFER' as never,
      terms: null,
      daysUntilDue: null,
      bankName: null,
      iban: null,
      rib: null,
      purchaseOrderRef: null
    },
    isDirty: false,
    isSaving: false,
    lastSaved: null,
    draftId: null,
    validationResult: null,
    submissionError: null,
    linkedInvoiceCommercialTtc: null,
    ...overrides
  };
}

describe('invoice-wizard-calculation.utils', () => {
  it('calculateLineFodecAmounts applies FODEC then VAT on HT+FODEC', () => {
    const result = calculateLineFodecAmounts(1000, 19, true, 1);
    expect(result.fodecAmount).toBe(10);
    expect(result.vatAmount).toBe(191.9);
    expect(result.totalTTC).toBe(1201.9);
  });

  it('calculateLineFodecAmounts without FODEC keeps VAT on HT only', () => {
    const result = calculateLineFodecAmounts(1000, 19, false, 1);
    expect(result.fodecAmount).toBe(0);
    expect(result.vatAmount).toBe(190);
    expect(result.totalTTC).toBe(1190);
  });

  it('computeWizardTotalsCheck validates FODEC totals including fiscal stamp', () => {
    const state = minimalState({
      lines: [{
        id: '1',
        lineNumber: 1,
        productId: 'p1',
        designation: 'Item',
        description: null,
        quantity: 10,
        unit: 'U',
        unitPriceHT: 100,
        discountType: null,
        discountValue: null,
        vatRate: TunisianVatRate.Standard,
        isFodecApplicable: true,
        discountAmount: 0,
        totalHT: 1000,
        fodecAmount: 10,
        vatAmount: 191.9,
        totalTTC: 1201.9
      }],
      totals: {
        subTotalHT: 1000,
        totalDiscount: 0,
        totalHT: 1000,
        totalFodec: 10,
        vatBreakdown: [],
        totalVat: 191.9,
        fiscalStampAmount: 1,
        totalTTC: 1202.9,
        currency: Currency.TND,
        fodecRatePercent: 1
      }
    });

    expect(computeWizardTotalsCheck(state).isValid).toBeTrue();
  });

  it('computeWizardTotalsCheck fails when FODEC header mismatch', () => {
    const state = minimalState({
      lines: [{
        id: '1',
        lineNumber: 1,
        productId: 'p1',
        designation: 'Item',
        description: null,
        quantity: 1,
        unit: 'U',
        unitPriceHT: 100,
        discountType: null,
        discountValue: null,
        vatRate: TunisianVatRate.Standard,
        isFodecApplicable: true,
        discountAmount: 0,
        totalHT: 100,
        fodecAmount: 1,
        vatAmount: 19.19,
        totalTTC: 120.19
      }],
      totals: {
        subTotalHT: 100,
        totalDiscount: 0,
        totalHT: 100,
        totalFodec: 0,
        vatBreakdown: [],
        totalVat: 19.19,
        fiscalStampAmount: 0,
        totalTTC: 119.19,
        currency: Currency.TND,
        fodecRatePercent: 1
      }
    });

    const check = computeWizardTotalsCheck(state);
    expect(check.isValid).toBeFalse();
    expect(check.fodecMatch).toBeFalse();
  });
});
