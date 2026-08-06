import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { PayrollRunDetailComponent } from './payroll-run-detail.component';
import { PayrollService, type PayrollRunDetail } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';

describe('PayrollRunDetailComponent', () => {
  let fixture: ComponentFixture<PayrollRunDetailComponent>;

  const mockRun: PayrollRunDetail = {
    id: 'run-1',
    year: 2026,
    month: 7,
    label: 'Paie 07/2026',
    status: 'Calculated',
    statusDisplay: 'Calculé',
    parametersFiscalYear: 2026,
    totalGross: 1467,
    totalCnssEmployee: 134.671,
    totalIrpp: 143.524,
    totalCss: 0,
    totalNet: 1143.018,
    totalCnssEmployer: 243.082,
    totalTfp: 0,
    totalFoprolos: 0,
    totalCssEmployer: 0,
    totalWorkAccident: 0,
    payslips: [],
    overtimeLines: []
  };

  function setup(perms: string[], runOverrides: Partial<PayrollRunDetail> = {}) {
    const run = { ...mockRun, ...runOverrides };
    TestBed.configureTestingModule({
      imports: [PayrollRunDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: PayrollService, useValue: {
          getRun: () => of({ success: true, data: run }),
          getParameters: () => of({ success: true, data: { enableExtendedOvertimeRates: false } }),
          listOvertime: () => of({ success: true, data: [] }),
          listIrppRegularizations: () => of({ success: true, data: [] }),
          // Requis par PayrollPaymentsPanelComponent, monté par le détail de cycle.
          listRunPayments: () => of({ success: true, data: [] }),
          calculateRun: jasmine.createSpy('calculateRun'),
          validateRun: jasmine.createSpy('validateRun'),
          reopenRun: jasmine.createSpy('reopenRun'),
          closeRun: jasmine.createSpy('closeRun'),
          getPayslip: jasmine.createSpy('getPayslip'),
          downloadPayslipPdf: jasmine.createSpy('downloadPayslipPdf'),
          getBankTransferPreview: jasmine.createSpy('getBankTransferPreview'),
          exportBankTransfer: jasmine.createSpy('exportBankTransfer')
        } },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },
        { provide: AuthService, useValue: {
          hasPermission: (p: string) => perms.includes(p),
          isAccountingFirm: () => false,
          isDelegatedMode: () => false
        } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'run-1' } } } }
      ]
    });
    fixture = TestBed.createComponent(PayrollRunDetailComponent);
    fixture.detectChanges();
  }

  it('shows Calculer when user has payroll:run', () => {
    setup([PERMISSIONS.payroll.run]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Calculer');
  });

  it('hides Calculer when user lacks payroll:run', () => {
    setup([PERMISSIONS.payroll.read]);
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Calculer');
  });

  it('shows Valider when user has payroll:validate and status Calculated', () => {
    setup([PERMISSIONS.payroll.validate]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Valider');
  });

  it('shows Export virement when Validated and payroll:export', () => {
    setup([PERMISSIONS.payroll.export], { status: 'Validated', statusDisplay: 'Validé' });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Export virement');
  });

  it('hides Export virement when Calculated even with payroll:export', () => {
    setup([PERMISSIONS.payroll.export], { status: 'Calculated' });
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Export virement');
  });

  it('hides Export virement when Validated without payroll:export', () => {
    setup([PERMISSIONS.payroll.read], { status: 'Validated', statusDisplay: 'Validé' });
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Export virement');
  });
});
