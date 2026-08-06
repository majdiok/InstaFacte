import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { CnssRemittanceTabComponent } from './cnss-remittance-tab.component';
import { PayrollService } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';

describe('CnssRemittanceTabComponent', () => {
  let fixture: ComponentFixture<CnssRemittanceTabComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CnssRemittanceTabComponent],
      providers: [
        {
          provide: PayrollService,
          useValue: {
            getCnssRemittance: () => of({
              success: true,
              data: {
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
                warnings: [],
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
              }
            })
          }
        },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: AuthService, useValue: { hasPermission: () => true } },
        // BankAccountService (injecté par le dialogue de paiement) requiert HttpClient.
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CnssRemittanceTabComponent);
    fixture.detectChanges();
  });

  it('should create and load remittance', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.componentInstance.remittance()?.totalDue).toBe(310);
  });
});
