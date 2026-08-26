import { signal } from '@angular/core';
import { StockVoucherFormComponent } from './stock-voucher-form.component';

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
