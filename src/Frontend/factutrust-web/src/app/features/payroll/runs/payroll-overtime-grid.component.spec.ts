import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PayrollOvertimeGridComponent } from './payroll-overtime-grid.component';
import { PayrollService } from '@core/services/payroll.service';
import { EmployeeService } from '@core/services/employee.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { of } from 'rxjs';

describe('PayrollOvertimeGridComponent', () => {
  let fixture: ComponentFixture<PayrollOvertimeGridComponent>;

  const employees = [
    { id: 'e48', fullName: 'Salarié 48h', currentBaseSalary: 2080, currentWeeklyRegime: 'FortyEightHours' },
    { id: 'e40', fullName: 'Salarié 40h', currentBaseSalary: 1733.3, currentWeeklyRegime: 'FortyHours' }
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PayrollOvertimeGridComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PayrollService, useValue: {
          listOvertime: () => of({ success: true, data: [] }),
          getParameters: () => of({ success: true, data: { enableExtendedOvertimeRates: false } }),
          previewOvertime: () => of({ success: true, data: { hourlyRate: 10, computedAmount: 25, effectiveAmount: 25, isOverridden: false } })
        } },
        { provide: EmployeeService, useValue: { list: () => of({ success: true, data: { items: employees } }) } },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },
        { provide: ConfirmationService, useValue: jasmine.createSpyObj('ConfirmationService', ['confirm']) }
      ]
    });
    fixture = TestBed.createComponent(PayrollOvertimeGridComponent);
    fixture.componentInstance.year = 2026;
    fixture.componentInstance.month = 7;
    fixture.componentInstance.readOnly = true;
    fixture.detectChanges();
  });

  it('hides add button in readOnly mode', () => {
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Ajouter une ligne');
  });

  it('shows section title for overtime', () => {
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Heures supplémentaires');
  });

  it('proposes the legal rate for a 48h-regime employee (175 %)', () => {
    const cmp = fixture.componentInstance;
    cmp.formEmployeeId = 'e48';
    cmp.onEmployeeChange();
    expect(cmp.formRatePercent).toBe(175);
    expect(cmp.divisorLabel).toBe('208');
  });

  it('proposes the legal rate for a 40h-regime employee (125 %) with divisor 173,33', () => {
    const cmp = fixture.componentInstance;
    cmp.formEmployeeId = 'e40';
    cmp.onEmployeeChange();
    expect(cmp.formRatePercent).toBe(125);
    expect(cmp.divisorLabel).toBe('173,33');
  });

  it('offers 175 % without extended rates only for the 48h regime', () => {
    const cmp = fixture.componentInstance;
    cmp.formEmployeeId = 'e48';
    expect(cmp.rateOptions.some(o => o.value === 175)).toBeTrue();
    cmp.formEmployeeId = 'e40';
    expect(cmp.rateOptions.some(o => o.value === 175)).toBeFalse();
  });
});
