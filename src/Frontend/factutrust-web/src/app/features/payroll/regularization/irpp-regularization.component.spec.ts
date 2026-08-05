import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { IrppRegularizationComponent } from './irpp-regularization.component';
import { PayrollService, IrppRegularizationPreview } from '@core/services/payroll.service';
import { EmployeeService, ApiResponse } from '@core/services/employee.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';

function previewFixture(overrides: Partial<IrppRegularizationPreview> = {}): IrppRegularizationPreview {
  return {
    employeeId: 'emp-1',
    employeeName: 'BEN ALI Karim',
    employeeNumber: 'EMP-001',
    year: 2026,
    month: 12,
    reason: 0,
    reasonLabel: 'Régularisation annuelle (décembre)',
    monthsCounted: 12,
    cumulNetTaxable: 19796.796,
    cumulIrppWithheld: 3199.2,
    cumulCssWithheld: 98.988,
    irppDue: 3199.199,
    cssDue: 98.984,
    irppDelta: -150.5,
    cssDelta: -4.5,
    totalDelta: -155,
    isAdditionalWithholding: false,
    isFeatureDisabled: false,
    isPartialYear: false,
    months: [
      { month: 11, monthLabel: 'Novembre', monthlyNetTaxable: 1649.733, irpp: 266.6, css: 8.249, isSettled: true },
      { month: 12, monthLabel: 'Décembre', monthlyNetTaxable: 1649.733, irpp: 266.6, css: 8.249, isSettled: false }
    ],
    ...overrides
  };
}

describe('IrppRegularizationComponent', () => {
  let payrollStub: jasmine.SpyObj<PayrollService>;
  let toastSpy: jasmine.SpyObj<ToastService>;

  async function setup(
    preview: IrppRegularizationPreview | null = previewFixture(),
    canRun = true
  ): Promise<ComponentFixture<IrppRegularizationComponent>> {
    payrollStub = jasmine.createSpyObj<PayrollService>('PayrollService', [
      'previewIrppRegularization',
      'upsertIrppRegularization'
    ]);
    payrollStub.previewIrppRegularization.and.returnValue(
      of({ success: true, data: preview } as ApiResponse<IrppRegularizationPreview>)
    );
    payrollStub.upsertIrppRegularization.and.returnValue(
      of({ success: true, data: 'reg-1' } as ApiResponse<string>)
    );

    toastSpy = jasmine.createSpyObj<ToastService>('ToastService', ['add']);

    await TestBed.configureTestingModule({
      imports: [IrppRegularizationComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PayrollService, useValue: payrollStub },
        { provide: ToastService, useValue: toastSpy },
        {
          provide: EmployeeService,
          useValue: {
            list: () => of({
              success: true,
              data: { items: [{ id: 'emp-1', fullName: 'BEN ALI Karim' }] }
            })
          }
        },
        {
          provide: AuthService,
          useValue: {
            hasPermission: () => canRun,
            hasAnyPermission: () => canRun,
            hasModule: () => true,
            isFirmDelegatedReadonly: () => false
          }
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({ year: '2026', month: '12' }) } }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(IrppRegularizationComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('lit la période depuis les paramètres de requête', fakeAsync(async () => {
    const fixture = await setup();
    tick();

    expect(fixture.componentInstance.year).toBe(2026);
    expect(fixture.componentInstance.month).toBe(12);
    expect(fixture.nativeElement.textContent).toContain('Décembre 2026');
  }));

  it('n’affiche le récapitulatif qu’après un calcul', fakeAsync(async () => {
    const fixture = await setup();
    tick();

    expect(fixture.nativeElement.textContent).not.toContain('Récapitulatif');

    fixture.componentInstance.employeeId = 'emp-1';
    fixture.componentInstance.calculate();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Récapitulatif');
    expect(fixture.nativeElement.textContent).toContain('Restitution');
  }));

  it('pré-remplit les montants ajustables avec les écarts calculés', fakeAsync(async () => {
    const fixture = await setup();
    tick();

    fixture.componentInstance.employeeId = 'emp-1';
    fixture.componentInstance.calculate();
    tick();

    expect(fixture.componentInstance.overrideIrpp).toBe(-150.5);
    expect(fixture.componentInstance.overrideCss).toBe(-4.5);
  }));

  it('affiche le détail mois par mois avec une ligne de total', fakeAsync(async () => {
    const fixture = await setup();
    tick();

    fixture.componentInstance.employeeId = 'emp-1';
    fixture.componentInstance.calculate();
    tick();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Novembre');
    expect(text).toContain('Décembre');
    expect(text).toContain('Total');
    expect(text).toContain('Mois en cours');
  }));

  it('signale une année incomplète', fakeAsync(async () => {
    const fixture = await setup(previewFixture({ isPartialYear: true, monthsCounted: 3 }));
    tick();

    fixture.componentInstance.employeeId = 'emp-1';
    fixture.componentInstance.calculate();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Année incomplète');
  }));

  it('avertit quand la fonctionnalité est désactivée pour l’exercice', fakeAsync(async () => {
    const fixture = await setup(previewFixture({ isFeatureDisabled: true }));
    tick();

    fixture.componentInstance.employeeId = 'emp-1';
    fixture.componentInstance.calculate();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('indicatif');
  }));

  it('n’envoie pas d’override quand les montants sont inchangés', fakeAsync(async () => {
    const fixture = await setup();
    tick();

    fixture.componentInstance.employeeId = 'emp-1';
    fixture.componentInstance.calculate();
    tick();

    fixture.componentInstance.save();
    tick();

    const body = payrollStub.upsertIrppRegularization.calls.mostRecent().args[0];
    expect(body.overrideIrppDelta).toBeNull();
    expect(body.overrideCssDelta).toBeNull();
  }));

  it('envoie un override quand le gestionnaire corrige le montant', fakeAsync(async () => {
    const fixture = await setup();
    tick();

    fixture.componentInstance.employeeId = 'emp-1';
    fixture.componentInstance.calculate();
    tick();

    fixture.componentInstance.overrideIrpp = -100;
    fixture.componentInstance.save();
    tick();

    const body = payrollStub.upsertIrppRegularization.calls.mostRecent().args[0];
    expect(body.overrideIrppDelta).toBe(-100);
  }));

  it('refuse d’enregistrer sans droit de gestion', fakeAsync(async () => {
    const fixture = await setup(previewFixture(), false);
    tick();

    fixture.componentInstance.employeeId = 'emp-1';
    fixture.componentInstance.calculate();
    tick();

    fixture.componentInstance.save();
    tick();

    expect(payrollStub.upsertIrppRegularization).not.toHaveBeenCalled();
  }));

  it('signale une erreur de calcul sans laisser de résultat périmé', fakeAsync(async () => {
    const fixture = await setup();
    tick();

    payrollStub.previewIrppRegularization.and.returnValue(
      throwError(() => ({ error: { message: 'Calcul impossible.' } }))
    );

    fixture.componentInstance.employeeId = 'emp-1';
    fixture.componentInstance.calculate();
    tick();

    expect(fixture.componentInstance.preview()).toBeNull();
    expect(toastSpy.add).toHaveBeenCalled();
  }));
});
