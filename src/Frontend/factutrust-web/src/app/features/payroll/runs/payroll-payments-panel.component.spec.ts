import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { PayrollPaymentsPanelComponent } from './payroll-payments-panel.component';
import { PayrollService, type PayrollPayment } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';

describe('PayrollPaymentsPanelComponent', () => {
  let fixture: ComponentFixture<PayrollPaymentsPanelComponent>;
  let payrollSpy: jasmine.SpyObj<PayrollService>;
  let confirmSpy: jasmine.SpyObj<ConfirmationService>;

  const payment: PayrollPayment = {
    id: 'pay-1',
    payrollRunId: 'run-1',
    amount: 1000,
    paymentDate: '2026-07-31',
    method: 'BankTransfer',
    methodDisplay: 'Virement',
    reference: 'VIR-1',
    isCancelled: false,
    createdAt: '2026-07-31T10:00:00Z',
    lines: []
  };

  beforeEach(() => {
    payrollSpy = jasmine.createSpyObj('PayrollService', ['listRunPayments', 'cancelPayment']);
    payrollSpy.listRunPayments.and.returnValue(of({ success: true, data: [payment] }));
    payrollSpy.cancelPayment.and.returnValue(of({ success: true, data: null }));
    confirmSpy = jasmine.createSpyObj('ConfirmationService', ['confirm', 'prompt']);

    TestBed.configureTestingModule({
      imports: [PayrollPaymentsPanelComponent],
      providers: [
        provideNoopAnimations(),
        { provide: PayrollService, useValue: payrollSpy },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },
        { provide: ConfirmationService, useValue: confirmSpy }
      ]
    });
    fixture = TestBed.createComponent(PayrollPaymentsPanelComponent);
    fixture.componentRef.setInput('runId', 'run-1');
    fixture.detectChanges();
  });

  it('loads payments on init', () => {
    expect(payrollSpy.listRunPayments).toHaveBeenCalledWith('run-1', true);
    expect(fixture.componentInstance.payments().length).toBe(1);
  });

  it('cancelOne aborts when the prompt returns no reason', async () => {
    confirmSpy.prompt.and.resolveTo(null);
    await fixture.componentInstance.cancelOne(payment);
    expect(payrollSpy.cancelPayment).not.toHaveBeenCalled();
  });

  it('cancelOne cancels the payment with the entered reason', async () => {
    confirmSpy.prompt.and.resolveTo('erreur bancaire');
    await fixture.componentInstance.cancelOne(payment);
    expect(payrollSpy.cancelPayment).toHaveBeenCalledWith('pay-1', 'erreur bancaire');
  });
});
