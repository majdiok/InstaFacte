import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { FirmGovernanceService } from '@core/services/firm-governance.service';
import { FirmAssignmentService } from '@core/services/firm-assignment.service';
import { FirmCollaboratorsService } from '@core/services/firm-collaborators.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { signal } from '@angular/core';
import { TimeSheetsFacade } from './time-sheets/time-sheets.facade';
import { TimeSheetsPageComponent } from './time-sheets/time-sheets-page.component';

describe('FirmTimeSheetsComponent', () => {
  let component: TimeSheetsPageComponent;
  let facade: TimeSheetsFacade;
  let fixture: ComponentFixture<TimeSheetsPageComponent>;
  let createSpy: jasmine.Spy;
  let listSpy: jasmine.Spy;
  let bulkSpy: jasmine.Spy;
  let activityCodes: unknown[];
  let periods: unknown[];

  beforeEach(async () => {
    createSpy = jasmine.createSpy('createTimeSheet').and.returnValue(of({ success: true, data: {} }));
    listSpy = jasmine.createSpy('listTimeSheets').and.callFake((year?: number, month?: number) => {
      if (year === 2026 && month === 7) {
        return of({
          success: true,
          data: [{ id: 'jul28', workDate: '2026-07-28', hours: 9.5, isBillable: true, isValidated: false, status: 0 }]
        });
      }
      if (year === 2026 && month === 8) {
        return of({
          success: true,
          data: [{ id: 'aug1', workDate: '2026-08-01', hours: 2, isBillable: true, isValidated: false, status: 0 }]
        });
      }
      return of({ success: true, data: [] });
    });
    bulkSpy = jasmine.createSpy('validateTimeSheetsBulkDetailed')
      .and.returnValue(of({ success: true, data: { validated: 2, skipped: 0, failures: [] } }));
    activityCodes = [];
    periods = [];

    // Mode classique pour les tests unitaires (pas de localStorage riche).
    try { localStorage.setItem('timesheetRichUi', '1'); } catch { /* ignore */ }

    await TestBed.configureTestingModule({
      imports: [TimeSheetsPageComponent],
      providers: [
        { provide: FirmGovernanceService, useValue: {
          listTimeSheets: listSpy,
          createTimeSheet: createSpy,
          updateTimeSheet: jasmine.createSpy('updateTimeSheet').and.returnValue(of({ success: true, data: {} })),
          deleteTimeSheet: jasmine.createSpy('deleteTimeSheet').and.returnValue(of({ success: true })),
          validateTimeSheet: jasmine.createSpy('validateTimeSheet').and.returnValue(of({ success: true, data: {} })),
          unvalidateTimeSheet: jasmine.createSpy('unvalidateTimeSheet').and.returnValue(of({ success: true, data: {} })),
          submitTimeSheet: jasmine.createSpy('submitTimeSheet').and.returnValue(of({ success: true, data: {} })),
          startTimeSheetTimer: jasmine.createSpy('startTimeSheetTimer').and.returnValue(of({ success: true, data: {} })),
          stopTimeSheetTimer: jasmine.createSpy('stopTimeSheetTimer').and.returnValue(of({ success: true, data: {} })),
          duplicateTimeSheetWeek: jasmine.createSpy('duplicateTimeSheetWeek').and.returnValue(of({ success: true, data: [] })),
          validateTimeSheetsBulkDetailed: bulkSpy,
          listActivityCodes: () => of({ success: true, data: activityCodes }),
          listTimeSheetPeriods: () => of({ success: true, data: periods }),
          getTimeSheetYearSettings: () => of({
            success: true,
            data: {
              annualProductiveHours: 1600,
              maxDailyHours: 10,
              maxWeeklyHours: 48,
              enforceHardLimits: true
            }
          }),
          lockTimeSheetPeriod: jasmine.createSpy('lockTimeSheetPeriod').and.returnValue(of({ success: true, data: { year: 2026, month: 7, isLocked: true } })),
          unlockTimeSheetPeriod: jasmine.createSpy('unlockTimeSheetPeriod').and.returnValue(of({ success: true, data: {} }))
        }},
        { provide: FirmAssignmentService, useValue: {
          getActiveClients: () => of({ success: true, data: [{ assignmentId: 'a1', companyName: 'Ste X', companyTenantId: 't1', activeSince: '' }] })
        }},
        { provide: FirmCollaboratorsService, useValue: {
          list: () => of([])
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

    fixture = TestBed.createComponent(TimeSheetsPageComponent);
    component = fixture.componentInstance;
    facade = component.facade;
    fixture.detectChanges();
  });

  it('envoie firmClientAssignmentId lors de la saisie', () => {
    facade.form.patchValue({ firmClientAssignmentId: 'a1', hours: 1, workDate: '2026-07-20' });
    facade.submit();
    expect(createSpy).toHaveBeenCalledWith(jasmine.objectContaining({ firmClientAssignmentId: 'a1' }));
  });

  it('préremplit firmClientAssignmentId depuis query assignmentId', () => {
    expect(facade.form.value.firmClientAssignmentId).toBe('a1');
  });

  it('propose la date du jour en heure locale, pas en UTC', () => {
    expect(facade.form.value.workDate).toBe(TimeSheetsFacade.todayLocalIso());
  });

  it('permet de consulter toute une année', () => {
    expect(facade.monthOptions[0].value).toBeNull();
    expect(facade.monthOptions.length).toBe(13);
  });

  it('ne verrouille rien tant que la période est ouverte', () => {
    expect(facade.periodLocked()).toBeFalse();
  });

  it('signale une période clôturée et bloque la saisie', () => {
    periods = [{ year: 2026, month: 7, isLocked: true, lockedByDisplayName: 'Chef' }];
    facade.selectedYear.set(2026);
    facade.selectedMonth.set(7);
    facade.onPeriodChange();

    expect(facade.currentPeriod()?.isLocked).toBeTrue();
    expect(facade.periodLocked()).toBeTrue();
  });

  it('valide la sélection en lot', () => {
    facade.selection.set([
      { id: 'e1', isValidated: false, status: 1 } as never,
      { id: 'e2', isValidated: false, status: 1 } as never
    ]);

    facade.validateSelection();

    expect(bulkSpy).toHaveBeenCalledWith(['e1', 'e2']);
  });

  it('ignore les lignes non soumises dans la validation en lot', () => {
    facade.selection.set([
      { id: 'e1', isValidated: false, status: 1 } as never,
      { id: 'e2', isValidated: false, status: 0 } as never,
      { id: 'e3', isValidated: true, status: 2 } as never
    ]);

    facade.validateSelection();

    expect(bulkSpy).toHaveBeenCalledWith(['e1']);
  });

  it('applique la facturabilité par défaut du code activité choisi', () => {
    activityCodes = [{ id: 'c1', code: 'ADMIN', label: 'Administratif interne', isBillableByDefault: false }];
    facade.activityCodes.set(activityCodes as never);

    facade.onActivityCodeChange('ADMIN');

    expect(facade.form.value.isBillable).toBeFalse();
  });

  it('sépare les heures facturables des non facturables', () => {
    facade.entries.set([
      { id: 'e1', hours: 6, isBillable: true } as never,
      { id: 'e2', hours: 2, isBillable: false } as never
    ]);

    expect(facade.totalHours()).toBe(8);
    expect(facade.billableHours()).toBe(6);
    expect(facade.nonBillableHours()).toBe(2);
    expect(facade.billableRatio()).toBe(75);
  });

  it('préserve le contexte client/activité après une saisie rapide', () => {
    facade.form.patchValue({
      firmClientAssignmentId: 'a1',
      activityCode: 'ADMIN',
      isBillable: false,
      hours: 2,
      workDate: '2026-07-20'
    });
    facade.submit();
    expect(facade.form.value.firmClientAssignmentId).toBe('a1');
    expect(facade.form.value.activityCode).toBe('ADMIN');
    expect(facade.form.value.isBillable).toBeFalse();
  });

  it('active la vue semaine et calcule 7 jours', () => {
    facade.onModeChange('week');
    facade.setFocusDate('2026-07-20');
    expect(facade.mode()).toBe('week');
    expect(facade.weekDays().length).toBe(7);
  });

  it('expose le numéro de semaine ISO et un badge de statut', () => {
    facade.setFocusDate('2026-05-13');
    expect(facade.isoWeekNumber()).toBe(20);
    facade.entries.set([
      { id: 'e1', hours: 2, isValidated: false, status: 0 } as never,
      { id: 'e2', hours: 2, isValidated: false, status: 0 } as never
    ]);
    expect(facade.statusBadge()).toBe('En brouillon');
  });

  it('filtre par responsable de dossier côté client', () => {
    facade.clients.set([
      { assignmentId: 'a1', companyName: 'A', assignedAccountantUserId: 'u1', assignedAccountantName: 'Alice' } as never,
      { assignmentId: 'a2', companyName: 'B', assignedAccountantUserId: 'u2', assignedAccountantName: 'Bob' } as never
    ]);
    facade.entries.set([
      { id: 'e1', hours: 1, isValidated: false, firmClientAssignmentId: 'a1', isBillable: true } as never,
      { id: 'e2', hours: 2, isValidated: false, firmClientAssignmentId: 'a2', isBillable: true } as never
    ]);
    facade.setFilters({
      clientId: null, activityCode: null, accountantUserId: 'u1',
      billableOnly: null, workLocation: null, showValidated: true
    });
    expect(facade.filteredEntries().length).toBe(1);
    expect(facade.filteredEntries()[0].id).toBe('e1');
  });

  it('calcule les KPI à valider et occupation', () => {
    facade.yearSettings.set({ annualProductiveHours: 1600 } as never);
    facade.periodScope.set('week');
    facade.entries.set([
      { id: 'e1', hours: 4, isValidated: false, status: 1, isBillable: true } as never,
      { id: 'e2', hours: 2, isValidated: false, status: 0, isBillable: false } as never
    ]);
    expect(facade.hoursToValidate()).toBe(4);
    expect(facade.hoursRejected()).toBe(0);
    expect(facade.productiveHoursApprox()).toBeCloseTo(1600 / 52, 5);
    expect(facade.occupationPercent()).toBeGreaterThan(0);
  });

  it('prépare un créneau drag sans auto-submit', () => {
    facade.requestCreateFromRange({ date: '2026-07-20', startTime: '09:00', endTime: '11:00' });
    expect(facade.pendingRange()).toEqual({ date: '2026-07-20', startTime: '09:00', endTime: '11:00' });
    expect(facade.form.value.startTime).toBe('09:00');
    expect(createSpy).not.toHaveBeenCalled();
  });

  it('ouvre le dialog en mode enrichi sans appeler create', () => {
    try { localStorage.setItem('timesheetRichUi', '1'); } catch { /* ignore */ }
    facade.richUi.set(true);
    facade.requestCreateFromRange({ date: '2026-07-21', startTime: '10:00', endTime: '11:30' });
    expect(facade.entryDialogOpen()).toBeTrue();
    expect(createSpy).not.toHaveBeenCalled();
    facade.closeEntryDialog();
    expect(facade.entryDialogOpen()).toBeFalse();
    try { localStorage.setItem('timesheetRichUi', '1'); } catch { /* ignore */ }
    facade.richUi.set(false);
  });

  it('Aujourd’hui conserve la vue semaine', () => {
    facade.onModeChange('week');
    facade.setFocusDate('2026-07-01');
    facade.goTodayWeek();
    expect(facade.mode()).toBe('week');
    expect(facade.periodScope()).toBe('today');
    expect(facade.focusedDate()).toBe(TimeSheetsFacade.todayLocalIso());
  });

  it('calcule weekTotalHours sur les jours de la semaine focus', () => {
    facade.setFocusDate('2026-07-20'); // lundi
    facade.entries.set([
      { id: 'e1', hours: 2, workDate: '2026-07-20', isBillable: true } as never,
      { id: 'e2', hours: 3, workDate: '2026-07-22', isBillable: true } as never,
      { id: 'e3', hours: 8, workDate: '2026-07-28', isBillable: true } as never
    ]);
    expect(facade.weekTotalHours()).toBe(5);
  });

  it('refuse le move d’une entrée validée', () => {
    const toast = TestBed.inject(ToastService) as unknown as { add: jasmine.Spy };
    const updateSpy = TestBed.inject(FirmGovernanceService).updateTimeSheet as jasmine.Spy;
    updateSpy.calls.reset();
    facade.moveEntry(
      { id: 'e1', isValidated: true, status: 2, hours: 1 } as never,
      { date: '2026-07-21', startTime: '09:00', endTime: '10:00' }
    );
    expect(updateSpy).not.toHaveBeenCalled();
    expect(toast.add).toHaveBeenCalled();
  });

  it('calcule hoursBetween pour 12:45–16:02 (~3,283 h)', () => {
    expect(facade.hoursBetween('12:45', '16:02')).toBeCloseTo(3.283, 3);
  });

  it('ne force pas 0,25 h quand la fin est avant le début', () => {
    expect(facade.hoursBetween('16:00', '12:00')).toBeNull();
  });

  it('resync Heures après édition Début/Fin (bug drag 0,25 h)', () => {
    facade.requestCreateFromRange({ date: '2026-07-28', startTime: '12:45', endTime: '13:00' });
    expect(facade.form.value.hours).toBeCloseTo(0.25, 5);

    facade.form.get('endTime')!.setValue('16:02');
    expect(facade.form.value.hours).toBeCloseTo(3.283, 3);

    facade.form.patchValue({ startTime: '09:00', endTime: '10:30' });
    facade.syncHoursFromSlot();
    expect(facade.form.value.hours).toBeCloseTo(1.5, 5);
  });

  it('recalcule hours au submit à partir du créneau', () => {
    createSpy.calls.reset();
    facade.form.patchValue({
      workDate: '2026-07-20',
      hours: 0.25,
      startTime: '12:45',
      endTime: '16:02',
      firmClientAssignmentId: 'a1'
    });
    facade.submit();
    expect(createSpy).toHaveBeenCalledWith(jasmine.objectContaining({
      hours: jasmine.any(Number),
      startTime: '12:45',
      endTime: '16:02'
    }));
    const payload = createSpy.calls.mostRecent().args[0] as { hours: number };
    expect(payload.hours).toBeCloseTo(3.283, 3);
  });

  it('refuse submit si créneau incomplet (début sans fin)', () => {
    createSpy.calls.reset();
    const toast = TestBed.inject(ToastService) as unknown as { add: jasmine.Spy };
    toast.add.calls.reset();
    facade.form.patchValue({
      workDate: '2026-07-20',
      hours: 1,
      startTime: '09:00',
      endTime: '',
      firmClientAssignmentId: 'a1'
    });
    facade.submit();
    expect(createSpy).not.toHaveBeenCalled();
    expect(toast.add).toHaveBeenCalled();
  });

  it('patchInline recalcule hours en fusionnant start/end partiels', (done) => {
    const updateSpy = TestBed.inject(FirmGovernanceService).updateTimeSheet as jasmine.Spy;
    updateSpy.calls.reset();
    const entry = {
      id: 'e1',
      workDate: '2026-07-20',
      hours: 1,
      startTime: '09:00',
      endTime: '10:00',
      isValidated: false,
      status: 0,
      isBillable: true
    } as never;

    facade.patchInline(entry, { endTime: '12:30' });

    setTimeout(() => {
      expect(updateSpy).toHaveBeenCalledWith('e1', jasmine.objectContaining({
        startTime: '09:00',
        endTime: '12:30',
        hours: 3.5
      }));
      done();
    }, 550);
  });

  it('démarre et arrête le chronomètre (T09/T10)', () => {
    const api = TestBed.inject(FirmGovernanceService) as unknown as {
      startTimeSheetTimer: jasmine.Spy;
      stopTimeSheetTimer: jasmine.Spy;
    };
    facade.startTimer();
    expect(api.startTimeSheetTimer).toHaveBeenCalled();
    facade.entries.set([{ id: 't1', timerStartedAtUtc: new Date().toISOString(), hours: 0.25 } as never]);
    facade.stopTimer();
    expect(api.stopTimeSheetTimer).toHaveBeenCalledWith(jasmine.objectContaining({ entryId: 't1' }));
  });

  it('duplique la semaine vers la semaine suivante (T19)', () => {
    const api = TestBed.inject(FirmGovernanceService) as unknown as { duplicateTimeSheetWeek: jasmine.Spy };
    api.duplicateTimeSheetWeek.calls.reset();
    facade.setFocusDate('2026-07-20');
    facade.duplicateWeek();
    expect(api.duplicateTimeSheetWeek).toHaveBeenCalledWith(jasmine.objectContaining({
      sourceWeekStart: '2026-07-20',
      targetWeekStart: '2026-07-27'
    }));
  });

  it('exporte un CSV avec les colonnes attendues (T20)', () => {
    const clickSpy = jasmine.createSpy('click');
    const createEl = spyOn(document, 'createElement').and.callFake((tag: string) => {
      if (tag === 'a') {
        return { href: '', download: '', click: clickSpy } as unknown as HTMLAnchorElement;
      }
      return document.createElement(tag);
    });
    spyOn(URL, 'createObjectURL').and.returnValue('blob:test');
    spyOn(URL, 'revokeObjectURL');
    facade.entries.set([
      {
        id: 'e1', workDate: '2026-07-20', hours: 2, clientCompanyName: 'Ste X',
        activityCode: 'REV', notes: 'n', startTime: '09:00', endTime: '11:00',
        isBillable: true, tags: 'urgent', workLocation: 'Office', statusDisplay: 'Brouillon'
      } as never
    ]);
    facade.exportCsv();
    expect(createEl).toHaveBeenCalledWith('a');
    expect(clickSpy).toHaveBeenCalled();
  });

  it('soumet les brouillons filtrés (T21)', () => {
    const api = TestBed.inject(FirmGovernanceService) as unknown as { submitTimeSheet: jasmine.Spy };
    api.submitTimeSheet.calls.reset();
    facade.entries.set([
      { id: 'd1', hours: 1, isValidated: false, status: 0 } as never,
      { id: 's1', hours: 1, isValidated: false, status: 1 } as never,
      { id: 't1', hours: 0.25, isValidated: false, status: 0, timerStartedAtUtc: '2026-07-20T10:00:00Z' } as never
    ]);
    facade.submitDrafts();
    expect(api.submitTimeSheet).toHaveBeenCalledOnceWith('d1');
  });

  it('clôture la période sélectionnée (T49)', () => {
    const api = TestBed.inject(FirmGovernanceService) as unknown as { lockTimeSheetPeriod: jasmine.Spy };
    api.lockTimeSheetPeriod.calls.reset();
    facade.selectedYear.set(2026);
    facade.selectedMonth.set(8);
    facade.lockPeriod();
    expect(api.lockTimeSheetPeriod).toHaveBeenCalledWith(2026, 8);
  });

  it('filtre par lieu de travail enum (T17)', () => {
    facade.entries.set([
      { id: 'e1', hours: 1, workLocation: 'Office', isBillable: true } as never,
      { id: 'e2', hours: 2, workLocation: 'Remote', isBillable: true } as never
    ]);
    facade.setFilters({
      clientId: null, activityCode: null, accountantUserId: null,
      billableOnly: null, workLocation: 'Remote', showValidated: true
    });
    expect(facade.filteredEntries().map(e => e.id)).toEqual(['e2']);
  });

  it('toggle mode classique masque le dialog enrichi (T06/T53)', () => {
    facade.richUi.set(true);
    facade.entryDialogOpen.set(true);
    facade.toggleRichUi();
    expect(facade.richUi()).toBeFalse();
    expect(facade.mode()).toBe('list');
    expect(facade.formVisible()).toBeTrue();
    expect(facade.entryDialogOpen()).toBeFalse();
  });

  it('focusAddForm ouvre le dialog en mode enrichi (T38)', () => {
    facade.richUi.set(true);
    facade.focusAddForm();
    expect(facade.entryDialogOpen()).toBeTrue();
  });

  it('charge juillet et août pour une semaine chevauchante (selectedMonth=8)', () => {
    listSpy.calls.reset();
    facade.selectedYear.set(2026);
    facade.selectedMonth.set(8);
    facade.mode.set('week');
    facade.periodScope.set('week');
    facade.setFocusDate('2026-07-28'); // mar. dans la semaine 27/07–02/08
    facade.load();

    const months = listSpy.calls.allArgs().map((args: unknown[]) => args[1]).sort();
    expect(months).toEqual([7, 8]);
    expect(facade.entries().some(e => e.id === 'jul28')).toBeTrue();
    expect(facade.entries().some(e => e.id === 'aug1')).toBeTrue();
  });

  it('en mode liste ne charge que le mois sélectionné', () => {
    listSpy.calls.reset();
    facade.mode.set('list');
    facade.periodScope.set('month');
    facade.selectedYear.set(2026);
    facade.selectedMonth.set(8);
    facade.load();

    expect(listSpy).toHaveBeenCalledTimes(1);
    expect(listSpy).toHaveBeenCalledWith(2026, 8, undefined);
  });

  it('précheck bloque le submit si plafond journalier hard dépassé', () => {
    createSpy.calls.reset();
    const toast = TestBed.inject(ToastService) as unknown as { add: jasmine.Spy };
    toast.add.calls.reset();
    facade.yearSettings.set({
      annualProductiveHours: 1600,
      maxDailyHours: 10,
      maxWeeklyHours: 48,
      enforceHardLimits: true
    } as never);
    facade.entries.set([
      { id: 'e1', workDate: '2026-07-28', hours: 9.5, isBillable: true, isValidated: false, status: 0 } as never
    ]);
    facade.form.patchValue({
      workDate: '2026-07-28',
      hours: 3,
      startTime: '',
      endTime: '',
      firmClientAssignmentId: 'a1'
    });
    facade.submit();
    expect(createSpy).not.toHaveBeenCalled();
    expect(facade.anomalies().length).toBeGreaterThan(0);
    expect(facade.anomalies()[0]).toContain('plafond journalier');
    expect(toast.add).toHaveBeenCalled();
  });

  it('affiche le message API 400 dans anomalies sans fermer le dialog', () => {
    createSpy.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 400,
      statusText: 'Bad Request',
      error: { success: false, message: 'Le total du 28/07/2026 atteindrait 12,55 h, au-delà du plafond journalier de 10 h.' }
    })));
    const toast = TestBed.inject(ToastService) as unknown as { add: jasmine.Spy };
    toast.add.calls.reset();
    facade.yearSettings.set({
      maxDailyHours: 10,
      maxWeeklyHours: 48,
      enforceHardLimits: false
    } as never);
    facade.richUi.set(true);
    facade.entryDialogOpen.set(true);
    facade.form.patchValue({
      workDate: '2026-07-28',
      hours: 1,
      startTime: '',
      endTime: '',
      firmClientAssignmentId: 'a1'
    });
    facade.submit();
    expect(facade.anomalies()[0]).toContain('plafond journalier');
    expect(facade.entryDialogOpen()).toBeTrue();
    expect(toast.add).toHaveBeenCalledWith(jasmine.objectContaining({
      summary: 'Plafond / saisie refusée',
      detail: jasmine.stringMatching(/plafond journalier/)
    }));
  });

  it('n’applique pas le précheck hard si enforceHardLimits=false (warnings soft)', () => {
    createSpy.calls.reset();
    createSpy.and.returnValue(of({
      success: true,
      data: { warnings: ['Le total du 28/07/2026 atteindrait 12 h, au-delà du plafond journalier de 10 h.'] }
    }));
    facade.yearSettings.set({
      maxDailyHours: 10,
      maxWeeklyHours: 48,
      enforceHardLimits: false
    } as never);
    facade.entries.set([
      { id: 'e1', workDate: '2026-07-28', hours: 9.5, isBillable: true } as never
    ]);
    facade.form.patchValue({
      workDate: '2026-07-28',
      hours: 3,
      startTime: '',
      endTime: '',
      firmClientAssignmentId: 'a1'
    });
    facade.submit();
    expect(createSpy).toHaveBeenCalled();
    expect(facade.anomalies().length).toBe(1);
  });
});
