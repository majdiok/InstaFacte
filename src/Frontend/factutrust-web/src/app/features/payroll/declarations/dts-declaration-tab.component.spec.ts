import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DtsDeclarationTabComponent } from './dts-declaration-tab.component';
import { PayrollService, type DtsDeclaration } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';

describe('DtsDeclarationTabComponent', () => {
  let fixture: ComponentFixture<DtsDeclarationTabComponent>;
  let getDtsSpy: jasmine.Spy;
  let exportSpy: jasmine.Spy;

  const emptyDts: DtsDeclaration = {
    year: 2026,
    quarter: 2,
    totalGross: 0,
    totalCnssableGross: 0,
    totalCnssEmployee: 0,
    totalCnssEmployer: 0,
    totalContributions: 0,
    employeeCount: 0,
    includedMonths: [],
    missingMonths: [4, 5, 6],
    isComplete: false,
    lines: []
  };

  const filledDts: DtsDeclaration = {
    year: 2026,
    quarter: 2,
    totalGross: 3000,
    totalCnssableGross: 2800,
    totalCnssEmployee: 257.04,
    totalCnssEmployer: 463.96,
    totalContributions: 721,
    employeeCount: 1,
    includedMonths: [4, 5, 6],
    missingMonths: [],
    isComplete: true,
    lines: [{
      employeeId: 'e1',
      employeeName: 'Alice Dupont',
      cnssNumber: '1234567890',
      totalGross: 3000,
      totalCnssableGross: 2800,
      cnssEmployee: 257.04,
      cnssEmployer: 463.96,
      monthsCount: 3
    }]
  };

  const incompleteDts: DtsDeclaration = {
    ...filledDts,
    includedMonths: [4],
    missingMonths: [5, 6],
    isComplete: false
  };

  function setup(dtsResponse: DtsDeclaration | 'error' = emptyDts, year = 2026, quarter = 2) {
    getDtsSpy = jasmine.createSpy('getDts').and.returnValue(
      dtsResponse === 'error'
        ? throwError(() => new Error('fail'))
        : of({ success: true, data: dtsResponse })
    );
    exportSpy = jasmine.createSpy('exportDtsCsv').and.returnValue(of(new Blob(['csv'])));

    TestBed.configureTestingModule({
      imports: [DtsDeclarationTabComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: PayrollService,
          useValue: { getDts: getDtsSpy, exportDtsCsv: exportSpy }
        },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) }
      ]
    });

    fixture = TestBed.createComponent(DtsDeclarationTabComponent);
    fixture.componentRef.setInput('initialYear', year);
    fixture.componentRef.setInput('initialQuarter', quarter);
    fixture.detectChanges();
  }

  it('loads DTS for initial year and quarter inputs', fakeAsync(() => {
    setup(emptyDts, 2026, 2);
    tick();
    fixture.detectChanges();

    expect(getDtsSpy).toHaveBeenCalledWith(2026, 2);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Aucun salarié déclaré');
  }));

  it('shows incompleteness warning when isComplete is false', fakeAsync(() => {
    setup(incompleteDts, 2026, 2);
    tick();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Trimestre incomplet');
    expect(el.textContent).toContain('Alice Dupont');
  }));

  it('disables export when employeeCount is zero', fakeAsync(() => {
    setup(emptyDts, 2026, 2);
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.canExport()).toBeFalse();
  }));

  it('enables export when lines are present', fakeAsync(() => {
    setup(filledDts, 2026, 2);
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.canExport()).toBeTrue();
  }));
});
