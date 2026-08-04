import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { InventoryWizardComponent } from './inventory-wizard.component';
import { InventoryService, InventoryType, ActiveInventoryDto, InventoryProductItem } from '@core/services/inventory.service';
import { StockService } from '@core/services/stock.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { MessageService } from 'primeng/api';

function makeProduct(overrides: Partial<InventoryProductItem> = {}): InventoryProductItem {
  return {
    productId: 'p1',
    productName: 'Produit A',
    productCode: 'PA001',
    theoreticalQuantity: 10,
    isCounted: false,
    countedQuantity: null,
    ...overrides
  };
}

function makeInventory(products: InventoryProductItem[]): ActiveInventoryDto {
  const counted = products.filter(p => p.isCounted).length;
  return {
    inventoryId: 'inv-1',
    warehouseId: 'wh-1',
    warehouseName: 'Entrepôt principal',
    startedAt: new Date().toISOString(),
    type: InventoryType.Complete,
    typeLabel: 'Complet',
    totalProducts: products.length,
    countedProducts: counted,
    remainingProducts: products.length - counted,
    progressPercent: products.length ? Math.round((counted / products.length) * 100) : 0,
    progressMessage: '',
    products
  };
}

describe('InventoryWizardComponent', () => {
  let fixture: ComponentFixture<InventoryWizardComponent>;
  let component: InventoryWizardComponent;
  let inventoryService: jasmine.SpyObj<InventoryService>;
  let stockService: jasmine.SpyObj<StockService>;
  let confirmationService: { confirm: jasmine.Spy };

  beforeEach(async () => {
    inventoryService = jasmine.createSpyObj('InventoryService', [
      'getActiveInventory',
      'startInventory',
      'recordCount',
      'getSummary',
      'validateInventory',
      'cancelInventory'
    ]);
    stockService = jasmine.createSpyObj('StockService', [
      'getWarehouses',
      'getSimpleOverview'
    ]);
    confirmationService = { confirm: jasmine.createSpy('confirm') };

    inventoryService.getActiveInventory.and.returnValue(of({
      success: true,
      data: null,
      message: null,
      errors: []
    }));
    stockService.getWarehouses.and.returnValue(of({
      success: true,
      data: [{ id: 'wh-1', code: 'WH1', name: 'Principal', address: null, isDefault: true, isActive: true }],
      message: null,
      errors: []
    }));
    stockService.getSimpleOverview.and.returnValue(of({
      success: true,
      data: { items: [] },
      message: null,
      errors: []
    } as any));

    await TestBed.configureTestingModule({
      imports: [InventoryWizardComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        MessageService,
        { provide: InventoryService, useValue: inventoryService },
        { provide: StockService, useValue: stockService },
        {
          provide: ConfirmationService,
          useValue: confirmationService
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(InventoryWizardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('confirmCancelInventory', () => {
    it('opens confirmation with md size and long action labels', () => {
      component.confirmCancelInventory();

      expect(confirmationService.confirm).toHaveBeenCalledWith(
        jasmine.objectContaining({
          header: 'Annuler l\'inventaire',
          acceptLabel: 'Annuler l\'inventaire',
          rejectLabel: 'Continuer le comptage',
          acceptButtonStyleClass: 'btn-danger',
          size: 'md'
        })
      );
    });
  });

  describe('onCountedQuantityChange', () => {
    it('updates the countedQuantity signal', () => {
      component.onCountedQuantityChange(42);
      expect(component.countedQuantity()).toBe(42);

      component.onCountedQuantityChange(null);
      expect(component.countedQuantity()).toBe(0);
    });
  });

  describe('countDifference', () => {
    beforeEach(() => {
      component.activeInventory.set(makeInventory([
        makeProduct({ theoreticalQuantity: 10 })
      ]));
      component.currentProductIndex.set(0);
    });

    it('returns 0 when counted equals theoretical', () => {
      component.countedQuantity.set(10);
      expect(component.countDifference()).toBe(0);
    });

    it('returns positive delta when counted is higher', () => {
      component.countedQuantity.set(15);
      expect(component.countDifference()).toBe(5);
    });

    it('returns negative delta when counted is lower', () => {
      component.countedQuantity.set(7);
      expect(component.countDifference()).toBe(-3);
    });

    it('returns null when no current product', () => {
      component.activeInventory.set(null);
      expect(component.countDifference()).toBeNull();
    });
  });

  describe('toggleProductSelection', () => {
    it('adds and removes product ids without double-toggle', () => {
      expect(component.isProductSelected('p1')).toBeFalse();

      component.toggleProductSelection('p1');
      expect(component.isProductSelected('p1')).toBeTrue();
      expect(component.selectedProductIds().size).toBe(1);

      component.toggleProductSelection('p1');
      expect(component.isProductSelected('p1')).toBeFalse();
      expect(component.selectedProductIds().size).toBe(0);
    });
  });

  describe('canStartPartialInventory', () => {
    it('allows start for complete inventory', () => {
      component.inventoryType.set(InventoryType.Complete);
      expect(component.canStartPartialInventory()).toBeTrue();
    });

    it('blocks start for partial without selection', () => {
      component.inventoryType.set(InventoryType.Partial);
      component.selectedProductIds.set(new Set());
      expect(component.canStartPartialInventory()).toBeFalse();
    });

    it('allows start for partial with selection', () => {
      component.inventoryType.set(InventoryType.Partial);
      component.selectedProductIds.set(new Set(['p1']));
      expect(component.canStartPartialInventory()).toBeTrue();
    });
  });

  describe('findNextUncountedProduct', () => {
    it('selects the first uncounted product and pre-fills theoretical quantity', () => {
      component.activeInventory.set(makeInventory([
        makeProduct({ productId: 'a', isCounted: true, countedQuantity: 5, theoreticalQuantity: 5 }),
        makeProduct({ productId: 'b', isCounted: false, theoreticalQuantity: 12 }),
        makeProduct({ productId: 'c', isCounted: false, theoreticalQuantity: 3 })
      ]));

      component.findNextUncountedProduct();

      expect(component.currentProductIndex()).toBe(1);
      expect(component.countedQuantity()).toBe(12);
      expect(component.currentProduct()?.productId).toBe('b');
    });
  });

  describe('navigateToProduct', () => {
    it('loads counted quantity for already counted products', () => {
      component.activeInventory.set(makeInventory([
        makeProduct({ productId: 'a', isCounted: true, countedQuantity: 8, theoreticalQuantity: 10 })
      ]));

      component.navigateToProduct(0);

      expect(component.currentProductIndex()).toBe(0);
      expect(component.countedQuantity()).toBe(8);
    });

    it('loads theoretical quantity for uncounted products', () => {
      component.activeInventory.set(makeInventory([
        makeProduct({ productId: 'a', isCounted: false, theoreticalQuantity: 10 })
      ]));

      component.navigateToProduct(0);

      expect(component.countedQuantity()).toBe(10);
    });
  });
});
