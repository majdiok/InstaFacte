import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { EmployeeDepartureDialogComponent } from './employee-departure-dialog.component';
import { EmployeeService } from '@core/services/employee.service';
import { ToastService } from '@core/services/toast.service';

describe('EmployeeDepartureDialogComponent', () => {
  let fixture: ComponentFixture<EmployeeDepartureDialogComponent>;
  let employees: jasmine.SpyObj<EmployeeService>;

  beforeEach(() => {
    employees = jasmine.createSpyObj('EmployeeService', ['terminate']);
    employees.terminate.and.returnValue(of({
      success: true,
      data: { isActive: false, terminationDate: '2026-08-01', warning: 'Test warning' }
    }));

    TestBed.configureTestingModule({
      imports: [EmployeeDepartureDialogComponent],
      providers: [
        provideNoopAnimations(),
        { provide: EmployeeService, useValue: employees },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) }
      ]
    });

    fixture = TestBed.createComponent(EmployeeDepartureDialogComponent);
    fixture.componentInstance.employeeId = 'e1';
    fixture.componentInstance.visible = true;
    fixture.detectChanges();
  });

  it('submits termination with closeActiveContract', () => {
    fixture.componentInstance.terminationDate = new Date(2026, 7, 1);
    fixture.componentInstance.closeActiveContract = true;
    fixture.componentInstance.submit();

    expect(employees.terminate).toHaveBeenCalledWith('e1', jasmine.objectContaining({
      closeActiveContract: true,
      deactivateNow: true
    }));
  });
});
