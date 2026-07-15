import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { EmployeeLeavesTabComponent } from './employee-leaves-tab.component';
import { EmployeeService } from '@core/services/employee.service';
import { PayrollService } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { of } from 'rxjs';

describe('EmployeeLeavesTabComponent', () => {
  let fixture: ComponentFixture<EmployeeLeavesTabComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [EmployeeLeavesTabComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: EmployeeService, useValue: {
          listLeaves: () => of({ success: true, data: [] }),
          getLeaveBalance: () => of({ success: true, data: { employeeId: 'e1', year: 2026, openingBalance: 0, accruedInYear: 0, totalAcquired: 0, consumed: 0, remaining: 0 } })
        } },
        { provide: PayrollService, useValue: jasmine.createSpyObj('PayrollService', [
          'createLeave', 'approveLeave', 'deleteLeave', 'computeLeaveDays', 'setLeaveOpeningBalance'
        ]) },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },
        { provide: ConfirmationService, useValue: jasmine.createSpyObj('ConfirmationService', ['confirm']) }
      ]
    });
    fixture = TestBed.createComponent(EmployeeLeavesTabComponent);
    fixture.componentInstance.employeeId = 'e1';
    fixture.componentInstance.readOnly = true;
    fixture.detectChanges();
  });

  it('does not show add button when readOnly', () => {
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Ajouter un congé');
  });
});
