import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProjectBillingTabComponent } from './project-billing.tab';
import { BillableProjectTask } from '../project-api.service';

describe('ProjectBillingTabComponent', () => {
  let component: ProjectBillingTabComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectBillingTabComponent]
    }).compileComponents();

    const fixture: ComponentFixture<ProjectBillingTabComponent> = TestBed.createComponent(ProjectBillingTabComponent);
    component = fixture.componentInstance;
  });

  it('canInvoiceTasks requires selected tasks with amounts in fixed mode', () => {
    component.taskBillingMethod = 'fixed';
    component.billableTasks = [{ id: 't1', title: 'A', uninvoicedBillableHours: 0, hourlyRate: 0, previewAmountHt: 0, isEligible: true }];
    component.toggleTaskSelected('t1', true);
    expect(component.canInvoiceTasks).toBe(false);

    component.taskAmounts['t1'] = 100;
    expect(component.canInvoiceTasks).toBe(true);
  });

  it('canInvoiceTasks requires eligible tasks in hourly mode', () => {
    component.taskBillingMethod = 'hourly';
    component.billableTasks = [
      { id: 't1', title: 'A', uninvoicedBillableHours: 5, hourlyRate: 80, previewAmountHt: 400, isEligible: true },
      { id: 't2', title: 'B', uninvoicedBillableHours: 2, hourlyRate: 0, previewAmountHt: 0, isEligible: false, blockReason: 'Pas de tarif' }
    ] as BillableProjectTask[];

    component.toggleTaskSelected('t1', true);
    expect(component.canInvoiceTasks).toBe(true);

    component.toggleTaskSelected('t2', true);
    expect(component.canInvoiceTasks).toBe(false);
  });

  it('emitInvoiceTime always uses member grouping', () => {
    const spy = jasmine.createSpy('invoiceTime');
    component.invoiceTime.subscribe(spy);
    component.timeGroupBy = 'task';
    component.timeNotes = 'note';
    component.emitInvoiceTime();
    expect(spy).toHaveBeenCalledWith({ groupBy: 'member', notes: 'note' });
  });

  it('emitInvoiceTasks builds payload for fixed mode', () => {
    const spy = jasmine.createSpy('invoiceTasks');
    component.invoiceTasks.subscribe(spy);
    component.taskBillingMethod = 'fixed';
    component.toggleTaskSelected('t1', true);
    component.taskAmounts['t1'] = 250;
    component.timeNotes = 'forfait';
    component.emitInvoiceTasks();
    expect(spy).toHaveBeenCalledWith({
      method: 'fixed',
      notes: 'forfait',
      tasks: [{ taskId: 't1', amountHt: 250 }]
    });
  });
});
