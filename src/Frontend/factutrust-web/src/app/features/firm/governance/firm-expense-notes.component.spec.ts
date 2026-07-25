import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { FirmExpenseNotesComponent } from './firm-expense-notes.component';
import { FirmGovernanceService } from '@core/services/firm-governance.service';
import { FirmAssignmentService } from '@core/services/firm-assignment.service';
import { FirmGovernanceActionsService } from '../shared/firm-governance-actions.service';
import { ToastService } from '@core/services/toast.service';

describe('FirmExpenseNotesComponent', () => {
  let fixture: ComponentFixture<FirmExpenseNotesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FirmExpenseNotesComponent],
      providers: [
        {
          provide: FirmGovernanceService,
          useValue: {
            listExpenseNotes: () => of({
              success: true,
              data: [{
                id: 'n1',
                firmClientAssignmentId: 'a1',
                companyName: 'Ste X',
                periodYear: 2026,
                periodMonth: 7,
                status: 0,
                statusDisplay: 'Brouillon',
                totalToReimburse: 100,
                mixedCharges: 0,
                operatingExpenses: 0,
                mileageAllowance: 0,
                salesAmount: 0
              }]
            }),
            upsertExpenseNote: () => of({ success: true, data: {} })
          }
        },
        {
          provide: FirmAssignmentService,
          useValue: { getActiveClients: () => of({ success: true, data: [] }) }
        },
        {
          provide: FirmGovernanceActionsService,
          useValue: {
            submitExpenseNote: jasmine.createSpy('submitExpenseNote').and.returnValue(Promise.resolve(true)),
            processExpenseNote: jasmine.createSpy('processExpenseNote').and.returnValue(Promise.resolve(true)),
            reimburseExpenseNote: jasmine.createSpy('reimburseExpenseNote').and.returnValue(Promise.resolve(true))
          }
        },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FirmExpenseNotesComponent);
    fixture.detectChanges();
  });

  it('affiche le bouton soumettre pour une note brouillon', () => {
    const html = fixture.nativeElement.innerHTML as string;
    expect(html).toContain('pi-send');
  });
});
