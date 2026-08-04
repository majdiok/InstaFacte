import {
  canCreateHonorairesCreditNote,
  canRecordHonorairesPayment,
  honorairesInvoiceStatusLabel,
  honorairesInvoiceStatusSeverity,
  normalizeHonorairesDocumentType,
  normalizeHonorairesInvoiceStatus,
  normalizeHonorairesQuoteStatus,
  suggestHonorairesPaymentAmounts,
  validateHonorairesPaymentDraft,
  HonorairesDocumentType,
  HonorairesInvoiceStatus,
  HonorairesQuoteStatus
} from './honoraires-invoice-status';

describe('honoraires-invoice-status', () => {
  it('maps status labels like backend ToDisplayString', () => {
    expect(honorairesInvoiceStatusLabel(HonorairesInvoiceStatus.Draft)).toBe('Brouillon');
    expect(honorairesInvoiceStatusLabel(HonorairesInvoiceStatus.Validated)).toBe('Validée');
    expect(honorairesInvoiceStatusLabel(HonorairesInvoiceStatus.Paid)).toBe('Payée');
    expect(honorairesInvoiceStatusLabel(HonorairesInvoiceStatus.PartiallyPaid)).toBe(
      'Partiellement payée'
    );
    expect(honorairesInvoiceStatusLabel(HonorairesInvoiceStatus.Cancelled)).toBe('Annulée');
  });

  it('prefers API statusDisplay when provided', () => {
    expect(honorairesInvoiceStatusLabel(HonorairesInvoiceStatus.Validated, 'Custom')).toBe(
      'Custom'
    );
  });

  it('does not map Paid as Annulée (regression)', () => {
    expect(honorairesInvoiceStatusLabel(4)).not.toBe('Annulée');
    expect(honorairesInvoiceStatusLabel(5)).not.toBe('En retard');
  });

  it('canCreateHonorairesCreditNote allows finalized invoices only', () => {
    expect(canCreateHonorairesCreditNote(HonorairesInvoiceStatus.Draft, false)).toBeFalse();
    expect(canCreateHonorairesCreditNote(HonorairesInvoiceStatus.Validated, false)).toBeTrue();
    expect(canCreateHonorairesCreditNote(HonorairesInvoiceStatus.Paid, false)).toBeTrue();
    expect(canCreateHonorairesCreditNote(HonorairesInvoiceStatus.PartiallyPaid, false)).toBeTrue();
    expect(canCreateHonorairesCreditNote(HonorairesInvoiceStatus.Cancelled, false)).toBeFalse();
    expect(canCreateHonorairesCreditNote(HonorairesInvoiceStatus.Validated, true)).toBeFalse();
  });

  it('canCreateHonorairesCreditNote accepts API string status Validated', () => {
    expect(canCreateHonorairesCreditNote('Validated', false)).toBeTrue();
    expect(canCreateHonorairesCreditNote('Paid', false)).toBeTrue();
    expect(canCreateHonorairesCreditNote('PartiallyPaid', false)).toBeTrue();
    expect(canCreateHonorairesCreditNote('Draft', false)).toBeFalse();
    expect(canCreateHonorairesCreditNote('Cancelled', false)).toBeFalse();
  });

  it('normalizeHonorairesInvoiceStatus maps API transport', () => {
    expect(normalizeHonorairesInvoiceStatus('Validated')).toBe(HonorairesInvoiceStatus.Validated);
    expect(normalizeHonorairesInvoiceStatus('PartiallyPaid')).toBe(HonorairesInvoiceStatus.PartiallyPaid);
    expect(normalizeHonorairesInvoiceStatus(1)).toBe(HonorairesInvoiceStatus.Validated);
    expect(normalizeHonorairesInvoiceStatus('Unknown')).toBeNull();
  });

  it('normalizeHonorairesDocumentType maps API transport', () => {
    expect(normalizeHonorairesDocumentType('Invoice')).toBe(HonorairesDocumentType.Invoice);
    expect(normalizeHonorairesDocumentType('CreditNote')).toBe(HonorairesDocumentType.CreditNote);
    expect(normalizeHonorairesDocumentType(0)).toBe(HonorairesDocumentType.Invoice);
  });

  it('normalizeHonorairesQuoteStatus maps API transport', () => {
    expect(normalizeHonorairesQuoteStatus('Sent')).toBe(HonorairesQuoteStatus.Sent);
    expect(normalizeHonorairesQuoteStatus('Accepted')).toBe(HonorairesQuoteStatus.Accepted);
    expect(normalizeHonorairesQuoteStatus(1)).toBe(HonorairesQuoteStatus.Sent);
  });

  it('canRecordHonorairesPayment accepts API string status', () => {
    expect(canRecordHonorairesPayment('Validated')).toBeTrue();
    expect(canRecordHonorairesPayment('PartiallyPaid')).toBeTrue();
    expect(canRecordHonorairesPayment('Paid')).toBeFalse();
    expect(canRecordHonorairesPayment('Draft')).toBeFalse();
  });

  it('canRecordHonorairesPayment excludes credit notes and paid invoices', () => {
    expect(canRecordHonorairesPayment(HonorairesInvoiceStatus.Validated)).toBeTrue();
    expect(canRecordHonorairesPayment(HonorairesInvoiceStatus.PartiallyPaid)).toBeTrue();
    expect(canRecordHonorairesPayment(HonorairesInvoiceStatus.Paid)).toBeFalse();
    expect(canRecordHonorairesPayment(HonorairesInvoiceStatus.Draft)).toBeFalse();
    expect(canRecordHonorairesPayment(HonorairesInvoiceStatus.Validated, true)).toBeFalse();
  });

  it('defines document types for invoices and credit notes', () => {
    expect(HonorairesDocumentType.Invoice).toBe(0);
    expect(HonorairesDocumentType.CreditNote).toBe(1);
  });

  it('status severity marks Cancelled as danger and Paid as success', () => {
    expect(honorairesInvoiceStatusSeverity(HonorairesInvoiceStatus.Cancelled)).toBe('danger');
    expect(honorairesInvoiceStatusSeverity(HonorairesInvoiceStatus.Paid)).toBe('success');
    expect(honorairesInvoiceStatusSeverity(HonorairesInvoiceStatus.PartiallyPaid)).toBe('warn');
  });

  it('suggestHonorairesPaymentAmounts uses amountDue and remaining RS', () => {
    expect(
      suggestHonorairesPaymentAmounts({
        amountDue: 238,
        invoiceWithholdingAmount: 20,
        paymentsClientWithholdingTotal: 0
      })
    ).toEqual({ amount: 218, clientWithholdingAmount: 20 });

    expect(
      suggestHonorairesPaymentAmounts({
        amountDue: 100,
        invoiceWithholdingAmount: 20,
        paymentsClientWithholdingTotal: 20
      })
    ).toEqual({ amount: 100, clientWithholdingAmount: 0 });
  });

  it('validateHonorairesPaymentDraft rejects overpayment and missing fields', () => {
    expect(
      validateHonorairesPaymentDraft({
        amount: 90,
        clientWithholdingAmount: 20,
        amountDue: 100,
        paymentDate: '2026-08-01',
        method: 1
      })
    ).toContain('dépasse');

    expect(
      validateHonorairesPaymentDraft({
        amount: 80,
        clientWithholdingAmount: 20,
        amountDue: 100,
        paymentDate: '2026-08-01',
        method: 1
      })
    ).toBeNull();

    expect(
      validateHonorairesPaymentDraft({
        amount: 10,
        clientWithholdingAmount: 0,
        amountDue: 100,
        paymentDate: '',
        method: 1
      })
    ).toContain('date');
  });
});
