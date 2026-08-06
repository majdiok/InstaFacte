import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { PayrollJournalComponent } from './payroll-journal.component';
import { PayrollService, type PayrollJournal } from '@core/services/payroll.service';
import type { ApiResponse } from '@core/services/employee.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';

function journalFixture(overrides: Partial<PayrollJournal> = {}): PayrollJournal {
  return {
    payrollRunId: 'run-1',
    year: 2026,
    month: 3,
    periodLabel: 'Mars 2026',
    status: 'Validated',
    statusDisplay: 'Validé',
    isProvisional: false,
    employeeCount: 1,
    totalGross: 2000,
    totalCnssableGross: 2000,
    totalCnssEmployee: 183.6,
    totalProfessionalExpenses: 181.64,
    totalFamilyDeductions: 0,
    totalNetTaxable: 1634.76,
    totalIrpp: 250,
    totalIrppRegularization: 0,
    totalCss: 8.174,
    totalCssRegularization: 0,
    totalOtherDeductions: 0,
    totalNonTaxableAllowances: 0,
    totalNetSalary: 1558.226,
    totalCnssEmployer: 331.4,
    totalWorkAccident: 8,
    totalTfp: 40,
    totalFoprolos: 20,
    totalCssEmployer: 10,
    totalEmployerCharges: 409.4,
    totalEmployerCost: 2409.4,
    totalDebit: 2409.4,
    totalCredit: 2409.4,
    isBalanced: true,
    accountingLinesArePosted: true,
    accountingEntryNumber: 42,
    accountingEntryDate: '2026-03-31T00:00:00',
    accountingJournalCode: 'JOD',
    lines: [
      {
        payslipId: 'slip-1',
        employeeId: 'emp-1',
        employeeNumber: 'EMP001',
        employeeName: 'Ahmed Ben Ali',
        cnssNumber: '1234567890',
        grossSalary: 2000,
        cnssableGross: 2000,
        cnssEmployee: 183.6,
        professionalExpenses: 181.64,
        familyDeductions: 0,
        monthlyNetTaxable: 1634.76,
        irpp: 250,
        irppRegularization: 0,
        css: 8.174,
        cssRegularization: 0,
        otherDeductions: 0,
        nonTaxableAllowances: 0,
        netSalary: 1558.226,
        cnssEmployer: 331.4,
        workAccidentContribution: 8,
        tfp: 40,
        foprolos: 20,
        cssEmployer: 10,
        totalEmployerCharges: 409.4,
        totalCost: 2409.4
      }
    ],
    accountingLines: [
      { accountNumber: '640', accountLabel: 'Charges de personnel', label: 'Paie 03/2026', debit: 2000, credit: 0 },
      { accountNumber: '421', accountLabel: 'Personnel — rémunérations dues', label: 'Paie 03/2026', debit: 0, credit: 1558.226 }
    ],
    ...overrides
  };
}

describe('PayrollJournalComponent', () => {
  let payrollStub: jasmine.SpyObj<PayrollService>;

  async function setup(
    journal: PayrollJournal | null = journalFixture(),
    permissions: string[] = [PERMISSIONS.payroll.read, PERMISSIONS.payroll.export]
  ): Promise<ComponentFixture<PayrollJournalComponent>> {
    payrollStub = jasmine.createSpyObj<PayrollService>('PayrollService', ['getPayrollJournal', 'exportPayrollJournal']);
    payrollStub.getPayrollJournal.and.returnValue(of({ success: true, data: journal } as ApiResponse<PayrollJournal>));
    payrollStub.exportPayrollJournal.and.returnValue(of(new Blob(['x'])));

    await TestBed.configureTestingModule({
      imports: [PayrollJournalComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PayrollService, useValue: payrollStub },
        { provide: ToastService, useValue: jasmine.createSpyObj<ToastService>('ToastService', ['add']) },
        {
          provide: AuthService,
          useValue: {
            hasPermission: (p: string) => permissions.includes(p),
            hasAnyPermission: () => true,
            hasModule: () => true,
            isAccountingFirm: () => false,
            isDelegatedMode: () => false
          }
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({ year: '2026', month: '3' }) } }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PayrollJournalComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('lit la période depuis les paramètres de requête', async () => {
    const fixture = await setup();

    expect(fixture.componentInstance.year).toBe(2026);
    expect(fixture.componentInstance.month).toBe(3);
    expect(payrollStub.getPayrollJournal).toHaveBeenCalledWith(2026, 3, false);
  });

  it('affiche le détail par salarié et ses totaux', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Ahmed Ben Ali');
    expect(text).toContain('Totaux — 1 salarié(s)');
  });

  it('rappelle le numéro de l’écriture comptabilisée', async () => {
    const fixture = await setup();

    expect(fixture.componentInstance.accountingOrigin()).toContain('n° 42');
    expect(fixture.componentInstance.accountingOrigin()).toContain('JOD');
  });

  it('signale une ventilation simulée quand le cycle n’est pas comptabilisé', async () => {
    const fixture = await setup(
      journalFixture({ accountingLinesArePosted: false, accountingEntryNumber: undefined, accountingJournalCode: undefined })
    );

    expect(fixture.componentInstance.accountingOrigin()).toContain('simulée');
  });

  it('affiche un bandeau de contrôle si débit ≠ crédit', async () => {
    const fixture = await setup(journalFixture({ isBalanced: false, totalCredit: 2000 }));

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('total débit ≠ total crédit');
  });

  it('affiche un bandeau provisoire pour un cycle calculé', async () => {
    const fixture = await setup(journalFixture({ isProvisional: true, status: 'Calculated', statusDisplay: 'Calculé' }));

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('État provisoire');
  });

  it('exporte la vue correspondant à l’onglet actif', async () => {
    const fixture = await setup();

    fixture.componentInstance.onExport('excel');
    expect(payrollStub.exportPayrollJournal).toHaveBeenCalledWith(2026, 3, false, 'excel', 'ByEmployee');

    fixture.componentInstance.activeTabIndex = 1;
    fixture.componentInstance.onExport('pdf');
    expect(payrollStub.exportPayrollJournal).toHaveBeenCalledWith(2026, 3, false, 'pdf', 'Accounting');
  });

  it('masque le menu d’export sans la permission payroll:export', async () => {
    const fixture = await setup(journalFixture(), [PERMISSIONS.payroll.read]);

    expect(fixture.componentInstance.canExport()).toBeFalse();
    expect(fixture.nativeElement.querySelector('app-accounting-export-menu')).toBeNull();
  });

  it('remonte le message métier quand aucun cycle n’existe pour la période', async () => {
    payrollStub = jasmine.createSpyObj<PayrollService>('PayrollService', ['getPayrollJournal', 'exportPayrollJournal']);
    payrollStub.getPayrollJournal.and.returnValue(
      throwError(() => ({ error: { message: 'Aucun cycle de paie pour Mars 2026.' } }))
    );

    await TestBed.configureTestingModule({
      imports: [PayrollJournalComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PayrollService, useValue: payrollStub },
        { provide: ToastService, useValue: jasmine.createSpyObj<ToastService>('ToastService', ['add']) },
        {
          provide: AuthService,
          useValue: {
            hasPermission: () => true,
            hasAnyPermission: () => true,
            hasModule: () => true,
            isAccountingFirm: () => false,
            isDelegatedMode: () => false
          }
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({}) } }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PayrollJournalComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toContain('Aucun cycle de paie');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Aucun cycle de paie');
  });
});
