import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { provideRouter } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { FirmCollaboratorCostsComponent } from './firm-collaborator-costs.component';
import { FirmGovernanceService } from '@core/services/firm-governance.service';
import { ToastService } from '@core/services/toast.service';

describe('FirmCollaboratorCostsComponent', () => {
  let fixture: ComponentFixture<FirmCollaboratorCostsComponent>;
  let listSpy: jasmine.Spy;
  let syncSpy: jasmine.Spy;

  beforeEach(async () => {
    listSpy = jasmine.createSpy('listCollaboratorCosts').and.returnValue(of({
      success: true,
      data: [{
        collaboratorUserId: 'u1',
        collaboratorName: 'Amine Ben Salah',
        year: 2026,
        grossAnnualSalary: 30000,
        employerContributions: 5871,
        payrollExtras: 0,
        totalEmployerCost: 35871,
        source: 2,
        sourceDisplay: 'Importé paie',
        costDiagnostic: 0,
        costDiagnosticDisplay: 'À jour',
        effectiveHourlyRate: 45,
        hourlyRateSource: 2,
        hourlyRateSourceDisplay: 'Dérivé',
        hourlyRateBasis: '35871 / 796',
        annualProductiveHours: 796,
        payrollEmployeeId: 'e1',
        payrollEmployeeName: 'Amine Ben Salah',
        payrollLinkSource: 2,
        payrollLinkSourceDisplay: 'Auto (email)',
        payslipCount: 12
      }]
    }));

    syncSpy = jasmine.createSpy('syncCollaboratorCosts').and.returnValue(of({
      success: true,
      data: {
        payrollAvailable: true,
        linkedByEmail: 1,
        imported: 1,
        skippedManual: 0,
        skippedUnlinked: 0,
        skippedUpToDate: 0,
        syncedAt: new Date().toISOString()
      }
    }));

    await TestBed.configureTestingModule({
      imports: [FirmCollaboratorCostsComponent],
      providers: [
        provideRouter([]),
        ConfirmationService,
        {
          provide: FirmGovernanceService,
          useValue: {
            listCollaboratorCosts: listSpy,
            listPayrollEmployees: jasmine.createSpy('listPayrollEmployees').and.returnValue(of({
              success: true,
              data: { isAvailable: true, employees: [] }
            })),
            getPayrollProvisioningStatus: jasmine.createSpy('getPayrollProvisioningStatus').and.returnValue(of({
              success: true,
              data: {
                internalPayrollEnabled: true,
                activePayrollEmployees: 0,
                collaborators: [{ collaboratorUserId: 'u1', collaboratorName: 'Test', hasPayrollEmployee: false, payrollLinkSource: 0 }]
              }
            })),
            provisionPayrollFromCollaborators: jasmine.createSpy('provision').and.returnValue(of({
              success: true,
              data: { created: 1, linked: 0, skipped: 0, messages: [] }
            })),
            syncCollaboratorCosts: syncSpy,
            saveCollaboratorCost: jasmine.createSpy('save').and.returnValue(of({ success: true })),
            linkPayrollEmployee: jasmine.createSpy('link').and.returnValue(of({ success: true })),
            getTimeSheetYearSettings: jasmine.createSpy('getTimeSheetYearSettings').and.returnValue(of({
              success: true,
              data: {
                year: 2026,
                weeklyRegime: 0,
                weeklyRegimeDisplay: '48 h',
                maxDailyHours: 10,
                maxWeeklyHours: 48,
                allowFutureEntryDays: 0,
                maxBackdatingDays: 45,
                enforceHardLimits: true,
                paidLeaveDaysPerYear: 12,
                publicHolidayDaysPerYear: 12,
                productivityRatePercent: 80,
                cnssEmployerRate: 16.57,
                tfpRate: 2,
                foprolosRate: 1,
                workAccidentRate: 0,
                cssEmployerRate: 0,
                annualBaseHours: 2496,
                dailyHours: 8,
                annualProductiveHours: 1843.2,
                totalEmployerChargeRate: 19.57,
                productiveHoursMode: 0
              }
            })),
            saveTimeSheetYearSettings: jasmine.createSpy('saveTimeSheetYearSettings')
              .and.returnValue(of({ success: true }))
          }
        },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FirmCollaboratorCostsComponent);
    fixture.detectChanges();
  });

  it('charge les coûts au démarrage', () => {
    expect(listSpy).toHaveBeenCalled();
    expect(fixture.componentInstance.rows().length).toBe(1);
  });

  it('déclenche la synchronisation', () => {
    fixture.componentInstance.sync(false);
    expect(syncSpy).toHaveBeenCalled();
  });

  it('affiche le bandeau provision si salariés manquants', () => {
    fixture.detectChanges();
    expect(fixture.componentInstance.needsProvisioning()).toBeTrue();
  });

  it('affiche le bandeau paie indisponible', () => {
    const api = TestBed.inject(FirmGovernanceService) as unknown as {
      listPayrollEmployees: jasmine.Spy;
    };
    api.listPayrollEmployees.and.returnValue(of({
      success: true,
      data: { isAvailable: false, unavailableReason: 'Paie non configurée', employees: [] }
    }));
    fixture.componentInstance.load();
    fixture.detectChanges();
    expect(fixture.componentInstance.payrollUnavailable()).toBeTrue();
  });

  it('affiche le diagnostic quand la ligne est vide', () => {
    const api = TestBed.inject(FirmGovernanceService) as unknown as {
      listCollaboratorCosts: jasmine.Spy;
    };
    api.listCollaboratorCosts.and.returnValue(of({
      success: true,
      data: [{
        collaboratorUserId: 'u1',
        collaboratorName: 'Amine Ben Salah',
        year: 2026,
        grossAnnualSalary: 0,
        employerContributions: 0,
        payrollExtras: 0,
        totalEmployerCost: 0,
        source: 0,
        sourceDisplay: 'Aucune donnée',
        costDiagnostic: 2,
        costDiagnosticDisplay: 'Non lié à la paie',
        costDiagnosticHint: 'Rattachez ce collaborateur à un salarié de la paie interne.',
        effectiveHourlyRate: 50,
        hourlyRateSource: 4,
        hourlyRateSourceDisplay: 'Défaut cabinet',
        hourlyRateBasis: 'Taux par défaut du cabinet',
        annualProductiveHours: 1843.2,
        payrollLinkSource: 0,
        payslipCount: 0
      }]
    }));
    fixture.componentInstance.load();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Aucune donnée');
    expect(text).toContain('Non lié à la paie');
    // Le bouton de rattrapage doit être offert sur une ligne non liée.
    expect(text).toContain('Lier');
  });

  it('distingue un collaborateur lié dont la paie est illisible d’un non lié', () => {
    const api = TestBed.inject(FirmGovernanceService) as unknown as {
      listCollaboratorCosts: jasmine.Spy;
    };
    api.listCollaboratorCosts.and.returnValue(of({
      success: true,
      data: [{
        collaboratorUserId: 'u1',
        collaboratorName: 'Amine Ben Salah',
        year: 2026,
        grossAnnualSalary: 0,
        employerContributions: 0,
        payrollExtras: 0,
        totalEmployerCost: 0,
        source: 0,
        sourceDisplay: 'Aucune donnée',
        costDiagnostic: 3,
        costDiagnosticDisplay: 'Paie illisible',
        effectiveHourlyRate: 50,
        hourlyRateSource: 4,
        hourlyRateSourceDisplay: 'Défaut cabinet',
        hourlyRateBasis: 'Taux par défaut du cabinet',
        annualProductiveHours: 1843.2,
        payrollEmployeeId: 'e1',
        payrollLinkSource: 1,
        payrollLinkSourceDisplay: 'Manuelle',
        payslipCount: 0
      }]
    }));
    fixture.componentInstance.load();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Salarié lié');
    expect(text).not.toContain('Non lié');
  });

  it('bascule l’exercice en heures productives individualisées', () => {
    const api = TestBed.inject(FirmGovernanceService) as unknown as {
      saveTimeSheetYearSettings: jasmine.Spy;
    };
    // Le service de confirmation est fourni par le composant : on prend son instance à lui.
    const confirm = fixture.debugElement.injector.get(ConfirmationService);
    // Le basculement change les taux : il passe obligatoirement par une confirmation.
    spyOn(confirm, 'confirm').and.callFake((o: { accept?: () => void }) => {
      o.accept?.();
      return confirm;
    });

    fixture.componentInstance.confirmToggleHoursMode();

    expect(api.saveTimeSheetYearSettings).toHaveBeenCalled();
    const [, body] = api.saveTimeSheetYearSettings.calls.mostRecent().args;
    expect(body.productiveHoursMode).toBe(1);
    // Les autres paramètres de l'exercice doivent être renvoyés inchangés.
    expect(body.productivityRatePercent).toBe(80);
    expect(body.paidLeaveDaysPerYear).toBe(12);
  });

  it('signale l’écart entre congés réels et jours paramétrés', () => {
    const row = {
      realAbsenceDays: 18,
      parametricLeaveDays: 12
    } as never;
    expect(fixture.componentInstance.hasLeaveGap(row)).toBeTrue();

    const aligned = { realAbsenceDays: 12, parametricLeaveDays: 12 } as never;
    expect(fixture.componentInstance.hasLeaveGap(aligned)).toBeFalse();
  });
});
