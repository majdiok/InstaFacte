import {
  InvoiceStatusKey,
  normalizeInvoiceStatus,
  isRealizedRevenue,
  isUnpaidInvoice,
  getPeriodRange,
  sumRealizedRevenue,
  RevenueInvoiceLike
} from './invoice-metrics.util';

describe('invoice-metrics.util', () => {
  describe('normalizeInvoiceStatus', () => {
    it('maps French display labels (case/diacritic-insensitive)', () => {
      expect(normalizeInvoiceStatus('Payée')).toBe(InvoiceStatusKey.Paid);
      expect(normalizeInvoiceStatus('Validée')).toBe(InvoiceStatusKey.Validated);
      // Regression guard: the old analytics code compared against 'VALIDÉE' (uppercase),
      // which never matched the real 'Validée' value.
      expect(normalizeInvoiceStatus('VALIDÉE')).toBe(InvoiceStatusKey.Validated);
      expect(normalizeInvoiceStatus('Partiellement payée')).toBe(InvoiceStatusKey.PartiallyPaid);
      expect(normalizeInvoiceStatus('Signée')).toBe(InvoiceStatusKey.Signed);
      expect(normalizeInvoiceStatus('Annulée')).toBe(InvoiceStatusKey.Cancelled);
    });

    it('maps English keys defensively', () => {
      expect(normalizeInvoiceStatus('paid')).toBe(InvoiceStatusKey.Paid);
      expect(normalizeInvoiceStatus('validated')).toBe(InvoiceStatusKey.Validated);
      expect(normalizeInvoiceStatus('partiallyPaid')).toBe(InvoiceStatusKey.PartiallyPaid);
    });

    it('maps numeric enum values', () => {
      expect(normalizeInvoiceStatus(4)).toBe(InvoiceStatusKey.Paid);
      expect(normalizeInvoiceStatus(1)).toBe(InvoiceStatusKey.Validated);
      expect(normalizeInvoiceStatus(0)).toBe(InvoiceStatusKey.Draft);
    });

    it('returns null for unknown values', () => {
      expect(normalizeInvoiceStatus('badStatus')).toBeNull();
      expect(normalizeInvoiceStatus(999)).toBeNull();
      expect(normalizeInvoiceStatus(3)).toBeNull();
      expect(normalizeInvoiceStatus(null)).toBeNull();
      expect(normalizeInvoiceStatus(undefined)).toBeNull();
    });
  });

  describe('isRealizedRevenue', () => {
    it('counts Payée and Validée only', () => {
      expect(isRealizedRevenue('Payée')).toBe(true);
      expect(isRealizedRevenue('Validée')).toBe(true);
    });

    it('excludes Signée, drafts, partial, overdue, cancelled', () => {
      expect(isRealizedRevenue('Signée')).toBe(false);
      expect(isRealizedRevenue('Brouillon')).toBe(false);
      expect(isRealizedRevenue('Partiellement payée')).toBe(false);
      expect(isRealizedRevenue('En retard')).toBe(false);
      expect(isRealizedRevenue('Annulée')).toBe(false);
    });
  });

  describe('isUnpaidInvoice', () => {
    it('matches backend unpaidOnly: everything except Paid and Cancelled', () => {
      expect(isUnpaidInvoice('Brouillon')).toBe(true);
      expect(isUnpaidInvoice('Validée')).toBe(true);
      expect(isUnpaidInvoice('Signée')).toBe(true);
      expect(isUnpaidInvoice('En retard')).toBe(true);
      // Critical: partially paid IS unpaid (the dashboard previously excluded it).
      expect(isUnpaidInvoice('Partiellement payée')).toBe(true);
    });

    it('excludes Paid and Cancelled', () => {
      expect(isUnpaidInvoice('Payée')).toBe(false);
      expect(isUnpaidInvoice('Annulée')).toBe(false);
    });

    it('excludes unrecognized statuses', () => {
      expect(isUnpaidInvoice('???')).toBe(false);
    });
  });

  describe('getPeriodRange', () => {
    const now = new Date(2026, 5, 18, 10, 0, 0); // 18 June 2026

    it('computes month bounds', () => {
      const r = getPeriodRange('month', now);
      expect(r.currentFrom).toEqual(new Date(2026, 5, 1));
      expect(r.currentTo).toEqual(new Date(2026, 6, 0, 23, 59, 59));
      expect(r.previousFrom).toEqual(new Date(2026, 4, 1));
    });

    it('computes year bounds', () => {
      const r = getPeriodRange('year', now);
      expect(r.currentFrom).toEqual(new Date(2026, 0, 1));
      expect(r.currentTo).toEqual(new Date(2026, 11, 31, 23, 59, 59));
      expect(r.previousFrom).toEqual(new Date(2025, 0, 1));
    });

    it('computes cumulative (all) bounds from the epoch with no prior period', () => {
      const r = getPeriodRange('all', now);
      expect(r.currentFrom).toEqual(new Date(0));
      expect(r.currentTo).toEqual(new Date(2026, 5, 18, 23, 59, 59));
      expect(r.previousFrom).toEqual(new Date(0));
      expect(r.previousTo).toEqual(new Date(0));
    });
  });

  describe('sumRealizedRevenue', () => {
    const invoices: RevenueInvoiceLike[] = [
      { status: 'Payée', issueDate: '2026-06-10', totalAmount: 1000 },
      { status: 'Validée', issueDate: '2026-06-12', totalAmount: 500 },
      { status: 'Signée', issueDate: '2026-06-12', totalAmount: 9999 },     // excluded
      { status: 'Brouillon', issueDate: '2026-06-12', totalAmount: 9999 },  // excluded
      { status: 'Payée', issueDate: '2025-01-01', totalAmount: 200 },       // out of range
      { status: 'Payée', issueDate: '2026-06-15', totalAmount: -300 }       // credit note (AVO)
    ];

    it('sums Payée + Validée, signed, all-time when no range', () => {
      expect(sumRealizedRevenue(invoices)).toBe(1000 + 500 + 200 - 300);
    });

    it('restricts to a date range and keeps credit notes negative', () => {
      const total = sumRealizedRevenue(invoices, {
        from: new Date(2026, 5, 1),
        to: new Date(2026, 5, 30, 23, 59, 59)
      });
      expect(total).toBe(1000 + 500 - 300);
    });
  });
});
