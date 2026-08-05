import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PayrollService } from '@core/services/payroll.service';
import { BankAccountService } from '@core/services/bank-account.service';
import { ToastService } from '@core/services/toast.service';
import { PayrollRecordPaymentDialogComponent } from './payroll-record-payment-dialog.component';

describe('PayrollRecordPaymentDialogComponent', () => {
  let component: PayrollRecordPaymentDialogComponent;
  let fixture: ComponentFixture<PayrollRecordPaymentDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PayrollRecordPaymentDialogComponent],
      providers: [
        {
          provide: PayrollService,
          useValue: {
            recordRunPayment: () => of({ success: true, data: 'id' })
          }
        },
        {
          provide: BankAccountService,
          useValue: { list: () => of({ success: true, data: [] }) }
        },
        { provide: ToastService, useValue: { add: () => {} } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PayrollRecordPaymentDialogComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('runId', 'run-1');
    fixture.componentRef.setInput('payslips', [
      {
        id: 'p1',
        employeeId: 'e1',
        employeeName: 'Alice',
        employeeNumber: '001',
        grossSalary: 2000,
        cnssEmployee: 100,
        irpp: 50,
        css: 0,
        netSalary: 1500,
        remainingToPay: 1500
      }
    ]);
    fixture.detectChanges();
  });

  it('should validate when row selected', () => {
    component.onOpen();
    expect(component.isValid()).toBe(false);
    component.bankAccountId = 'bank-1';
    expect(component.isValid()).toBe(true);
  });
});
