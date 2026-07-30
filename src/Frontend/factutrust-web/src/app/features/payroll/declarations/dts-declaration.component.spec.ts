import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter, ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DtsDeclarationComponent } from './dts-declaration.component';
import { PayrollService, type DtsDeclaration } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';

describe('DtsDeclarationComponent', () => {
  let fixture: ComponentFixture<DtsDeclarationComponent>;
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

  function setup(query: Record<string, string>, dtsResponse: DtsDeclaration | 'error' = emptyDts) {
    getDtsSpy = jasmine.createSpy('getDts').and.returnValue(
      dtsResponse === 'error'
        ? throwError(() => new Error('fail'))
        : of({ success: true, data: dtsResponse })
    );
    exportSpy = jasmine.createSpy('exportDtsCsv').and.returnValue(of(new Blob(['csv'])));

    TestBed.configureTestingModule({
      imports: [DtsDeclarationComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: PayrollService,
          useValue: { getDts: getDtsSpy, exportDtsCsv: exportSpy }
        },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(query)
            }
          }
        }
      ]
    });

    fixture = TestBed.createComponent(DtsDeclarationComponent);
    fixture.detectChanges();
  }

  it('reads year and quarter from query params (fiscal deep-link)', fakeAsync(() => {
    setup({ year: '2026', quarter: '2' }, emptyDts);
    tick();
    fixture.detectChanges();

    expect(getDtsSpy).toHaveBeenCalledWith(2026, 2);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Aucun salarié déclaré');
    expect(el.textContent).toContain('Avril, Mai, Juin');
  }));

  it('ignores invalid query params and keeps calendar defaults', fakeAsync(() => {
    const now = new Date();
    const expectedYear = now.getFullYear();
    const expectedQuarter = Math.ceil((now.getMonth() + 1) / 3);

    setup({ year: 'abc', quarter: '9' }, emptyDts);
    tick();

    expect(getDtsSpy).toHaveBeenCalledWith(expectedYear, expectedQuarter);
  }));

  it('shows incompleteness warning when isComplete is false', fakeAsync(() => {
    setup({ year: '2026', quarter: '2' }, incompleteDts);
    tick();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Trimestre incomplet');
    expect(el.textContent).toContain('Mai, Juin');
    expect(el.textContent).toContain('Alice Dupont');
    expect(el.textContent).toContain('Cotisations CNSS');
  }));

  it('disables export when employeeCount is zero', fakeAsync(() => {
    setup({ year: '2026', quarter: '2' }, emptyDts);
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.canExport()).toBeFalse();
  }));

  it('enables export and shows table when lines are present', fakeAsync(() => {
    setup({ year: '2026', quarter: '2' }, filledDts);
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.canExport()).toBeTrue();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Alice Dupont');
    expect(el.textContent).toContain('Mois');
    expect(el.textContent).not.toContain('Trimestre incomplet');
  }));

  it('shows error empty state when API fails', fakeAsync(() => {
    setup({}, 'error');
    tick();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Impossible de charger la DTS');
    expect(fixture.componentInstance.dts()).toBeNull();
  }));
});
