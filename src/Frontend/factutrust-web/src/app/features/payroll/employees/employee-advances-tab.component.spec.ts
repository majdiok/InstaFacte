import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { EmployeeAdvancesTabComponent } from './employee-advances-tab.component';
import { EmployeeService } from '@core/services/employee.service';
import { PayrollService } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { of } from 'rxjs';

describe('EmployeeAdvancesTabComponent', () => {
  let fixture: ComponentFixture<EmployeeAdvancesTabComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [EmployeeAdvancesTabComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: EmployeeService, useValue: { listAdvances: () => of({ success: true, data: [] }) } },
        { provide: PayrollService, useValue: jasmine.createSpyObj('PayrollService', ['createAdvance', 'deleteAdvance']) },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },
        { provide: ConfirmationService, useValue: jasmine.createSpyObj('ConfirmationService', ['confirm']) }
      ]
    });
    fixture = TestBed.createComponent(EmployeeAdvancesTabComponent);
    fixture.componentInstance.employeeId = 'e1';
    fixture.componentInstance.readOnly = true;
    fixture.detectChanges();
  });

  it('does not show add button when readOnly', () => {
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Nouvelle avance');
  });
});
