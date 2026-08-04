import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { SalesOrderCreateComponent } from './sales-order-create.component';
import { SalesOrderService } from '@core/services/sales-order.service';
import { PricingService } from '@core/services/pricing.service';
import { DocumentLinePricingService, EMPTY_LINE_PROMOTION } from '@shared/utils/document-line-pricing.helper';
import { ClientService } from '@core/services/client.service';
import { ProductService } from '@core/services/product.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { formatLocalDate } from '@core/utils/date.util';

describe('SalesOrderCreateComponent', () => {
  let component: SalesOrderCreateComponent;
  let fixture: ComponentFixture<SalesOrderCreateComponent>;
  let createSalesOrderSpy: jasmine.Spy;
  let toastAddSpy: jasmine.Spy;

  const mockProduct = {
    id: 'prod-1',
    code: 'P001',
    name: 'Produit test',
    label: 'P001 — Produit test',
    unitPrice: 100
  };

  const mockClient = { id: 'client-1', label: 'Client test' };

  beforeEach(async () => {
    createSalesOrderSpy = jasmine.createSpy('createSalesOrder').and.returnValue(
      of({ data: 'order-1', success: true })
    );
    toastAddSpy = jasmine.createSpy('add');

    await TestBed.configureTestingModule({
      imports: [SalesOrderCreateComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: SalesOrderService,
          useValue: { createSalesOrder: createSalesOrderSpy }
        },
        {
          provide: PricingService,
          useValue: {
            resolve: () =>
              of({
                data: { unitPriceHT: 95, source: 'Catalog', currency: 'TND', isNegotiated: false },
                success: true
              })
          }
        },
        DocumentLinePricingService,
        {
          provide: ClientService,
          useValue: {
            getClients: () => of({ data: { items: [] } })
          }
        },
        {
          provide: ProductService,
          useValue: {
            getProducts: () => of({ data: { items: [] } })
          }
        },
        { provide: ToastService, useValue: { add: toastAddSpy } },
        {
          provide: ErrorHandlerService,
          useValue: {
            extractErrorMessage: () => 'Erreur',
            logError: jasmine.createSpy('logError')
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SalesOrderCreateComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('canSubmit is false without client', () => {
    component.newProduct = mockProduct;
    component.newQuantity = 1;
    component.addLine();

    expect(component.canSubmit()).toBeFalse();
  });

  it('canSubmit is false with client but no lines', () => {
    component.selectedClient = mockClient;

    expect(component.canSubmit()).toBeFalse();
  });

  it('canSubmit is true with client, line and order date', () => {
    component.selectedClient = mockClient;
    component.newProduct = mockProduct;
    component.newQuantity = 2;
    component.addLine();

    expect(component.canSubmit()).toBeTrue();
  });

  it('canSubmit becomes true when client is selected after adding a line (reactivity regression)', () => {
    component.newProduct = mockProduct;
    component.newQuantity = 1;
    component.addLine();
    expect(component.canSubmit()).toBeFalse();

    component.selectedClient = mockClient;
    fixture.detectChanges();

    expect(component.canSubmit()).toBeTrue();
  });

  it('lineTotal applies discount percent', () => {
    const total = component.lineTotal({
      product: mockProduct,
      quantity: 10,
      unitPrice: 100,
      discountPercent: 10,
      notes: null,
      priceSource: null,
      priceOverridden: false,
      ...EMPTY_LINE_PROMOTION
    });

    expect(total).toBe(900);
  });

  it('getOrderDateString uses local calendar date', () => {
    component.orderDateModel = new Date(2026, 5, 18); // 18/06/2026 local

    expect(component.getOrderDateString()).toBe('2026-06-18');
    expect(component.getOrderDateString()).toBe(formatLocalDate(component.orderDateModel));
  });

  it('submit does not call service when canSubmit is false', () => {
    component.submit();

    expect(createSalesOrderSpy).not.toHaveBeenCalled();
  });

  it('submit shows warning when delivery date is before order date', () => {
    component.selectedClient = mockClient;
    component.orderDateModel = new Date(2026, 7, 10);
    component.expectedDeliveryDateModel = new Date(2026, 7, 1);
    component.newProduct = mockProduct;
    component.newQuantity = 1;
    component.addLine();

    component.submit();

    expect(createSalesOrderSpy).not.toHaveBeenCalled();
    expect(toastAddSpy).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'warn', summary: 'Date invalide' })
    );
  });

  it('submit calls service with formatted dates when valid', () => {
    component.selectedClient = mockClient;
    component.orderDateModel = new Date(2026, 0, 8);
    component.expectedDeliveryDateModel = new Date(2026, 8, 8);
    component.reference = 'REF-1';
    component.newProduct = mockProduct;
    component.newQuantity = 3;
    component.addLine();

    component.submit();

    expect(createSalesOrderSpy).toHaveBeenCalledWith(
      jasmine.objectContaining({
        clientId: 'client-1',
        orderDate: '2026-01-08',
        expectedDeliveryDate: '2026-09-08',
        reference: 'REF-1',
        lines: jasmine.arrayContaining([
          jasmine.objectContaining({ productId: 'prod-1', quantity: 3, unitPrice: 0 })
        ])
      })
    );
  });

  it('submitBlockers lists missing client and lines', () => {
    expect(component.submitBlockers()).toContain('Sélectionnez un client');
    expect(component.submitBlockers()).toContain('Ajoutez au moins une ligne produit');
    expect(component.hasOrderDate()).toBeTrue();
  });

  it('navigates to detail after successful creation', () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate');

    component.selectedClient = mockClient;
    component.newProduct = mockProduct;
    component.newQuantity = 1;
    component.addLine();
    component.submit();

    expect(navigateSpy).toHaveBeenCalledWith(['/sales-orders', 'order-1']);
  });
});
