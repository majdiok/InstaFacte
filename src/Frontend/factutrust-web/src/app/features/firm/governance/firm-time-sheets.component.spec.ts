import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { FirmTimeSheetsComponent } from './firm-time-sheets.component';
import { FirmGovernanceService } from '@core/services/firm-governance.service';
import { FirmAssignmentService } from '@core/services/firm-assignment.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { signal } from '@angular/core';

describe('FirmTimeSheetsComponent', () => {
  let component: FirmTimeSheetsComponent;
  let createSpy: jasmine.Spy;
  let bulkSpy: jasmine.Spy;
  let lockSpy: jasmine.Spy;
  let activityCodes: unknown[];
  let periods: unknown[];

  beforeEach(async () => {
    createSpy = jasmine.createSpy('createTimeSheet').and.returnValue(of({ success: true, data: {} }));
    bulkSpy = jasmine.createSpy('validateTimeSheetsBulkDetailed')
      .and.returnValue(of({ success: true, data: { validated: 2, skipped: 0, failures: [] } }));
    lockSpy = jasmine.createSpy('lockTimeSheetPeriod')
      .and.returnValue(of({ success: true, data: { year: 2026, month: 7, isLocked: true } }));
    activityCodes = [];
    periods = [];

    await TestBed.configureTestingModule({
      imports: [FirmTimeSheetsComponent],
      providers: [
        { provide: FirmGovernanceService, useValue: {
          listTimeSheets: () => of({ success: true, data: [] }),
          createTimeSheet: createSpy,
          updateTimeSheet: jasmine.createSpy('updateTimeSheet').and.returnValue(of({ success: true, data: {} })),
          deleteTimeSheet: jasmine.createSpy('deleteTimeSheet').and.returnValue(of({ success: true })),
          validateTimeSheet: jasmine.createSpy('validateTimeSheet').and.returnValue(of({ success: true, data: {} })),
          unvalidateTimeSheet: jasmine.createSpy('unvalidateTimeSheet').and.returnValue(of({ success: true, data: {} })),
          validateTimeSheetsBulkDetailed: bulkSpy,
          listActivityCodes: () => of({ success: true, data: activityCodes }),
          listTimeSheetPeriods: () => of({ success: true, data: periods }),
          lockTimeSheetPeriod: lockSpy,
          unlockTimeSheetPeriod: jasmine.createSpy('unlockTimeSheetPeriod').and.returnValue(of({ success: true, data: {} }))
        }},
        { provide: FirmAssignmentService, useValue: {
          getActiveClients: () => of({ success: true, data: [{ assignmentId: 'a1', companyName: 'Ste X', companyTenantId: 't1', activeSince: '' }] })
        }},
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: AuthService, useValue: { isFirmManager: signal(true) } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: {
                get: (key: string) => key === 'assignmentId' ? 'a1' : key === 'userId' ? null : null
              }
            }
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(FirmTimeSheetsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('envoie firmClientAssignmentId lors de la saisie', () => {
    component.form.patchValue({ firmClientAssignmentId: 'a1', hours: 1, workDate: '2026-07-20' });
    component.submit();
    expect(createSpy).toHaveBeenCalledWith(jasmine.objectContaining({ firmClientAssignmentId: 'a1' }));
  });

  it('préremplit firmClientAssignmentId depuis query assignmentId', () => {
    expect(component.form.value.firmClientAssignmentId).toBe('a1');
  });

  it('propose la date du jour en heure locale, pas en UTC', () => {
    // toISOString() renvoyait la veille entre minuit et 1 h à Tunis (UTC+1).
    const now = new Date();
    const expected = `${now.getFullYear()}-`
      + `${`${now.getMonth() + 1}`.padStart(2, '0')}-`
      + `${`${now.getDate()}`.padStart(2, '0')}`;

    expect(component.form.value.workDate).toBe(expected);
  });

  it('permet de consulter toute une année', () => {
    // Le mois était auparavant imposé, rendant la vue annuelle impossible.
    expect(component.monthOptions[0].value).toBeNull();
    expect(component.monthOptions.length).toBe(13);
  });

  it('ne verrouille rien tant que la période est ouverte', () => {
    expect(component.periodLocked()).toBeFalse();
  });

  it('signale une période clôturée et bloque la saisie', () => {
    // Nouveau tableau et non mutation en place : un signal réglé sur la même référence
    // n'invalide pas le computed qui le lit.
    periods = [{ year: 2026, month: 7, isLocked: true, lockedByDisplayName: 'Chef' }];
    component.selectedYear = 2026;
    component.selectedMonth = 7;
    component.onPeriodChange();

    expect(component.currentPeriod()?.isLocked).toBeTrue();
    expect(component.periodLocked()).toBeTrue();
  });

  it('valide la sélection en lot', () => {
    component.selection = [
      { id: 'e1', isValidated: false } as never,
      { id: 'e2', isValidated: false } as never
    ];

    component.validateSelection();

    expect(bulkSpy).toHaveBeenCalledWith(['e1', 'e2']);
  });

  it('ignore les lignes déjà validées dans la validation en lot', () => {
    component.selection = [
      { id: 'e1', isValidated: false } as never,
      { id: 'e2', isValidated: true } as never
    ];

    component.validateSelection();

    expect(bulkSpy).toHaveBeenCalledWith(['e1']);
  });

  it('applique la facturabilité par défaut du code activité choisi', () => {
    activityCodes = [{ id: 'c1', code: 'ADMIN', label: 'Administratif interne', isBillableByDefault: false }];
    component.activityCodes.set(activityCodes as never);

    component.onActivityCodeChange('ADMIN');

    expect(component.form.value.isBillable).toBeFalse();
  });

  it('sépare les heures facturables des non facturables', () => {
    component.entries.set([
      { id: 'e1', hours: 6, isBillable: true } as never,
      { id: 'e2', hours: 2, isBillable: false } as never
    ]);

    expect(component.totalHours()).toBe(8);
    expect(component.billableHours()).toBe(6);
    expect(component.nonBillableHours()).toBe(2);
    expect(component.billableRatio()).toBe(75);
  });
});
