/**
 * Aligned with FactuTrust.Domain.Enums.HonorairesInvoiceStatus.
 * Do not invent intermediate values — backend uses sparse numbering.
 */
export const HonorairesInvoiceStatus = {
  Draft: 0,
  Validated: 1,
  Paid: 4,
  PartiallyPaid: 5,
  Cancelled: 7
} as const;

export type HonorairesInvoiceStatusValue =
  (typeof HonorairesInvoiceStatus)[keyof typeof HonorairesInvoiceStatus];

const STATUS_LABELS: Record<number, string> = {
  [HonorairesInvoiceStatus.Draft]: 'Brouillon',
  [HonorairesInvoiceStatus.Validated]: 'Validée',
  [HonorairesInvoiceStatus.Paid]: 'Payée',
  [HonorairesInvoiceStatus.PartiallyPaid]: 'Partiellement payée',
  [HonorairesInvoiceStatus.Cancelled]: 'Annulée'
};

export const HonorairesDocumentType = {
  Invoice: 0,
  CreditNote: 1
} as const;

export type HonorairesDocumentTypeValue =
  (typeof HonorairesDocumentType)[keyof typeof HonorairesDocumentType];

/** Aligned with FactuTrust.Domain.Enums.HonorairesQuoteStatus. */
export const HonorairesQuoteStatus = {
  Draft: 0,
  Sent: 1,
  Accepted: 2,
  Rejected: 3,
  Expired: 4,
  Converted: 5,
  Cancelled: 6
} as const;

export type HonorairesQuoteStatusValue =
  (typeof HonorairesQuoteStatus)[keyof typeof HonorairesQuoteStatus];

const API_INVOICE_STATUS_MAP: Record<string, HonorairesInvoiceStatusValue> = {
  Draft: HonorairesInvoiceStatus.Draft,
  Validated: HonorairesInvoiceStatus.Validated,
  Paid: HonorairesInvoiceStatus.Paid,
  PartiallyPaid: HonorairesInvoiceStatus.PartiallyPaid,
  Cancelled: HonorairesInvoiceStatus.Cancelled
};

const API_DOCUMENT_TYPE_MAP: Record<string, HonorairesDocumentTypeValue> = {
  Invoice: HonorairesDocumentType.Invoice,
  CreditNote: HonorairesDocumentType.CreditNote
};

const API_QUOTE_STATUS_MAP: Record<string, HonorairesQuoteStatusValue> = {
  Draft: HonorairesQuoteStatus.Draft,
  Sent: HonorairesQuoteStatus.Sent,
  Accepted: HonorairesQuoteStatus.Accepted,
  Rejected: HonorairesQuoteStatus.Rejected,
  Expired: HonorairesQuoteStatus.Expired,
  Converted: HonorairesQuoteStatus.Converted,
  Cancelled: HonorairesQuoteStatus.Cancelled
};

/**
 * Normalizes invoice status from API transport (PascalCase string or numeric enum).
 */
export function normalizeHonorairesInvoiceStatus(
  status: string | number | null | undefined
): HonorairesInvoiceStatusValue | null {
  if (status === null || status === undefined) return null;
  if (typeof status === 'number') {
    return status in STATUS_LABELS ? (status as HonorairesInvoiceStatusValue) : null;
  }
  return API_INVOICE_STATUS_MAP[status] ?? null;
}

/** Normalizes document type from API transport (PascalCase string or numeric enum). */
export function normalizeHonorairesDocumentType(
  type: string | number | null | undefined
): HonorairesDocumentTypeValue | null {
  if (type === null || type === undefined) return null;
  if (typeof type === 'number') {
    return type === HonorairesDocumentType.Invoice || type === HonorairesDocumentType.CreditNote
      ? (type as HonorairesDocumentTypeValue)
      : null;
  }
  return API_DOCUMENT_TYPE_MAP[type] ?? null;
}

/** Normalizes quote status from API transport (PascalCase string or numeric enum). */
export function normalizeHonorairesQuoteStatus(
  status: string | number | null | undefined
): HonorairesQuoteStatusValue | null {
  if (status === null || status === undefined) return null;
  if (typeof status === 'number') {
    return Object.values(HonorairesQuoteStatus).includes(status as HonorairesQuoteStatusValue)
      ? (status as HonorairesQuoteStatusValue)
      : null;
  }
  return API_QUOTE_STATUS_MAP[status] ?? null;
}

/** Matches eligible statuses for creating a credit note from a source invoice. */
export function canCreateHonorairesCreditNote(
  status: string | number,
  isCreditNote: boolean
): boolean {
  if (isCreditNote) return false;
  const normalized = normalizeHonorairesInvoiceStatus(status);
  if (normalized === null) return false;
  return (
    normalized === HonorairesInvoiceStatus.Validated ||
    normalized === HonorairesInvoiceStatus.Paid ||
    normalized === HonorairesInvoiceStatus.PartiallyPaid
  );
}

/** Matches HonorairesInvoiceStatusExtensions.CanBePaymentRecorded. */
export function canRecordHonorairesPayment(
  status: string | number,
  isCreditNote = false
): boolean {
  if (isCreditNote) return false;
  const normalized = normalizeHonorairesInvoiceStatus(status);
  if (normalized === null) return false;
  return (
    normalized === HonorairesInvoiceStatus.Validated ||
    normalized === HonorairesInvoiceStatus.PartiallyPaid
  );
}

export function honorairesInvoiceStatusLabel(
  status: string | number,
  statusDisplay?: string | null
): string {
  if (statusDisplay?.trim()) return statusDisplay.trim();
  const normalized = normalizeHonorairesInvoiceStatus(status);
  return normalized !== null ? (STATUS_LABELS[normalized] ?? '—') : '—';
}

export function honorairesInvoiceStatusSeverity(
  status: string | number
): 'info' | 'success' | 'warn' | 'danger' | 'secondary' {
  const normalized = normalizeHonorairesInvoiceStatus(status);
  if (normalized === null) return 'secondary';
  switch (normalized) {
    case HonorairesInvoiceStatus.Draft:
      return 'info';
    case HonorairesInvoiceStatus.Validated:
      return 'success';
    case HonorairesInvoiceStatus.PartiallyPaid:
      return 'warn';
    case HonorairesInvoiceStatus.Paid:
      return 'success';
    case HonorairesInvoiceStatus.Cancelled:
      return 'danger';
    default:
      return 'secondary';
  }
}

/**
 * Suggests remaining net + RS for a new payment.
 * Applied amount = net + clientWithholding; both count toward AmountDue.
 */
export function suggestHonorairesPaymentAmounts(params: {
  amountDue: number;
  invoiceWithholdingAmount: number;
  paymentsClientWithholdingTotal: number;
}): { amount: number; clientWithholdingAmount: number } {
  const due = Math.max(0, Math.round(params.amountDue * 1000) / 1000);
  const remainingRs = Math.max(
    0,
    Math.round(
      (params.invoiceWithholdingAmount - params.paymentsClientWithholdingTotal) * 1000
    ) / 1000
  );
  const rs = Math.min(remainingRs, due);
  const amount = Math.max(0, Math.round((due - rs) * 1000) / 1000);
  return { amount, clientWithholdingAmount: rs };
}

export function validateHonorairesPaymentDraft(params: {
  amount: number;
  clientWithholdingAmount: number;
  amountDue: number;
  paymentDate: string | null | undefined;
  method: number | null | undefined;
}): string | null {
  if (!params.paymentDate?.trim()) {
    return 'La date de paiement est obligatoire';
  }
  if (params.method == null || Number.isNaN(params.method)) {
    return 'Le mode de paiement est obligatoire';
  }
  const amount = params.amount || 0;
  const rs = params.clientWithholdingAmount || 0;
  if (amount < 0 || rs < 0) {
    return 'Les montants ne peuvent pas être négatifs';
  }
  const applied = Math.round((amount + rs) * 1000) / 1000;
  if (applied <= 0) {
    return 'Le montant encaissé ou la retenue doit être positif';
  }
  if (applied > params.amountDue + 0.0005) {
    return 'Le montant dépasse le reste dû';
  }
  return null;
}
