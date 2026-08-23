import { TestBed } from '@angular/core/testing';
import { PosStateService } from './pos-state.service';
import { ProductListItem } from '@core/services/product.service';
import { LinkedInvoiceRef } from '@core/services/invoice-reference-resolver.service';

function fodecProduct(): ProductListItem {
  return {
    id: 'prod-fodec-1',
    code: 'FODEC-01',
    name: 'Produit FODEC',
    description: null,
    typeDisplay: 'Produit',
    categoryId: 'cat-1',
    category: 'Cat',
    unitPrice: 100,
    vatRate: 19,
    unit: 'U',
    isActive: true,
    isStockManaged: false,
    imageUrl: null,
    isFodecApplicable: true
  };
}

function sampleLinkedInvoice(): LinkedInvoiceRef {
  return {
    id: '550e8400-e29b-41d4-a716-446655440000',
    number: 'FAC-2026-000042',
    clientName: 'Client test',
    status: 'Validée',
    totalTTC: 100
  };
}

describe('PosStateService credit note mode', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [PosStateService] });
  });

  it('canValidate requires linked invoice and at least one line in credit mode', () => {
    const svc = TestBed.inject(PosStateService);
    expect(svc.canValidate()).toBeFalse();

    svc.enableCreditNoteMode(sampleLinkedInvoice());
    expect(svc.canValidate()).toBeFalse();

    svc.addProduct(fodecProduct());
    expect(svc.canValidate()).toBeTrue();
  });

  it('disableCreditNoteMode clears linked invoice and lines', () => {
    const svc = TestBed.inject(PosStateService);
    svc.enableCreditNoteMode(sampleLinkedInvoice());
    svc.addProduct(fodecProduct());

    svc.disableCreditNoteMode();

    expect(svc.isCreditNote()).toBeFalse();
    expect(svc.linkedInvoice()).toBeNull();
    expect(svc.lines().length).toBe(0);
    expect(svc.canValidate()).toBeFalse();
  });

  it('enableCreditNoteMode ignores invalid ref', () => {
    const svc = TestBed.inject(PosStateService);
    svc.enableCreditNoteMode({ ...sampleLinkedInvoice(), id: '  ' });
    expect(svc.isCreditNote()).toBeFalse();
  });
});

describe('PosStateService FODEC', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [PosStateService] });
  });

  it('addProduct with FODEC computes line and totals with VAT on HT+FODEC', () => {
    const svc = TestBed.inject(PosStateService);
    svc.addProduct(fodecProduct());

    const line = svc.lines()[0];
    expect(line.isFodecApplicable).toBeTrue();
    expect(line.totalHT).toBe(100);
    expect(line.fodecAmount).toBe(1);
    expect(line.vatAmount).toBe(19.19);
    expect(line.totalTTC).toBe(120.19);

    const totals = svc.totals();
    expect(totals.totalFodec).toBe(1);
    expect(totals.totalVat).toBe(19.19);
    expect(totals.totalTTC).toBe(120.19);
    expect(totals.fodecRatePercent).toBe(1);
  });

  it('addProduct without FODEC keeps legacy amounts', () => {
    const svc = TestBed.inject(PosStateService);
    svc.addProduct({ ...fodecProduct(), isFodecApplicable: false });

    const line = svc.lines()[0];
    expect(line.fodecAmount).toBe(0);
    expect(line.vatAmount).toBe(19);
    expect(line.totalTTC).toBe(119);
    expect(svc.totals().totalFodec).toBe(0);
  });
});

describe('PosStateService restoreSnapshot', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [PosStateService] });
  });

  it('restores lines but generates a new ticket id', () => {
    const svc = TestBed.inject(PosStateService);
    svc.addProduct({ ...fodecProduct(), isFodecApplicable: false });
    const snapshot = svc.getSnapshot();
    const previousTicketId = snapshot.sessionId;

    svc.resetOrder();
    svc.restoreSnapshot(snapshot);

    expect(svc.lines().length).toBe(1);
    expect(svc.sessionId()).not.toBe(previousTicketId);
  });
});

describe('PosStateService applyResolvedPrices', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [PosStateService] });
  });

  it('applies the resolved price and recalculates the line', () => {
    const svc = TestBed.inject(PosStateService);
    svc.addProduct({ ...fodecProduct(), isFodecApplicable: false });
    expect(svc.lines()[0].unitPriceHT).toBe(100);

    svc.applyResolvedPrices([
      { productId: 'prod-fodec-1', unitPriceHT: 80, isNegotiated: true }
    ]);

    const line = svc.lines()[0];
    expect(line.unitPriceHT).toBe(80);
    expect(line.isNegotiatedPrice).toBeTrue();
    expect(line.totalHT).toBe(80);
    expect(line.vatAmount).toBe(15.2);
    expect(svc.totals().totalTTC).toBe(95.2);
  });

  it('leaves untouched the lines absent from the response', () => {
    const svc = TestBed.inject(PosStateService);
    svc.addProduct({ ...fodecProduct(), isFodecApplicable: false });

    svc.applyResolvedPrices([
      { productId: 'un-autre-produit', unitPriceHT: 10, isNegotiated: true }
    ]);

    expect(svc.lines()[0].unitPriceHT).toBe(100);
    expect(svc.lines()[0].isNegotiatedPrice).toBeUndefined();
  });

  it('is a no-op for an empty response', () => {
    const svc = TestBed.inject(PosStateService);
    svc.addProduct({ ...fodecProduct(), isFodecApplicable: false });

    svc.applyResolvedPrices([]);

    expect(svc.lines()[0].unitPriceHT).toBe(100);
  });

  it('restores the catalog price when the client is cleared', () => {
    const svc = TestBed.inject(PosStateService);
    svc.addProduct({ ...fodecProduct(), isFodecApplicable: false });

    svc.applyResolvedPrices([
      { productId: 'prod-fodec-1', unitPriceHT: 80, isNegotiated: true }
    ]);
    expect(svc.lines()[0].unitPriceHT).toBe(80);

    // Retour au client de passage : le serveur repond le prix catalogue.
    svc.applyResolvedPrices([
      { productId: 'prod-fodec-1', unitPriceHT: 100, isNegotiated: false }
    ]);

    const line = svc.lines()[0];
    expect(line.unitPriceHT).toBe(100);
    expect(line.isNegotiatedPrice).toBeFalse();
    expect(svc.totals().totalTTC).toBe(119);
  });
});
