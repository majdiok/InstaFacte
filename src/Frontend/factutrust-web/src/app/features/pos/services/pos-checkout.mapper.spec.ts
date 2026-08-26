import { PaymentMethod, TunisianVatRate } from '../../invoices/invoice-wizard/models/invoice-wizard.models';
import { POS_PASSENGER_CLIENT_EMAIL } from '../constants/pos-client.constants';
import { PosCheckoutLine } from './pos-checkout.mapper';
import {
  DEFAULT_ON_ACCOUNT_TERM_DAYS,
  normalizePaymentSettlement,
  resolveHeaderPaymentMethod,
  shouldOverrideLinePrice,
  shouldRecordPaymentAfterSubmit,
  toDaysUntilDue,
  toDueDate,
  toNamedWizardClient,
  toPassengerWizardClient,
  toPaymentTerms,
  toWizardLines,
  toWizardMetadata
} from './pos-checkout.mapper';

function line(overrides: Partial<PosCheckoutLine> = {}): PosCheckoutLine {
  return {
    productId: 'p1',
    designation: 'Article',
    description: '  desc  ',
    quantity: 2,
    unit: 'U',
    unitPriceHT: 10,
    vatRate: TunisianVatRate.Standard,
    isFodecApplicable: false,
    totalHT: 20,
    discountType: null,
    discountValue: null,
    discountAmount: 0,
    ...overrides
  };
}

describe('pos-checkout.mapper', () => {
  it('maps passenger client to the seeded walk-in email', () => {
    const client = toPassengerWizardClient();
    expect(client.isNewClient).toBeTrue();
    expect(client.email).toBe(POS_PASSENGER_CLIENT_EMAIL);
    expect(client.id).toBeNull();
  });

  it('maps a named client with NIF as tax subject', () => {
    const client = toNamedWizardClient({
      id: 'c1',
      name: 'Ste Test',
      email: 'a@b.c',
      phone: '71',
      nif: '1234567A'
    });
    expect(client.isNewClient).toBeFalse();
    expect(client.taxType).toBe('TAX_SUBJECT');
    expect(client.nif).toBe('1234567A');
  });

  it('overrides price when negotiated or manually edited, not for catalog-only lines', () => {
    expect(shouldOverrideLinePrice({})).toBeFalse();
    expect(shouldOverrideLinePrice({ isNegotiatedPrice: true })).toBeTrue();
    expect(shouldOverrideLinePrice({ priceManuallyEdited: true })).toBeTrue();
  });

  it('spreads a global discount onto wizard lines and flags negotiated prices', () => {
    const mapped = toWizardLines(
      [line({ isNegotiatedPrice: true, totalHT: 20, discountAmount: 0 })],
      20,
      4
    );
    expect(mapped.length).toBe(1);
    expect(mapped[0].discountType).toBe('AMOUNT');
    expect(mapped[0].discountValue).toBe(4);
    expect(mapped[0].priceOverridden).toBeTrue();
    expect(mapped[0].description).toBe('desc');
  });

  it('builds split payment terms without stuffing amounts', () => {
    const terms = toPaymentTerms({
      isSplit: true,
      orderNotes: 'PO-12',
      settlement: 'immediate',
      dueDate: new Date(2026, 7, 25),
      isCreditNote: false
    });
    expect(terms).toContain('Paiement fractionne');
    expect(terms).toContain('PO-12');
  });

  it('builds on-account terms with due date and skips recording payment', () => {
    const due = toDueDate('onAccount', null, new Date(2026, 7, 1));
    expect(due.getDate()).toBe(1 + DEFAULT_ON_ACCOUNT_TERM_DAYS);
    const terms = toPaymentTerms({
      isSplit: false,
      orderNotes: '',
      settlement: 'onAccount',
      dueDate: due,
      isCreditNote: false
    });
    expect(terms).toContain('Paiement a terme');
    expect(shouldRecordPaymentAfterSubmit('onAccount', false)).toBeFalse();
    expect(shouldRecordPaymentAfterSubmit('immediate', false)).toBeTrue();
    expect(shouldRecordPaymentAfterSubmit('onAccount', true)).toBeTrue();
    expect(toDaysUntilDue('onAccount', 45)).toBe(45);
    expect(toDaysUntilDue('immediate', 45)).toBe(0);
  });

  it('normalizes legacy 2x/3x snapshots to on-account', () => {
    expect(normalizePaymentSettlement('2x')).toBe('onAccount');
    expect(normalizePaymentSettlement('3x')).toBe('onAccount');
    expect(normalizePaymentSettlement('full')).toBe('immediate');
    expect(normalizePaymentSettlement('immediate')).toBe('immediate');
  });

  it('uses the first split method as invoice header method', () => {
    expect(
      resolveHeaderPaymentMethod(true, [{ method: PaymentMethod.Card, amount: 5 }], PaymentMethod.Cash)
    ).toBe(PaymentMethod.Card);
  });

  it('stamps POS internal references for invoices and credit notes', () => {
    const sale = toWizardMetadata({
      isCreditNote: false,
      ticketId: '20260825-1',
      warehouseId: 'wh',
      cashRegisterSessionId: 'sess',
      dueDate: new Date()
    });
    expect(sale.internalReference).toBe('POS-20260825-1');
    expect(sale.cashRegisterSessionId).toBe('sess');

    const credit = toWizardMetadata({
      isCreditNote: true,
      ticketId: '20260825-1',
      warehouseId: 'wh',
      cashRegisterSessionId: null,
      dueDate: new Date(),
      linkedInvoiceId: 'inv'
    });
    expect(credit.internalReference).toBe('POS-AVO-20260825-1');
    expect(credit.linkedInvoiceId).toBe('inv');
  });
});
