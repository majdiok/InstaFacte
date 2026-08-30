import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { PayrollRunListComponent } from './payroll-run-list.component';
import { PayrollService, type PayrollRunListItem } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { PERMISSIONS } from '@core/config/permission-keys';

describe('PayrollRunListComponent', () => {
  let fixture: ComponentFixture<PayrollRunListComponent>;
  let listRunsSpy: jasmine.Spy;
  let createRunSpy: jasmine.Spy;

  function currentMonthItem(overrides: Partial<PayrollRunListItem> = {}): PayrollRunListItem {
    const now = new Date();
    return {
      id: 'run-current',
      year: now.getFullYear(),
      month: now.getMonth() + 1,
      label: `Paie ${String(now.getMonth() + 1).padStart(2, '0')}/${now.getFullYear()}`,
      status: 'Validated',
      statusDisplay: 'VALIDE',
      payslipCount: 1,
      totalGross: 1300,
      totalNet: 1151.733,
      ...overrides
    };
  }

  function otherMonthItem(): PayrollRunListItem {
    const now = new Date();
    // Always same year as selectedYear default; pick a month that is not current.
    const month = now.getMonth() === 0 ? 2 : 1;
    return {
      id: 'run-other',
      year: now.getFullYear(),
      month,
      label: `Paie ${String(month).padStart(2, '0')}/${now.getFullYear()}`,
      status: 'Validated',
      statusDisplay: 'VALIDE',
      payslipCount: 1,
      totalGross: 1300,
      totalNet: 1151.733
    };
  }

  function setup(options: {
    perms?: string[];
    runs?: PayrollRunListItem[];
  } = {}): void {
    const perms = options.perms ?? [PERMISSIONS.payroll.run];
    const runs = options.runs ?? [];
    listRunsSpy = jasmine.createSpy('listRuns').and.returnValue(of({ success: true, data: runs }));
    createRunSpy = jasmine.createSpy('createRun').and.returnValue(of({ success: true, data: 'new-id' }));

    TestBed.configureTestingModule({
      imports: [PayrollRunListComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: PayrollService,
          useValue: {
            listRuns: listRunsSpy,
            createRun: createRunSpy
          }
        },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },
        {
          provide: AuthService,
          useValue: {
            hasPermission: (p: string) => perms.includes(p),
            isAccountingFirm: () => false,
            isDelegatedMode: () => false,
            isPayrollFirmManaged: () => false
          }
        },
        {
          provide: ErrorHandlerService,
          useValue: { extractErrorMessage: (err: unknown) => (err as { error?: { message?: string } })?.error?.message ?? 'Création impossible.' }
        }
      ]
    });
    fixture = TestBed.createComponent(PayrollRunListComponent);
    fixture.detectChanges();
  }

  it('shows Nouveau cycle when canRun and no current-month cycle', () => {
    setup({ runs: [otherMonthItem()] });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Nouveau cycle (mois courant)');
  });

  it('hides Nouveau cycle when current-month cycle already exists', () => {
    setup({ runs: [currentMonthItem(), otherMonthItem()] });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Nouveau cycle (mois courant)');
  });

  it('hides Nouveau cycle when user lacks payroll:run', () => {
    setup({ perms: [PERMISSIONS.payroll.read], runs: [] });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Nouveau cycle (mois courant)');
  });

  it('does not call createRun when current-month cycle already exists', () => {
    setup({ runs: [currentMonthItem()] });
    fixture.componentInstance.createCurrentMonth();
    expect(createRunSpy).not.toHaveBeenCalled();
  });
});
