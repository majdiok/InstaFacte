/**
 * Single source of truth for invoice metric computations (status classification,
 * realized-revenue rules, period bounds) shared by the dashboard and the analytics
 * reports. Centralizing this logic prevents the surfaces from drifting apart.
 *
 * The invoice list API serializes status via InvoiceStatus.ToDisplayString() (French
 * labels: "Payée", "Validée", "Signée", "Partiellement payée"…). This normalizer also
 * accepts the numeric enum and English keys so callers never depend on a fragile,
 * exact-case string comparison.
 */

/** Mirrors FactuTrust.Domain.Enums.InvoiceStatus (numeric values must match the backend). */
export enum InvoiceStatusKey {
  Draft = 0,
  Validated = 1,
  Signed = 2,
  Paid = 4,
  PartiallyPaid = 5,
  Overdue = 6,
  Cancelled = 7,
  Archived = 8
}

export type RevenuePeriod = 'day' | 'month' | 'year' | 'all';

export interface PeriodRange {
  currentFrom: Date;
  currentTo: Date;
  previousFrom: Date;
  previousTo: Date;
}

/** Minimal shape needed to compute revenue — keeps this util decoupled from service models. */
export interface RevenueInvoiceLike {
  status: string | number | null | undefined;
  issueDate: string | Date;
  /** Signed total (TTC): positive on FAC, negative on AVO (credit notes). */
  totalAmount: number;
}

function stripDiacritics(value: string): string {
  return value.normalize('NFD').replace(/\p{M}/gu, '');
}

const STATUS_TOKEN_MAP: Record<string, InvoiceStatusKey> = {
  // French display labels (primary transport, via ToDisplayString)
  brouillon: InvoiceStatusKey.Draft,
  validee: InvoiceStatusKey.Validated,
  signee: InvoiceStatusKey.Signed,
  payee: InvoiceStatusKey.Paid,
  'partiellement payee': InvoiceStatusKey.PartiallyPaid,
  'en retard': InvoiceStatusKey.Overdue,
  annulee: InvoiceStatusKey.Cancelled,
  archivee: InvoiceStatusKey.Archived,
  // English keys (defensive: other DTOs / future transports)
  draft: InvoiceStatusKey.Draft,
  validated: InvoiceStatusKey.Validated,
  signed: InvoiceStatusKey.Signed,
  paid: InvoiceStatusKey.Paid,
  partiallypaid: InvoiceStatusKey.PartiallyPaid,
  'partially paid': InvoiceStatusKey.PartiallyPaid,
  overdue: InvoiceStatusKey.Overdue,
  cancelled: InvoiceStatusKey.Cancelled,
  canceled: InvoiceStatusKey.Cancelled,
  archived: InvoiceStatusKey.Archived
};

/**
 * Normalizes an invoice status (numeric enum, French label, or English key) to a
 * stable {@link InvoiceStatusKey}. Returns null for unrecognized values.
 */
export function normalizeInvoiceStatus(
  status: string | number | null | undefined
): InvoiceStatusKey | null {
  if (status === null || status === undefined) {
    return null;
  }
  if (typeof status === 'number') {
    return status in InvoiceStatusKey ? (status as InvoiceStatusKey) : null;
  }
  const token = stripDiacritics(status).toLowerCase().trim().replace(/\s+/g, ' ');
  return STATUS_TOKEN_MAP[token] ?? null;
}

/** Statuses that count toward realized revenue (chiffre d'affaires): Payée + Validée. */
export const REALIZED_REVENUE_STATUSES: ReadonlyArray<InvoiceStatusKey> = [
  InvoiceStatusKey.Paid,
  InvoiceStatusKey.Validated
];

/** True when an invoice's amount counts as realized revenue (Payée or Validée). */
export function isRealizedRevenue(status: string | number | null | undefined): boolean {
  const key = normalizeInvoiceStatus(status);
  return key === InvoiceStatusKey.Paid || key === InvoiceStatusKey.Validated;
}

/**
 * True when an invoice is unpaid, matching the backend `unpaidOnly` filter
 * (Status != Paid && Status != Cancelled). Notably includes "Partiellement payée".
 */
export function isUnpaidInvoice(status: string | number | null | undefined): boolean {
  const key = normalizeInvoiceStatus(status);
  return key !== null && key !== InvoiceStatusKey.Paid && key !== InvoiceStatusKey.Cancelled;
}

/**
 * Canonical date bounds for a reporting period. Mirrors the analytics service ranges
 * for day/month/year and adds 'all' (from the epoch to end of today, no prior period).
 */
export function getPeriodRange(period: RevenuePeriod, now: Date = new Date()): PeriodRange {
  if (period === 'day') {
    const currentFrom = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const currentTo = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59);
    const yest = new Date(currentFrom);
    yest.setDate(yest.getDate() - 1);
    return {
      currentFrom,
      currentTo,
      previousFrom: new Date(yest.getFullYear(), yest.getMonth(), yest.getDate()),
      previousTo: new Date(yest.getFullYear(), yest.getMonth(), yest.getDate(), 23, 59, 59)
    };
  }

  if (period === 'month') {
    return {
      currentFrom: new Date(now.getFullYear(), now.getMonth(), 1),
      currentTo: new Date(now.getFullYear(), now.getMonth() + 1, 0, 23, 59, 59),
      previousFrom: new Date(now.getFullYear(), now.getMonth() - 1, 1),
      previousTo: new Date(now.getFullYear(), now.getMonth(), 0, 23, 59, 59)
    };
  }

  if (period === 'year') {
    return {
      currentFrom: new Date(now.getFullYear(), 0, 1),
      currentTo: new Date(now.getFullYear(), 11, 31, 23, 59, 59),
      previousFrom: new Date(now.getFullYear() - 1, 0, 1),
      previousTo: new Date(now.getFullYear() - 1, 11, 31, 23, 59, 59)
    };
  }

  // 'all' — cumulative from the epoch to the end of today; no comparable prior period.
  return {
    currentFrom: new Date(0),
    currentTo: new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59),
    previousFrom: new Date(0),
    previousTo: new Date(0)
  };
}

/** True when a date falls within [from, to] (inclusive). */
export function isWithinRange(date: Date, from: Date, to: Date): boolean {
  return date >= from && date <= to;
}

/**
 * Sums realized-revenue (Payée + Validée) totals, optionally restricted to a date range.
 * Signed `totalAmount` is preserved, so credit notes (negative) correctly reduce the total.
 */
export function sumRealizedRevenue(
  invoices: ReadonlyArray<RevenueInvoiceLike>,
  range?: { from: Date; to: Date }
): number {
  return invoices.reduce((sum, inv) => {
    if (!isRealizedRevenue(inv.status)) {
      return sum;
    }
    if (range) {
      const d = new Date(inv.issueDate);
      if (d < range.from || d > range.to) {
        return sum;
      }
    }
    return sum + inv.totalAmount;
  }, 0);
}
