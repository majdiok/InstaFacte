import { signal } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of, Subject, throwError } from 'rxjs';
import { delay } from 'rxjs/operators';
import { ProductListItem, ProductService } from '@core/services/product.service';
import { StockService } from '@core/services/stock.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { StockVoucherService } from '@core/services/stock-voucher.service';
import { StockVoucherFormComponent } from './stock-voucher-form.component';

function makeProduct(overrides: Partial<ProductListItem> = {}): ProductListItem {
  return {
    id: 'p1',
    code: 'TAB001',
    name: 'table11',
    description: null,
    typeDisplay: 'Produit',
    categoryId: 'c1',
    category: 'Cat',
    unitPrice: 100,
    unit: 'Unité',
    vatRate: 19,
    isActive: true,
    isStockManaged: true,
    ...overrides
  };
}

describe('StockVoucherFormComponent', () => {
  function createComponent(isEntry: boolean): StockVoucherFormComponent {
    const component = Object.create(StockVoucherFormComponent.prototype) as StockVoucherFormComponent;
    Object.defineProperty(component, 'isEntry', { value: isEntry });
    component.warehouseId = null;
    component.voucherDate = new Date('2026-08-26');
    component.reason = isEntry ? 'InitialStock' : 'Damage';
    component.lines = [];
    component.submitting = signal(false);
    return component;
  }

  it('canSubmit returns false without warehouse or lines', () => {
    const component = createComponent(true);
    expect(component.canSubmit()).toBe(false);

    component.warehouseId = 'wh-1';
    expect(component.canSubmit()).toBe(false);
  });

  it('canSubmit returns true when header and lines are valid', () => {
    const component = createComponent(true);
    component.warehouseId = 'wh-1';
    component.lines = [{
      id: null,
      productId: 'p1',
      productCode: 'P001',
      productName: 'Produit',
      unit: 'Unité',
      quantity: 2,
      unitCost: 10,
      notes: '',
      lotAllocations: []
    }];
    expect(component.canSubmit()).toBe(true);
  });

  it('computes totalQuantity and totalValue', () => {
    const component = createComponent(true);
    component.lines = [
      { id: null, productId: 'p1', productCode: 'A', productName: 'A', unit: 'U', quantity: 2, unitCost: 10, notes: '', lotAllocations: [] },
      { id: null, productId: 'p2', productCode: 'B', productName: 'B', unit: 'U', quantity: 1, unitCost: 5, notes: '', lotAllocations: [] }
    ];
    expect(component.totalQuantity).toBe(3);
    expect(component.totalValue).toBe(25);
  });

  it('qtyExceedsAvailable detects overflow in issue mode', () => {
    const component = createComponent(false);
    component.stockByProductId = signal(new Map([['p1', { qty: 5, avgCost: 1 }]]));
    const line = {
      id: null,
      productId: 'p1',
      productCode: 'A',
      productName: 'A',
      unit: 'U',
      quantity: 6,
      unitCost: 1,
      notes: '',
      lotAllocations: []
    };
    expect(component.qtyExceedsAvailable(line)).toBe(true);
    line.quantity = 4;
    expect(component.qtyExceedsAvailable(line)).toBe(false);
  });

  it('submitBlockersTooltip lists missing steps', () => {
    const component = createComponent(true);
    const tooltip = component.submitBlockersTooltip();
    expect(tooltip).toContain('Sélectionnez un dépôt');
    expect(tooltip).toContain('Ajoutez au moins une ligne produit');
  });

  it('hasWarehouse, hasDate, hasReason, hasLines reflect state', () => {
    const component = createComponent(true);
    expect(component.hasWarehouse()).toBe(false);
    expect(component.hasDate()).toBe(true);
    expect(component.hasReason()).toBe(true);
    expect(component.hasLines()).toBe(false);

    component.warehouseId = 'wh-1';
    component.lines = [{
      id: null, productId: 'p1', productCode: 'A', productName: 'A', unit: 'U',
      quantity: 1, unitCost: 1, notes: '', lotAllocations: []
    }];
    expect(component.hasWarehouse()).toBe(true);
    expect(component.hasLines()).toBe(true);
  });
});

describe('StockVoucherFormComponent product search', () => {
  let fixture: ComponentFixture<StockVoucherFormComponent>;
  let component: StockVoucherFormComponent;
  let getProductsSpy: jasmine.Spy;

  beforeEach(async () => {
    getProductsSpy = jasmine.createSpy('getProducts').and.returnValue(
      of({
        success: true,
        data: {
          items: [
            makeProduct({ id: 'p1', isStockManaged: true }),
            makeProduct({ id: 'p2', code: 'SRV001', name: 'Service', isStockManaged: false })
          ],
          totalCount: 2,
          page: 1,
          pageSize: 50
        }
      })
    );

    await TestBed.configureTestingModule({
      imports: [StockVoucherFormComponent],
      providers: [
        provideRouter([]),
        MessageService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: { kind: 'Entry' },
              paramMap: { get: () => null }
            }
          }
        },
        {
          provide: ProductService,
          useValue: { getProducts: getProductsSpy }
        },
        {
          provide: StockService,
          useValue: {
            getFeatures: () => of({ success: true, data: null }),
            getStockItems: () => of({ success: true, data: { items: [], totalCount: 0 } }),
            getWarehouses: () => of({ success: true, data: { items: [] } })
          }
        },
        {
          provide: WarehouseContextService,
          useValue: { selectedWarehouseId: () => 'wh-1' }
        },
        {
          provide: StockVoucherService,
          useValue: {}
        },
        {
          provide: ErrorHandlerService,
          useValue: { extractErrorMessage: () => 'Erreur' }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StockVoucherFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('calls getProducts twice when the same query is submitted twice', fakeAsync(() => {
    component.onProductSearch({ query: 'tab', originalEvent: new Event('input') });
    tick(300);
    component.onProductSearch({ query: 'tab', originalEvent: new Event('input') });
    tick(300);

    expect(getProductsSpy).toHaveBeenCalledTimes(2);
    expect(component.productsLoading()).toBe(false);
  }));

  it('passes warehouseId from the search request to getProducts', fakeAsync(() => {
    component.warehouseId = 'wh-a';
    component.onProductSearch({ query: 'tab', originalEvent: new Event('input') });
    tick(300);

    expect(getProductsSpy).toHaveBeenCalledWith(jasmine.objectContaining({ warehouseId: 'wh-a' }));

    getProductsSpy.calls.reset();
    component.onWarehouseChange('wh-b');
    component.onProductSearch({ query: 'tab', originalEvent: new Event('input') });
    tick(300);

    expect(getProductsSpy).toHaveBeenCalledWith(jasmine.objectContaining({ warehouseId: 'wh-b' }));
  }));

  it('clears suggestions when warehouse changes', () => {
    component.productSuggestions.set([makeProduct()]);
    component.onWarehouseChange('wh-2');
    expect(component.productSuggestions()).toEqual([]);
  });

  it('filters out products that are not stock managed', fakeAsync(() => {
    component.onProductSearch({ query: 'tab', originalEvent: new Event('input') });
    tick(300);

    expect(component.productSuggestions().length).toBe(1);
    expect(component.productSuggestions()[0].id).toBe('p1');
  }));

  it('resets loading and clears suggestions on HTTP error', fakeAsync(() => {
    getProductsSpy.and.returnValue(throwError(() => new Error('network')));
    component.onProductSearch({ query: 'tab', originalEvent: new Event('input') });
    tick(300);

    expect(component.productsLoading()).toBe(false);
    expect(component.productSuggestions()).toEqual([]);
  }));

  it('resets loading when switchMap cancels a previous in-flight request', fakeAsync(() => {
    const delayed$ = new Subject<{ success: boolean; data: { items: ProductListItem[] } }>();
    getProductsSpy.and.returnValue(delayed$.pipe(delay(1000)));

    component.onProductSearch({ query: 't', originalEvent: new Event('input') });
    tick(300);
    expect(component.productsLoading()).toBe(true);

    component.onProductSearch({ query: 'tab', originalEvent: new Event('input') });
    tick(300);

    delayed$.next({ success: true, data: { items: [makeProduct()] } });
    delayed$.complete();
    tick(1000);

    expect(component.productsLoading()).toBe(false);
    expect(component.productSuggestions().length).toBe(1);
  }));
});
