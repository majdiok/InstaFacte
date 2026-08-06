import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { EmployeeSuspensionsTabComponent } from './employee-suspensions-tab.component';
import { PayrollService } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';

describe('EmployeeSuspensionsTabComponent', () => {
  let fixture: ComponentFixture<EmployeeSuspensionsTabComponent>;

  beforeEach(() => {
    const payroll = jasmine.createSpyObj('PayrollService', [
      'listSuspensions', 'createSuspension', 'updateSuspension', 'deleteSuspension', 'approveSuspension'
    ]);
    payroll.listSuspensions.and.returnValue(of({ success: true, data: [] }));

    TestBed.configureTestingModule({
      imports: [EmployeeSuspensionsTabComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PayrollService, useValue: payroll },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },
        { provide: ConfirmationService, useValue: jasmine.createSpyObj('ConfirmationService', ['confirm']) }
      ]
    });
    fixture = TestBed.createComponent(EmployeeSuspensionsTabComponent);
    fixture.componentInstance.employeeId = 'e1';
    fixture.componentInstance.readOnly = true;
    fixture.detectChanges();
  });

  it('does not show add button when readOnly', () => {
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Ajouter une suspension');
  });
});
