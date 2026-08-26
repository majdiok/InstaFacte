import {
  ClientTaxType,
  Currency,
  InvoiceType,
  PaymentMethod,
  TunisianVatRate
} from '../../invoices/invoice-wizard/models/invoice-wizard.models';
import { POS_PASSENGER_CLIENT_EMAIL, POS_PASSENGER_CLIENT_NAME } from '../constants/pos-client.constants';

export type PosPaymentSettlement = 'immediate' | 'onAccount';

export const DEFAULT_ON_ACCOUNT_TERM_DAYS = 30;

export interface PosCheckoutClient {
  id: string;
  name: string;
  email: string;
  phone: string;
  nif: string;
}

export interface PosCheckoutLine {
  productId: string;
  designation: string;
  description: string | null;
  quantity: number;
  unit: string;
  unitPriceHT: number;
  vatRate: TunisianVatRate;
  isFodecApplicable: boolean;
  discountType: 'PERCENT' | 'AMOUNT' | null;
  discountValue: number | null;
  discountAmount: number;
  totalHT: number;
  isNegotiatedPrice?: boolean;
  priceManuallyEdited?: boolean;
}

export interface PosCheckoutSplit {
  method: PaymentMethod;
  amount: number;
}

export interface PosWizardLinePayload {
  productId: string;
  designation: string;
  description: string | null;
  quantity: number;
  unit: string;
  unitPriceHT: number;
      vatRate: TunisianVatRate;
  isFodecApplicable: boolean;
  discountType: 'PERCENT' | 'AMOUNT' | null;
  discountValue: number | null;
  priceOverridden: boolean;
}

export interface PosWizardMetadataPayload {
  type: InvoiceType;
  issueDate: Date;
  dueDate: Date;
  currency: Currency;
  internalReference: string;
  warehouseId: string | null;
  cashRegisterSessionId: string | null;
  linkedInvoiceId?: string | null;
}

export interface PosWizardClientPayload {
  id: string | null;
  isNewClient: boolean;
  name: string;
  taxType: ClientTaxType;
  address: {
    street: string;
    streetLine2: null;
    postalCode: null;
    city: string;
    governorate: string;
    country: string;
  };
  nif: string | null;
  email: string;
  phone: string | null;
  contactPerson: null;
}

export function normalizePaymentSettlement(raw: unknown): PosPaymentSettlement {
  if (raw === 'onAccount' || raw === '2x' || raw === '3x') {
    return 'onAccount';
  }
  return 'immediate';
}

export function shouldOverrideLinePrice(line: {
  isNegotiatedPrice?: boolean;
  priceManuallyEdited?: boolean;
}): boolean {
  return !!line.isNegotiatedPrice || !!line.priceManuallyEdited;
}

export function shouldRecordPaymentAfterSubmit(
  settlement: PosPaymentSettlement,
  isSplit: boolean
): boolean {
  if (isSplit) {
    return true;
  }
  return settlement === 'immediate';
}

export function toDaysUntilDue(
  settlement: PosPaymentSettlement,
  paymentTermDays: number | null | undefined
): number {
  if (settlement !== 'onAccount') {
    return 0;
  }
  if (paymentTermDays != null && Number.isFinite(paymentTermDays) && paymentTermDays > 0) {
    return Math.floor(paymentTermDays);
  }
  return DEFAULT_ON_ACCOUNT_TERM_DAYS;
}

export function toDueDate(
  settlement: PosPaymentSettlement,
  paymentTermDays: number | null | undefined,
  now = new Date()
): Date {
  const due = new Date(now.getTime());
  due.setDate(due.getDate() + toDaysUntilDue(settlement, paymentTermDays));
  return due;
}

export function toPaymentTerms(input: {
  isSplit: boolean;
  orderNotes: string;
  settlement: PosPaymentSettlement;
  dueDate: Date;
  isCreditNote: boolean;
}): string {
  let paymentTerms = input.isCreditNote ? 'Remboursement comptant' : 'Paiement comptant';
  if (input.isSplit) {
    paymentTerms = input.isCreditNote ? 'Remboursement fractionne' : 'Paiement fractionne';
  } else if (!input.isCreditNote && input.settlement === 'onAccount') {
    paymentTerms = `Paiement a terme. Echeance: ${input.dueDate.toLocaleDateString('fr-FR')}`;
  }
  const notes = input.orderNotes?.trim();
  if (notes) {
    paymentTerms = paymentTerms ? `${paymentTerms} | ${notes}` : notes;
  }
  return paymentTerms;
}

export function toWizardLines(
  lines: ReadonlyArray<PosCheckoutLine>,
  subTotalHT: number,
  globalDiscount: number
): PosWizardLinePayload[] {
  return lines.map(line => {
    let discountType = line.discountType;
    let discountValue = line.discountValue;
    if (globalDiscount > 0 && subTotalHT > 0) {
      const lineShare = (line.totalHT / subTotalHT) * globalDiscount;
      const effectiveDiscount = line.discountAmount + lineShare;
      discountType = 'AMOUNT';
      discountValue = effectiveDiscount;
    }
    return {
      productId: line.productId,
      designation: line.designation,
      description: (line.description?.trim()) || null,
      quantity: line.quantity,
      unit: line.unit,
      unitPriceHT: line.unitPriceHT,
      vatRate: line.vatRate,
      isFodecApplicable: line.isFodecApplicable ?? false,
      discountType: discountType ?? null,
      discountValue: discountValue ?? null,
      priceOverridden: shouldOverrideLinePrice(line)
    };
  });
}

export function toWizardMetadata(input: {
  isCreditNote: boolean;
  ticketId: string;
  warehouseId: string | null;
  cashRegisterSessionId: string | null;
  dueDate: Date;
  linkedInvoiceId?: string | null;
}): PosWizardMetadataPayload {
  return {
    type: input.isCreditNote ? InvoiceType.CreditNote : InvoiceType.Invoice,
    issueDate: new Date(),
    dueDate: input.dueDate,
    currency: Currency.TND,
    internalReference: input.isCreditNote
      ? `POS-AVO-${input.ticketId}`
      : `POS-${input.ticketId}`,
    warehouseId: input.warehouseId,
    cashRegisterSessionId: input.cashRegisterSessionId,
    linkedInvoiceId: input.isCreditNote ? input.linkedInvoiceId ?? null : null
  };
}

export function toPassengerWizardClient(): PosWizardClientPayload {
  return {
    id: null,
    isNewClient: true,
    name: POS_PASSENGER_CLIENT_NAME,
    taxType: ClientTaxType.NonTaxSubject,
    address: {
      street: 'Non spécifié',
      streetLine2: null,
      postalCode: null,
      city: 'Non spécifié',
      governorate: 'Non spécifié',
      country: 'Tunisie'
    },
    nif: null,
    email: POS_PASSENGER_CLIENT_EMAIL,
    phone: null,
    contactPerson: null
  };
}

export function toNamedWizardClient(client: PosCheckoutClient): PosWizardClientPayload {
  return {
    id: client.id,
    isNewClient: false,
    name: client.name,
    taxType: client.nif ? ClientTaxType.TaxSubject : ClientTaxType.NonTaxSubject,
    address: {
      street: '',
      streetLine2: null,
      postalCode: null,
      city: '',
      governorate: '',
      country: 'Tunisie'
    },
    nif: client.nif || null,
    email: client.email,
    phone: client.phone || null,
    contactPerson: null
  };
}

export function resolveHeaderPaymentMethod(
  isSplit: boolean,
  splits: ReadonlyArray<PosCheckoutSplit>,
  paymentMethod: PaymentMethod
): PaymentMethod {
  if (isSplit && splits.length > 0) {
    return splits[0].method;
  }
  return paymentMethod;
}
