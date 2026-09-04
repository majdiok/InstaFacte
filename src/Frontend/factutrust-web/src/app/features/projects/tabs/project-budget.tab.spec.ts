import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { ProjectBudgetTabComponent } from './project-budget.tab';
import { ProjectPurchaseOrder } from '../project-api.service';

describe('ProjectBudgetTabComponent', () => {
  let fixture: ComponentFixture<ProjectBudgetTabComponent>;
  let component: ProjectBudgetTabComponent;

  const linkedPo: ProjectPurchaseOrder = {
    id: 'po-1',
    number: 'BC-2026-0001',
    supplierName: 'Acme',
    orderDate: '2026-09-01',
    status: 1,
    statusDisplay: 'Confirmé',
    statusCss: 'confirmed',
    totalHt: 1500.5
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectBudgetTabComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(ProjectBudgetTabComponent);
    component = fixture.componentInstance;
  });

  it('shows linked purchase orders table when canUpdate is false', () => {
    component.canUpdate = false;
    component.linkedPurchaseOrders = [linkedPo];
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('BC-2026-0001');
    expect(text).toContain('Acme');
    expect(text).toContain('Bons de commande');
    expect(text).not.toContain('Associer');
  });

  it('shows assign controls when canUpdate is true', () => {
    component.canUpdate = true;
    component.purchaseOrders = [{ id: 'po-2', name: 'BC-2026-0002 — Fournisseur' }];
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Associer');
  });

  it('emits assignPurchaseOrder and clears selection', () => {
    component.canUpdate = true;
    component.purchaseOrderId = 'po-2';
    const emitted: string[] = [];
    component.assignPurchaseOrder.subscribe(id => emitted.push(id));

    component.assignPo();

    expect(emitted).toEqual(['po-2']);
    expect(component.purchaseOrderId).toBe('');
  });

  it('shows empty message when no linked purchase orders', () => {
    component.linkedPurchaseOrders = [];
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Aucun bon de commande associé');
  });

  it('links purchase order number to detail route', () => {
    component.linkedPurchaseOrders = [linkedPo];
    fixture.detectChanges();
    const link = fixture.debugElement.query(By.css('a'));
    expect(link.attributes['href']).toContain('/purchase-orders/po-1');
  });
});
