import { PaymentMethod } from '../../invoices/invoice-wizard/models/invoice-wizard.models';

/** Maps wizard/POS string payment methods to API `PaymentMethod` integers. */
export function toApiPaymentMethod(method: PaymentMethod | string | number): number {
  if (typeof method === 'number' && Number.isFinite(method)) {
    return method;
  }

  switch (method) {
    case PaymentMethod.Cash:
    case 'CASH':
      return 0;
    case PaymentMethod.BankTransfer:
    case 'BANK_TRANSFER':
      return 1;
    case PaymentMethod.Check:
    case 'CHECK':
      return 2;
    case PaymentMethod.Card:
    case 'CARD':
      return 3;
    case PaymentMethod.Effect:
    case 'EFFECT':
      return 5;
    default:
      return 99;
  }
}
