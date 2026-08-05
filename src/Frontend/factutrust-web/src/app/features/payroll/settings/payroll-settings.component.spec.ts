import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { PayrollSettingsComponent } from './payroll-settings.component';
import { PayrollService, PayrollParameters } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';

describe('PayrollSettingsComponent', () => {
  let fixture: ComponentFixture<PayrollSettingsComponent>;
  let payrollSpy: jasmine.SpyObj<PayrollService>;
  let toastSpy: jasmine.SpyObj<ToastService>;

  const params: PayrollParameters = {
    id: 'p1',
    fiscalYear: 2026,
    cnssEmployeeRate: 9.18,
    cnssEmployerRate: 16.57,
    cnssEmployeeRateRsa: 9.18,
    cnssEmployerRateRsa: 16.57,
    enforceSmigOnContracts: false,
    enableExtendedOvertimeRates: false,
    enableAllowanceQuadrantMatrix: false,
    cssRate: 0.5,
    cssAnnualExemptionThreshold: 5000,
    professionalExpensesRate: 10,
    professionalExpensesAnnualCap: 2000,
    headOfFamilyAnnualDeduction: 300,
    childAnnualDeduction: 100,
    maxDeductibleChildren: 4,
    studentChildAnnualDeduction: 1000,
    disabledChildAnnualDeduction: 2000,
    parentDeductionRatePercent: 5,
    parentAnnualDeductionCap: 450,
    isIndustrialSector: false,
    tfpRateIndustry: 1,
    tfpRateOther: 2,
    foprolosRate: 1,
    monthlySmig: 528.32,
    mealVoucherDailyExemptionCap: 9.2,
    irppBrackets: [
      { lowerBound: 0, rate: 0 },
      { lowerBound: 5000, rate: 15 }
    ]
  };

  beforeEach(() => {
    payrollSpy = jasmine.createSpyObj('PayrollService', [
      'getParameters',
      'updateParameters',
      'getGarnishmentBrackets',
      'updateGarnishmentBrackets',
      'listSocialFunds'
    ]);
    payrollSpy.getParameters.and.returnValue(of({ success: true, data: { ...params, irppBrackets: params.irppBrackets.map(b => ({ ...b })) } }));
    payrollSpy.updateParameters.and.returnValue(of({ success: true, data: null }));
    payrollSpy.getGarnishmentBrackets.and.returnValue(of({ success: true, data: [] }));
    payrollSpy.updateGarnishmentBrackets.and.returnValue(of({ success: true, data: null }));
    payrollSpy.listSocialFunds.and.returnValue(of({ success: true, data: [] }));
    toastSpy = jasmine.createSpyObj('ToastService', ['add']);

    TestBed.configureTestingModule({
      imports: [PayrollSettingsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: PayrollService, useValue: payrollSpy },
        { provide: ToastService, useValue: toastSpy }
      ]
    });
    fixture = TestBed.createComponent(PayrollSettingsComponent);
    fixture.detectChanges();
  });

  it('loads parameters for the current fiscal year', () => {
    expect(payrollSpy.getParameters).toHaveBeenCalled();
    expect(fixture.componentInstance.params()?.cnssEmployeeRate).toBe(9.18);
  });

  it('saves valid parameters without id and fiscalYear in the payload', () => {
    fixture.componentInstance.save();
    expect(payrollSpy.updateParameters).toHaveBeenCalledTimes(1);
    const [, body] = payrollSpy.updateParameters.calls.mostRecent().args;
    expect((body as Record<string, unknown>)['id']).toBeUndefined();
    expect((body as Record<string, unknown>)['fiscalYear']).toBeUndefined();
    expect((body as Record<string, unknown>)['isIndustrialSector']).toBe(false);
  });

  it('blocks saving when two IRPP brackets share the same lower bound', () => {
    const p = fixture.componentInstance.params()!;
    p.irppBrackets.push({ lowerBound: 5000, rate: 25 });
    fixture.componentInstance.save();
    expect(payrollSpy.updateParameters).not.toHaveBeenCalled();
    expect(toastSpy.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error' }));
  });

  it('blocks saving when the first bracket does not start at zero', () => {
    const p = fixture.componentInstance.params()!;
    p.irppBrackets[0].lowerBound = 100;
    fixture.componentInstance.save();
    expect(payrollSpy.updateParameters).not.toHaveBeenCalled();
  });

  it('blocks saving when the bareme is empty', () => {
    const p = fixture.componentInstance.params()!;
    p.irppBrackets.length = 0;
    fixture.componentInstance.save();
    expect(payrollSpy.updateParameters).not.toHaveBeenCalled();
  });
});
