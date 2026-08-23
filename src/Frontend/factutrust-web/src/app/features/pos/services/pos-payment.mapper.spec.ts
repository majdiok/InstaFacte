import { toApiPaymentMethod } from './pos-payment.mapper';
import { PaymentMethod } from '../../invoices/invoice-wizard/models/invoice-wizard.models';

describe('toApiPaymentMethod', () => {
  it('maps wizard string enums to API integers', () => {
    expect(toApiPaymentMethod(PaymentMethod.Cash)).toBe(0);
    expect(toApiPaymentMethod(PaymentMethod.BankTransfer)).toBe(1);
    expect(toApiPaymentMethod(PaymentMethod.Check)).toBe(2);
    expect(toApiPaymentMethod(PaymentMethod.Card)).toBe(3);
    expect(toApiPaymentMethod(PaymentMethod.Effect)).toBe(5);
  });

  it('passes through numeric methods and defaults unknown values to Other', () => {
    expect(toApiPaymentMethod(0)).toBe(0);
    expect(toApiPaymentMethod('UNKNOWN')).toBe(99);
  });
});
