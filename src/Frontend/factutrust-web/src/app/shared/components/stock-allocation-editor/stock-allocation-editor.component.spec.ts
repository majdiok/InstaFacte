import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { StockService, StockFeatures, StockLotBalance } from '@core/services/stock.service';
import { createDefaultLotRow } from '@shared/utils/stock-traceability.utils';
import { StockAllocationEditorComponent } from './stock-allocation-editor.component';

const trackedFeatures: StockFeatures = {
  lotTrackingEnabled: true,
  serialTrackingEnabled: true,
  expiryTrackingEnabled: false,
  productVariantsEnabled: false,
  fifoLifoValuationEnabled: false,
  blockExpiredLotsOnExit: false,
  strictTrackedAllocation: false
};

describe('StockAllocationEditorComponent', () => {
  let fixture: ComponentFixture<StockAllocationEditorComponent>;
  let getLotsByProduct: jasmine.Spy;

  beforeEach(async () => {
    getLotsByProduct = jasmine.createSpy('getLotsByProduct').and.returnValue(
      of({ success: true, data: [] as StockLotBalance[], message: null, errors: [] })
    );

    await TestBed.configureTestingModule({
      imports: [StockAllocationEditorComponent],
      providers: [
        {
          provide: StockService,
          useValue: {
            getLotsByProduct,
            getSerialsByProduct: jasmine.createSpy('getSerialsByProduct').and.returnValue(
              of({ success: true, data: [], message: null, errors: [] })
            )
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StockAllocationEditorComponent);
  });

  function setExitLotInputs(trackingMode: number | string): void {
    fixture.componentRef.setInput('mode', 'exit');
    fixture.componentRef.setInput('productId', 'prod-1');
    fixture.componentRef.setInput('warehouseId', 'wh-1');
    fixture.componentRef.setInput('lineQuantity', 4);
    fixture.componentRef.setInput('trackingMode', trackingMode);
    fixture.componentRef.setInput('pickingPolicy', 'Manual');
    fixture.componentRef.setInput('features', trackedFeatures);
    fixture.componentRef.setInput('allocations', [createDefaultLotRow(4)]);
  }

  it('loads lots in exit mode when trackingMode is the API string Lot', () => {
    setExitLotInputs('Lot');
    fixture.detectChanges();

    expect(getLotsByProduct).toHaveBeenCalledWith('prod-1', 'wh-1');
  });

  it('does not render a free-text lot field when no lots are available on exit', () => {
    setExitLotInputs('Lot');
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('input[placeholder="N° lot"]')).toBeNull();
    expect(host.textContent).toContain('Aucun lot disponible dans cet entrepôt');
  });

  it('renders a filterable lot select when warehouse lots exist', () => {
    getLotsByProduct.and.returnValue(of({
      success: true,
      data: [{
        productLotId: 'lot-1',
        lotNumber: '455566',
        expiryDate: null,
        quantityOnHand: 85,
        quantityReserved: 0,
        quantityAvailable: 85
      }] as StockLotBalance[],
      message: null,
      errors: []
    }));
    setExitLotInputs('Lot');
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('input[placeholder="N° lot"]')).toBeNull();
    expect(host.querySelector('p-select')).toBeTruthy();
    expect(fixture.componentInstance.lotOptions().some(o => o.label.includes('455566'))).toBe(true);
  });

  it('emits loaded availability with zero lots when the warehouse has none', () => {
    const events: { loaded: boolean; availableCount: number }[] = [];
    fixture.componentInstance.availabilityChange.subscribe(e => events.push(e));
    setExitLotInputs('Lot');
    fixture.detectChanges();

    expect(events.some(e => e.loaded && e.availableCount === 0)).toBe(true);
  });

  it('caps allocation qty to lot available when a lot is picked', () => {
    getLotsByProduct.and.returnValue(of({
      success: true,
      data: [{
        productLotId: 'lot-567777',
        lotNumber: '567777',
        expiryDate: null,
        quantityOnHand: 3,
        quantityReserved: 0,
        quantityAvailable: 3
      }] as StockLotBalance[],
      message: null,
      errors: []
    }));
    setExitLotInputs('Lot');
    fixture.detectChanges();

    const alloc = fixture.componentInstance.allocations[0];
    expect(alloc.quantity).toBe(4);
    fixture.componentInstance.onLotPicked(alloc, 'lot-567777');

    expect(alloc.quantity).toBe(3);
    expect(alloc.productLotId).toBe('lot-567777');
    expect(alloc.lotNumber).toBe('567777');
  });

  it('emits lotStockValidChange false when qty is forced above available', () => {
    getLotsByProduct.and.returnValue(of({
      success: true,
      data: [{
        productLotId: 'lot-567777',
        lotNumber: '567777',
        expiryDate: null,
        quantityOnHand: 3,
        quantityReserved: 0,
        quantityAvailable: 3
      }] as StockLotBalance[],
      message: null,
      errors: []
    }));
    const validity: { productId: string; valid: boolean }[] = [];
    fixture.componentInstance.lotStockValidChange.subscribe(e => validity.push(e));
    setExitLotInputs('Lot');
    fixture.detectChanges();

    const alloc = fixture.componentInstance.allocations[0];
    fixture.componentInstance.onLotPicked(alloc, 'lot-567777');
    alloc.quantity = 4;
    fixture.componentInstance.emitLotStockValidity();
    fixture.detectChanges();

    expect(validity.some(e => e.productId === 'prod-1' && e.valid === false)).toBe(true);
    expect((fixture.nativeElement as HTMLElement).textContent)
      .toContain('Quantité supérieure au disponible du lot');
  });

  it('excludes expired lots from the exit picker when blocking is enabled', () => {
    const blocking: StockFeatures = {
      ...trackedFeatures,
      expiryTrackingEnabled: true,
      blockExpiredLotsOnExit: true
    };
    getLotsByProduct.and.returnValue(of({
      success: true,
      data: [
        {
          productLotId: 'lot-expired',
          lotNumber: '555589',
          expiryDate: '2020-01-01',
          quantityOnHand: 6,
          quantityReserved: 0,
          quantityAvailable: 6
        },
        {
          productLotId: 'lot-567777',
          lotNumber: '567777',
          expiryDate: '2099-12-31',
          quantityOnHand: 2,
          quantityReserved: 0,
          quantityAvailable: 2
        }
      ] as StockLotBalance[],
      message: null,
      errors: []
    }));

    const events: { availableCount: number; expiredExcludedCount?: number }[] = [];
    fixture.componentInstance.availabilityChange.subscribe(e => events.push(e));
    fixture.componentRef.setInput('mode', 'exit');
    fixture.componentRef.setInput('productId', 'prod-1');
    fixture.componentRef.setInput('warehouseId', 'wh-1');
    fixture.componentRef.setInput('lineQuantity', 1);
    fixture.componentRef.setInput('trackingMode', 'Lot');
    fixture.componentRef.setInput('pickingPolicy', 'Fefo');
    fixture.componentRef.setInput('features', blocking);
    fixture.componentRef.setInput('hasExpiryTracking', true);
    fixture.componentRef.setInput('allocations', [createDefaultLotRow(1)]);
    fixture.detectChanges();

    const labels = fixture.componentInstance.lotOptions().map(o => o.label);
    expect(labels.some(l => l.includes('555589'))).toBe(false);
    expect(labels.some(l => l.includes('567777'))).toBe(true);
    expect(fixture.componentInstance.excludedExpiredCount()).toBe(1);
    expect(events.some(e => e.availableCount === 1 && e.expiredExcludedCount === 1)).toBe(true);
    expect((fixture.nativeElement as HTMLElement).textContent)
      .toContain('lot(s) périmé(s) exclus de la sortie');
  });

  it('clears a preselected expired lot when blocking is enabled', () => {
    const blocking: StockFeatures = {
      ...trackedFeatures,
      expiryTrackingEnabled: true,
      blockExpiredLotsOnExit: true
    };
    getLotsByProduct.and.returnValue(of({
      success: true,
      data: [{
        productLotId: 'lot-expired',
        lotNumber: '555589',
        expiryDate: '2020-01-01',
        quantityOnHand: 6,
        quantityReserved: 0,
        quantityAvailable: 6
      }] as StockLotBalance[],
      message: null,
      errors: []
    }));

    const allocations = [{
      lotNumber: '555589',
      expiryDate: new Date('2020-01-01'),
      quantity: 3,
      productLotId: 'lot-expired'
    }];
    fixture.componentRef.setInput('mode', 'exit');
    fixture.componentRef.setInput('productId', 'prod-1');
    fixture.componentRef.setInput('warehouseId', 'wh-1');
    fixture.componentRef.setInput('lineQuantity', 3);
    fixture.componentRef.setInput('trackingMode', 'Lot');
    fixture.componentRef.setInput('pickingPolicy', 'Fefo');
    fixture.componentRef.setInput('features', blocking);
    fixture.componentRef.setInput('hasExpiryTracking', true);
    fixture.componentRef.setInput('allocations', allocations);
    fixture.detectChanges();

    expect(allocations[0].productLotId).toBeNull();
    expect(allocations[0].lotNumber).toBe('');
    expect((fixture.nativeElement as HTMLElement).textContent)
      .toContain('Tous les lots de cet entrepôt sont périmés');
  });
});
