import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { FirmLeavesReconciliationComponent } from './firm-leaves-reconciliation.component';
import { FirmLeavesService } from './data-access/firm-leaves.service';
import { ToastService } from '@core/services/toast.service';
import { FIRM_LEAVE_MIRROR_STATE } from './data-access/firm-leaves.models';

describe('FirmLeavesReconciliationComponent', () => {
  let fixture: ComponentFixture<FirmLeavesReconciliationComponent>;
  let getSpy: jasmine.Spy;
  let replaySpy: jasmine.Spy;

  const failedRow = {
    leaveRequestId: 'l1',
    userId: 'u1',
    collaboratorName: 'Karim Ferchiou',
    leaveTypeLabel: 'Congé sans solde',
    startDate: '2026-05-04',
    endDate: '2026-05-06',
    days: 3,
    mirrorState: FIRM_LEAVE_MIRROR_STATE.failed,
    mirrorStateDisplay: 'Report en échec',
    mirrorMessage: 'Base de paie injoignable.',
    canReplay: true
  };

  const frozenRow = {
    ...failedRow,
    leaveRequestId: 'l2',
    mirrorState: FIRM_LEAVE_MIRROR_STATE.blockedFrozenPayroll,
    mirrorStateDisplay: 'Paie du mois arrêtée',
    mirrorMessage: 'La paie de 05/2026 est arrêtée.',
    canReplay: false
  };

  beforeEach(async () => {
    getSpy = jasmine.createSpy('getReconciliation').and.returnValue(of({
      success: true,
      data: {
        year: 2026,
        mirroredCount: 4,
        noPayrollEffectCount: 2,
        pending: [failedRow, frozenRow],
        payrollAvailable: true
      }
    }));
    replaySpy = jasmine.createSpy('replayPayrollMirror').and.returnValue(of({
      success: true,
      data: { replayed: 1, succeeded: 1, messages: [] }
    }));

    await TestBed.configureTestingModule({
      imports: [FirmLeavesReconciliationComponent],
      providers: [
        {
          provide: FirmLeavesService,
          useValue: { getReconciliation: getSpy, replayPayrollMirror: replaySpy }
        },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FirmLeavesReconciliationComponent);
    fixture.detectChanges();
  });

  it('charge les écarts au démarrage', () => {
    expect(getSpy).toHaveBeenCalled();
    expect(fixture.componentInstance.report()?.pending.length).toBe(2);
  });

  it('ne propose le rejeu que pour les écarts rattrapables', () => {
    // Un mois de paie arrêté ne se rejoue pas : seule une régularisation le traite.
    expect(fixture.componentInstance.replayableCount()).toBe(1);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Régularisation');
  });

  it('rejoue un report ciblé', () => {
    fixture.componentInstance.replayOne(failedRow);
    expect(replaySpy).toHaveBeenCalledWith(2026, 'l1');
  });

  it('rejoue tous les reports en écart sans cibler de demande', () => {
    fixture.componentInstance.replayAll();
    expect(replaySpy).toHaveBeenCalledWith(2026, undefined);
  });

  it('distingue un échec technique d’un mois arrêté', () => {
    expect(fixture.componentInstance.stateSeverity(failedRow)).toBe('danger');
    expect(fixture.componentInstance.stateSeverity(frozenRow)).toBe('warn');
  });
});
