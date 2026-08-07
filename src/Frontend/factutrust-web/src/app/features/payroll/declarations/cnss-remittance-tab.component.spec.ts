import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { CnssRemittanceTabComponent } from './cnss-remittance-tab.component';
import { PayrollService } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';

const baseRemittance = {
  year: 2026,
  month: 3,
  employerCompanyName: 'Test',
  employerNif: '1234567A',
  employerCnssNumber: '9988776655',
  isEligible: true,
  hasExistingPayment: false,
  totalCnssEmployee: 100,
  totalCnssEmployer: 200,
  totalWorkAccident: 10,
  totalDue: 310,
  employeeCount: 1,
  documentReference: 'BCNSS-202603',
  warnings: [] as string[],
  lines: [{
    employeeId: '1',
    employeeName: 'Alice',
    cnssableGross: 2000,
    cnssEmployee: 100,
    cnssEmployer: 200,
    workAccident: 10,
    lineTotal: 310,
    warnings: []
  }]
};

describe('CnssRemittanceTabComponent', () => {
  let fixture: ComponentFixture<CnssRemittanceTabComponent>;
  let payrollService: jasmine.SpyObj<Pick<PayrollService, 'getCnssRemittance'>>;

  async function setup(remittance = baseRemittance): Promise<void> {
    payrollService = jasmine.createSpyObj('PayrollService', ['getCnssRemittance']);
    payrollService.getCnssRemittance.and.returnValue(of({ success: true, data: remittance }));

    await TestBed.configureTestingModule({
      imports: [CnssRemittanceTabComponent],
      providers: [
        { provide: PayrollService, useValue: payrollService },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: AuthService, useValue: { hasPermission: () => true } },
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CnssRemittanceTabComponent);
    fixture.detectChanges();
  }

  it('should create and load remittance', async () => {
    await setup();
    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.componentInstance.remittance()?.totalDue).toBe(310);
    expect(fixture.componentInstance.canExport()).toBeTrue();
  });

  it('should show banner and disable export when employer matricule is missing', async () => {
    await setup({
      ...baseRemittance,
      employerCnssNumber: null as unknown as string,
      warnings: ['Matricule employeur CNSS manquant.']
    });

    const compiled = fixture.nativeElement as HTMLElement;
    expect(fixture.componentInstance.missingEmployerMatricule()).toBeTrue();
    expect(fixture.componentInstance.canExport()).toBeFalse();
    expect(fixture.componentInstance.canRecordPayment()).toBeFalse();
    expect(compiled.querySelector('.cnss-matricule-banner')).toBeTruthy();
    expect(compiled.textContent).toContain('Le matricule employeur CNSS est manquant');
    expect(compiled.querySelector('.cnss-matricule-banner app-button a.btn')).toBeTruthy();
  });
});
