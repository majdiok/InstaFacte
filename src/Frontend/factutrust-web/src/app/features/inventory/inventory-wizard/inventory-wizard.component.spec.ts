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
import { TRACKING_MODE_LOT } from '@shared/utils/stock-traceability.utils';

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

  describe('submitCount', () => {
    it('sends lotNumber for opening inventory on lot-tracked product', () => {
      inventoryService.recordCount.and.returnValue(of({
        success: true,
        data: {
          productId: 'p-lot',
          productName: 'Produit lot',
          previousQuantity: 0,
          countedQuantity: 7,
          difference: 7,
          humanMessage: 'ok'
        },
        message: null,
        errors: []
      } as any));
      inventoryService.getActiveInventory.and.returnValue(of({
        success: true,
        data: makeInventory([
          makeProduct({
            productId: 'p-lot',
            theoreticalQuantity: 0,
            trackingMode: TRACKING_MODE_LOT
          })
        ]),
        message: null,
        errors: []
      } as any));

      component.activeInventory.set(makeInventory([
        makeProduct({
          productId: 'p-lot',
          theoreticalQuantity: 0,
          trackingMode: TRACKING_MODE_LOT
        })
      ]));
      component.currentProductIndex.set(0);
      component.countedQuantity.set(7);
      component.lotNumber.set('LOT-OPEN-1');

      component.submitCount();

      expect(inventoryService.recordCount).toHaveBeenCalledWith('inv-1', {
        productId: 'p-lot',
        countedQuantity: 7,
        lotNumber: 'LOT-OPEN-1'
      });
    });

    it('blocks submit when lot-tracked opening line has variance but no lot number', () => {
      component.activeInventory.set(makeInventory([
        makeProduct({
          productId: 'p-lot',
          theoreticalQuantity: 0,
          trackingMode: TRACKING_MODE_LOT
        })
      ]));
      component.currentProductIndex.set(0);
      component.countedQuantity.set(7);
      component.lotNumber.set('');

      component.submitCount();

      expect(inventoryService.recordCount).not.toHaveBeenCalled();
    });

    it('treats API string trackingMode Lot as a lot-tracked opening line', () => {
      inventoryService.recordCount.and.returnValue(of({
        success: true,
        data: {
          productId: 'p-lot',
          productName: 'cardoc',
          previousQuantity: 0,
          countedQuantity: 7,
          difference: 7,
          humanMessage: 'ok'
        },
        message: null,
        errors: []
      } as any));
      inventoryService.getActiveInventory.and.returnValue(of({
        success: true,
        data: makeInventory([
          makeProduct({
            productId: 'p-lot',
            productName: 'cardoc',
            theoreticalQuantity: 0,
            trackingMode: 'Lot'
          })
        ]),
        message: null,
        errors: []
      } as any));

      component.activeInventory.set(makeInventory([
        makeProduct({
          productId: 'p-lot',
          productName: 'cardoc',
          theoreticalQuantity: 0,
          trackingMode: 'Lot'
        })
      ]));
      component.currentProductIndex.set(0);
      component.countedQuantity.set(7);
      component.lotNumber.set('');

      expect(component.needsOpeningLotForProduct(component.currentProduct()!)).toBeTrue();
      expect(component.requiresLotNumberForCurrentCount()).toBeTrue();

      component.submitCount();
      expect(inventoryService.recordCount).not.toHaveBeenCalled();

      component.lotNumber.set('LOT-OPEN-1');
      component.submitCount();
      expect(inventoryService.recordCount).toHaveBeenCalledWith('inv-1', {
        productId: 'p-lot',
        countedQuantity: 7,
        lotNumber: 'LOT-OPEN-1'
      });
    });
  });

  describe('goToSummary', () => {
    it('opens the summary while products remain uncounted', () => {
      inventoryService.getSummary.and.returnValue(of({
        success: true,
        data: {
          inventoryId: 'inv-1',
          totalProducts: 2,
          countedProducts: 1,
          productsOk: 1,
          productsWithDifference: 0,
          productsNotCounted: 1,
          canValidate: true,
          statusMessage: 'Prêt à valider. 1 article non saisi sera confirmé à la quantité système.',
          productsOkList: [],
          productsWithDifferenceList: [],
          productsNotCountedList: [{
            productId: 'b',
            productName: 'Produit B',
            productCode: 'PB',
            theoreticalQuantity: 3,
            countedQuantity: null,
            difference: 0,
            humanMessage: 'Non compté',
            differenceClass: 'neutral'
          }]
        },
        message: null,
        errors: []
      }));

      component.activeInventory.set(makeInventory([
        makeProduct({ productId: 'a', isCounted: true, countedQuantity: 5 }),
        makeProduct({ productId: 'b', isCounted: false, theoreticalQuantity: 3 })
      ]));

      component.goToSummary();

      expect(component.step()).toBe('summary');
      expect(component.inventorySummary()?.canValidate).toBeTrue();
      expect(component.inventorySummary()?.productsNotCounted).toBe(1);
    });
  });

  describe('validateInventory', () => {
    function makeSummary(overrides: Record<string, unknown> = {}) {
      return {
        inventoryId: 'inv-1',
        totalProducts: 1,
        countedProducts: 1,
        productsOk: 0,
        productsWithDifference: 1,
        productsNotCounted: 0,
        canValidate: false,
        statusMessage: 'Impossible de valider. Le numéro de lot est obligatoire pour « cardoc » (article suivi par lot).',
        productsOkList: [],
        productsWithDifferenceList: [{
          productId: 'cardoc',
          productName: 'cardoc',
          productCode: 'CARD',
          theoreticalQuantity: 0,
          countedQuantity: 5,
          difference: 5,
          humanMessage: '+5 unités trouvées',
          differenceClass: 'positive' as const,
          trackingMode: 'Lot'
        }],
        productsNotCountedList: [],
        ...overrides
      };
    }

    it('does not POST when a lot-tracked variance line has no lot number', () => {
      component.activeInventory.set(makeInventory([
        makeProduct({
          productId: 'cardoc',
          productName: 'cardoc',
          theoreticalQuantity: 0,
          isCounted: true,
          countedQuantity: 5,
          trackingMode: 'Lot'
        })
      ]));
      component.inventorySummary.set(makeSummary() as any);

      component.validateInventory();

      expect(inventoryService.validateInventory).not.toHaveBeenCalled();
    });

    it('navigates to the first product missing a lot', () => {
      component.activeInventory.set(makeInventory([
        makeProduct({ productId: 'shirt', productName: 'Chemise', isCounted: true, countedQuantity: 4 }),
        makeProduct({
          productId: 'cardoc',
          productName: 'cardoc',
          theoreticalQuantity: 0,
          isCounted: true,
          countedQuantity: 5,
          trackingMode: 'Lot'
        })
      ]));
      component.inventorySummary.set(makeSummary() as any);
      component.step.set('summary');

      component.goToFirstMissingLot();

      expect(component.step()).toBe('count');
      expect(component.currentProduct()?.productId).toBe('cardoc');
      expect(component.countedQuantity()).toBe(5);
    });
  });
});
