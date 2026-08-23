import {
  DeliveryNoteStatus,
  canCreateReturnNoteFromDeliveryNote,
  canGenerateInvoiceFromDeliveryNote
} from './delivery-note.model';

describe('delivery-note invoice / return actions', () => {
  const delivered = {
    status: DeliveryNoteStatus.Delivered,
    invoiceId: undefined as string | undefined,
    hasInvoiceableQuantity: true
  };

  it('shows Générer facture when the BL is delivered and still invoiceable', () => {
    expect(canGenerateInvoiceFromDeliveryNote(delivered)).toBe(true);
  });

  it('hides Générer facture when remaining invoiceable quantity is 0', () => {
    expect(canGenerateInvoiceFromDeliveryNote({
      ...delivered,
      hasInvoiceableQuantity: false
    })).toBe(false);
  });

  it('hides Générer facture when the BL is already invoiced', () => {
    expect(canGenerateInvoiceFromDeliveryNote({
      ...delivered,
      invoiceId: 'inv-1'
    })).toBe(false);
  });

  it('allows creating a return note only for a delivered uninvoiced BL with remaining qty', () => {
    expect(canCreateReturnNoteFromDeliveryNote(delivered)).toBe(true);
    expect(canCreateReturnNoteFromDeliveryNote({
      ...delivered,
      hasInvoiceableQuantity: false
    })).toBe(false);
    expect(canCreateReturnNoteFromDeliveryNote({
      status: DeliveryNoteStatus.Draft,
      hasInvoiceableQuantity: true
    })).toBe(false);
  });
});
