import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { WithholdingCertificatesTabComponent } from './withholding-certificates-tab.component';
import { PayrollService, type PayrollWithholdingCertificateBatch } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';

describe('WithholdingCertificatesTabComponent', () => {
  let fixture: ComponentFixture<WithholdingCertificatesTabComponent>;

  const emptyBatch: PayrollWithholdingCertificateBatch = {
    year: 2025,
    employerCompanyName: 'Test',
    employerNif: '1234567A',
    employeeCount: 0,
    includedMonths: [],
    missingMonths: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
    isComplete: false,
    totalGross: 0,
    totalAnnualNetTaxable: 0,
    totalIrppWithheld: 0,
    totalCssWithheld: 0,
    totalWithholding: 0,
    lines: []
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [WithholdingCertificatesTabComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: PayrollService,
          useValue: {
            getWithholdingCertificates: jasmine.createSpy('getWithholdingCertificates').and.returnValue(
              of({ success: true, data: emptyBatch })
            ),
            exportWithholdingCertificatesCsv: jasmine.createSpy('exportWithholdingCertificatesCsv'),
            exportWithholdingCertificatesZip: jasmine.createSpy('exportWithholdingCertificatesZip'),
            downloadWithholdingCertificatePdf: jasmine.createSpy('downloadWithholdingCertificatePdf')
          }
        },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) }
      ]
    });

    fixture = TestBed.createComponent(WithholdingCertificatesTabComponent);
    fixture.componentRef.setInput('initialYear', 2025);
    fixture.detectChanges();
  });

  it('disables export when employeeCount is zero', fakeAsync(() => {
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.canExport()).toBeFalse();
  }));

  it('shows empty state when no lines', fakeAsync(() => {
    tick();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Aucun certificat à générer');
  }));
});
