import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PayrollVariableAllowanceGridComponent } from './payroll-variable-allowance-grid.component';
import { PayrollService } from '@core/services/payroll.service';
import { EmployeeService } from '@core/services/employee.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { of } from 'rxjs';

describe('PayrollVariableAllowanceGridComponent', () => {
  let fixture: ComponentFixture<PayrollVariableAllowanceGridComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PayrollVariableAllowanceGridComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: PayrollService,
          useValue: {
            listVariableAllowances: () => of({ success: true, data: [] })
          }
        },
        {
          provide: EmployeeService,
          useValue: { list: () => of({ success: true, data: { items: [] } }) }
        },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },
        { provide: ConfirmationService, useValue: jasmine.createSpyObj('ConfirmationService', ['confirm']) }
      ]
    });
    fixture = TestBed.createComponent(PayrollVariableAllowanceGridComponent);
    fixture.componentInstance.year = 2026;
    fixture.componentInstance.month = 8;
    fixture.componentInstance.readOnly = true;
    fixture.detectChanges();
  });

  it('hides add button in readOnly mode', () => {
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Ajouter une prime');
  });

  it('shows section title for variable allowances', () => {
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Primes mensuelles');
  });
});
