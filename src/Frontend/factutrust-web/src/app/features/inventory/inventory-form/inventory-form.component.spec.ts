import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { InventoryFormComponent, InventoryFormLine } from './inventory-form.component';
import {
    InventoryService,
    InventoryType,
    ActiveInventoryDto,
    InventoryProductItem
} from '@core/services/inventory.service';
import { StockService } from '@core/services/stock.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { ConfirmationService } from '@core/services/confirmation.service';

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
        warehouseName: 'Depôt Monastir',
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

function makeLine(
    product: InventoryProductItem,
    countedInput: number,
    lastSavedCount: number | null
): InventoryFormLine {
    return {
        ...product,
        countedInput,
        lastSavedCount,
        lotNumberInput: product.lotNumber ?? ''
    };
}

describe('InventoryFormComponent', () => {
    let fixture: ComponentFixture<InventoryFormComponent>;
    let component: InventoryFormComponent;
    let inventoryService: jasmine.SpyObj<InventoryService>;
    let stockService: jasmine.SpyObj<StockService>;
    let confirmationService: { confirm: jasmine.Spy };

    beforeEach(async () => {
        inventoryService = jasmine.createSpyObj('InventoryService', [
            'getActiveInventory',
            'startInventory',
            'recordCount',
            'validateInventory',
            'cancelInventory'
        ]);
        stockService = jasmine.createSpyObj('StockService', ['getWarehouses']);
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

        await TestBed.configureTestingModule({
            imports: [InventoryFormComponent],
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                provideRouter([]),
                provideNoopAnimations(),
                { provide: InventoryService, useValue: inventoryService },
                { provide: StockService, useValue: stockService },
                { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
                { provide: ErrorHandlerService, useValue: { extractErrorMessage: () => null, logError: () => undefined } },
                { provide: ConfirmationService, useValue: confirmationService }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(InventoryFormComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    describe('canValidate', () => {
        it('is true when some products are not yet counted', () => {
            const products = [
                makeProduct({ productId: 'a', isCounted: true, countedQuantity: 8 }),
                makeProduct({ productId: 'b', isCounted: false })
            ];
            component.activeInventory.set(makeInventory(products));
            component.lines.set([
                makeLine(products[0], 8, 8),
                makeLine(products[1], 10, null)
            ]);

            expect(component.canValidate()).toBeTrue();
        });

        it('is false without an active inventory', () => {
            component.activeInventory.set(null);
            component.lines.set([]);

            expect(component.canValidate()).toBeFalse();
        });
    });

    describe('validateInventory', () => {
        it('sends only dirty lines as pending counts', () => {
            confirmationService.confirm.and.callFake((cfg: { accept?: () => void }) => cfg.accept?.());
            inventoryService.validateInventory.and.returnValue(of({
                success: true,
                data: { inventoryId: 'inv-1', productsAdjusted: 1, humanMessage: 'ok' },
                message: null,
                errors: []
            }));

            const products = [
                makeProduct({ productId: 'a', theoreticalQuantity: 10, isCounted: false }),
                makeProduct({ productId: 'b', theoreticalQuantity: 5, isCounted: false })
            ];
            component.activeInventory.set(makeInventory(products));
            component.lines.set([
                makeLine(products[0], 10, null),
                makeLine(products[1], 8, null)
            ]);

            component.validateInventory();

            expect(inventoryService.validateInventory).toHaveBeenCalledWith('inv-1', {
                pendingCounts: [{ productId: 'b', countedQuantity: 8 }]
            });
        });

        it('blocks validate when a lot-tracked variance line has no lot number', () => {
            const products = [
                makeProduct({
                    productId: 'cardoc',
                    productName: 'cardoc',
                    theoreticalQuantity: 0,
                    isCounted: true,
                    countedQuantity: 5,
                    trackingMode: 'Lot'
                })
            ];
            component.activeInventory.set(makeInventory(products));
            component.lines.set([
                { ...makeLine(products[0], 5, 5), lotNumberInput: '' }
            ]);

            component.validateInventory();

            expect(confirmationService.confirm).not.toHaveBeenCalled();
            expect(inventoryService.validateInventory).not.toHaveBeenCalled();
        });
    });

    describe('saveCounts', () => {
        it('records only dirty lines', () => {
            inventoryService.recordCount.and.returnValue(of({
                success: true,
                data: {
                    productId: 'b',
                    productName: 'Produit B',
                    previousQuantity: 5,
                    countedQuantity: 8,
                    difference: 3,
                    humanMessage: 'ok'
                },
                message: null,
                errors: []
            }));
            inventoryService.getActiveInventory.and.returnValue(of({
                success: true,
                data: makeInventory([makeProduct({ productId: 'b', isCounted: true, countedQuantity: 8 })]),
                message: null,
                errors: []
            }));

            const products = [
                makeProduct({ productId: 'a', theoreticalQuantity: 10, isCounted: false }),
                makeProduct({ productId: 'b', theoreticalQuantity: 5, isCounted: false, productName: 'Produit B' })
            ];
            component.activeInventory.set(makeInventory(products));
            component.lines.set([
                makeLine(products[0], 10, null),
                makeLine(products[1], 8, null)
            ]);

            component.saveCounts();

            expect(inventoryService.recordCount).toHaveBeenCalledTimes(1);
            expect(inventoryService.recordCount).toHaveBeenCalledWith('inv-1', {
                productId: 'b',
                countedQuantity: 8
            });
        });
    });
});
