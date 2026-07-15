import { TestBed } from '@angular/core/testing';
import { PosStateService } from './pos-state.service';
import { ProductListItem } from '@core/services/product.service';
import { TunisianVatRate } from '../../invoices/invoice-wizard/models/invoice-wizard.models';

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
