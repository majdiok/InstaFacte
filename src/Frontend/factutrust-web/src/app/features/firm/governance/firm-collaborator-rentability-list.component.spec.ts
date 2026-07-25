import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { provideRouter } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { FirmCollaboratorRentabilityListComponent } from './firm-collaborator-rentability-list.component';
import { FirmGovernanceService } from '@core/services/firm-governance.service';
import { FirmCollaboratorsService } from '@core/services/firm-collaborators.service';
import { ToastService } from '@core/services/toast.service';
import { AuthService } from '@core/services/auth.service';
import { signal } from '@angular/core';

describe('FirmCollaboratorRentabilityListComponent', () => {
  let component: FirmCollaboratorRentabilityListComponent;
  let listSpy: jasmine.Spy;
  let recalculateSpy: jasmine.Spy;

  beforeEach(async () => {
    recalculateSpy = jasmine.createSpy('recalculateRentability').and.returnValue(of({
      success: true,
      data: { recalculated: 3, skipped: [] }
    }));

    listSpy = jasmine.createSpy('listRentability').and.returnValue(of({
      success: true,
      data: {
        items: [{
          id: 'r1',
          collaboratorUserId: 'u1',
          collaboratorName: 'Ada Lovelace',
          year: 2026,
          attachedCollaboratorsCount: 1,
          companiesCount: 2,
          totalRevenue: 10000,
          payrollCost: 4000,
          adminPayrollCharge: 500,
          itManagementCharge: 200,
          operatingCharge: 300,
          clientDebitBalance: 0,
          clientCreditBalance: 0,
          rentability: 5000
        }],
        totals: {
          id: '',
          collaboratorUserId: '',
          collaboratorName: '',
          year: 0,
          attachedCollaboratorsCount: 1,
          companiesCount: 2,
          totalRevenue: 10000,
          payrollCost: 4000,
          adminPayrollCharge: 500,
          itManagementCharge: 200,
          operatingCharge: 300,
          clientDebitBalance: 0,
          clientCreditBalance: 0,
          rentability: 5000
        }
      }
    }));

    await TestBed.configureTestingModule({
      imports: [FirmCollaboratorRentabilityListComponent],
      providers: [
        provideRouter([]),
        ConfirmationService,
        {
          provide: FirmGovernanceService,
          useValue: {
            listRentability: listSpy,
            deleteRentability: jasmine.createSpy('delete').and.returnValue(of({ success: true })),
            duplicateRentability: jasmine.createSpy('duplicate').and.returnValue(of({
              success: true,
              data: { duplicated: 1, skipped: 0 }
            })),
            recalculateRentability: recalculateSpy
          }
        },
        { provide: FirmCollaboratorsService, useValue: { list: () => of([]) } },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: AuthService, useValue: { isFirmManager: signal(true) } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(FirmCollaboratorRentabilityListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('charge la liste au démarrage', () => {
    expect(listSpy).toHaveBeenCalled();
    expect(component.items().length).toBe(1);
    expect(component.totals()?.rentability).toBe(5000);
  });

  it('déclenche le recalcul global et recharge la liste', () => {
    listSpy.calls.reset();

    component.recalculate();

    expect(recalculateSpy).toHaveBeenCalled();
    expect(listSpy).toHaveBeenCalled();
    expect(component.recalculating()).toBeFalse();
  });

  it('signale les snapshots laissés intacts par le recalcul', () => {
    const toast = TestBed.inject(ToastService) as unknown as { add: jasmine.Spy };
    recalculateSpy.and.returnValue(of({
      success: true,
      data: {
        recalculated: 1,
        skipped: [{ id: 's1', collaboratorName: 'Ada', year: 2024, reason: 'Aucune feuille de temps' }]
      }
    }));

    component.recalculate();

    // Un exercice non recalculé doit être visible, pas silencieux.
    expect(toast.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'warn' })
    );
  });
});
