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
