import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { PayrollBookComponent } from './payroll-book.component';
import { PayrollService, type PayrollBook } from '@core/services/payroll.service';
import type { ApiResponse } from '@core/services/employee.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';

function bookFixture(overrides: Partial<PayrollBook> = {}): PayrollBook {
  return {
    year: 2026,
    fromMonth: 1,
    toMonth: 3,
    periodLabel: 'Janvier à Mars 2026',
    includeCalculated: false,
    includedMonths: [1, 2, 3],
    missingMonths: [],
    provisionalMonths: [],
    isProvisional: false,
    employeeCount: 1,
    totalGross: 6000,
    totalCnssableGross: 6000,
    totalCnssEmployee: 550.8,
    totalProfessionalExpenses: 544.92,
    totalFamilyDeductions: 0,
    totalNetTaxable: 4904.28,
    totalIrpp: 800.15,
    totalIrppRegularization: 0,
    totalCss: 24.146,
    totalCssRegularization: 0,
    totalOtherDeductions: 0,
    totalNonTaxableAllowances: 0,
    totalNetSalary: 4624.904,
    totalCnssEmployer: 994.2,
    totalWorkAccident: 24,
    totalTfp: 120,
    totalFoprolos: 60,
    totalCssEmployer: 30,
    totalEmployerCharges: 1228.2,
    totalEmployerCost: 7228.2,
    lines: [
      {
        employeeId: 'emp-1',
        employeeNumber: 'EMP001',
        employeeName: 'Ahmed Ben Ali',
        cnssNumber: '1234567890',
        monthsCount: 3,
        grossSalary: 6000,
        cnssableGross: 6000,
        cnssEmployee: 550.8,
        professionalExpenses: 544.92,
        familyDeductions: 0,
        netTaxable: 4904.28,
        irpp: 800.15,
        irppRegularization: 0,
        css: 24.146,
        cssRegularization: 0,
        otherDeductions: 0,
        nonTaxableAllowances: 0,
        netSalary: 4624.904
      }
    ],
    ...overrides
  };
}

describe('PayrollBookComponent', () => {
  let payrollStub: jasmine.SpyObj<PayrollService>;

  async function setup(
    book: PayrollBook | null = bookFixture(),
    permissions: string[] = [PERMISSIONS.payroll.read, PERMISSIONS.payroll.export]
  ): Promise<ComponentFixture<PayrollBookComponent>> {
    payrollStub = jasmine.createSpyObj<PayrollService>('PayrollService', ['getPayrollBook', 'exportPayrollBook']);
    payrollStub.getPayrollBook.and.returnValue(of({ success: true, data: book } as ApiResponse<PayrollBook>));
    payrollStub.exportPayrollBook.and.returnValue(of(new Blob(['x'])));

    await TestBed.configureTestingModule({
      imports: [PayrollBookComponent],
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
          useValue: { snapshot: { queryParamMap: convertToParamMap({ year: '2026', fromMonth: '1', toMonth: '3' }) } }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PayrollBookComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('lit la période depuis les paramètres de requête et charge le livre', async () => {
    const fixture = await setup();

    expect(fixture.componentInstance.year).toBe(2026);
    expect(fixture.componentInstance.fromMonth).toBe(1);
    expect(fixture.componentInstance.toMonth).toBe(3);
    expect(payrollStub.getPayrollBook).toHaveBeenCalledWith(2026, 1, 3, false);
  });

  it('affiche les lignes et la ligne de totaux', async () => {
    const fixture = await setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Ahmed Ben Ali');
    expect(text).toContain('EMP001');
    expect(text).toContain('Totaux — 1 salarié(s)');
  });

  it('affiche un bandeau provisoire quand des cycles calculés sont inclus', async () => {
    const fixture = await setup(bookFixture({ isProvisional: true, provisionalMonths: [3] }));

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('État provisoire');
  });

  it('signale les mois sans cycle éligible', async () => {
    const fixture = await setup(bookFixture({ includedMonths: [1], missingMonths: [2, 3] }));

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Mois sans cycle éligible : Février, Mars');
  });

  it('masque le menu d’export sans la permission payroll:export', async () => {
    const fixture = await setup(bookFixture(), [PERMISSIONS.payroll.read]);

    expect(fixture.componentInstance.canExport()).toBeFalse();
    expect(fixture.nativeElement.querySelector('app-accounting-export-menu')).toBeNull();
  });

  it('expose le menu d’export avec la permission payroll:export', async () => {
    const fixture = await setup();

    expect(fixture.componentInstance.canExport()).toBeTrue();
    expect(fixture.nativeElement.querySelector('app-accounting-export-menu')).not.toBeNull();
  });

  it('refuse une plage de mois inversée sans appeler l’API', async () => {
    const fixture = await setup();
    payrollStub.getPayrollBook.calls.reset();

    fixture.componentInstance.fromMonth = 6;
    fixture.componentInstance.toMonth = 2;
    fixture.componentInstance.load();
    fixture.detectChanges();

    expect(payrollStub.getPayrollBook).not.toHaveBeenCalled();
    expect(fixture.componentInstance.rangeError()).toContain('ne peut pas précéder');
  });

  it('transmet le format choisi à l’export', async () => {
    const fixture = await setup();

    fixture.componentInstance.onExport('pdf');

    expect(payrollStub.exportPayrollBook).toHaveBeenCalledWith(2026, 1, 3, false, 'pdf');
  });

  it('affiche un état vide quand la période ne contient aucun bulletin', async () => {
    const fixture = await setup(bookFixture({ lines: [], employeeCount: 0 }));

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Aucun bulletin sur la période');
  });
});
