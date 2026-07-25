import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { FirmGovernanceService } from '@core/services/firm-governance.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { FirmGovernanceActionsService } from './firm-governance-actions.service';

describe('FirmGovernanceActionsService', () => {
  let submitSpy: jasmine.Spy;
  let toastAdd: jasmine.Spy;
  let confirmationAccept: boolean;
  let navigateSpy: jasmine.Spy;

  beforeEach(() => {
    submitSpy = jasmine.createSpy('submitExpenseNote').and.returnValue(of({ success: true, data: null }));
    toastAdd = jasmine.createSpy('add');
    confirmationAccept = true;
    navigateSpy = jasmine.createSpy('navigate').and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      providers: [
        FirmGovernanceActionsService,
        {
          provide: FirmGovernanceService,
          useValue: {
            submitExpenseNote: submitSpy,
            processExpenseNote: jasmine.createSpy('processExpenseNote').and.returnValue(of({ success: true, data: null })),
            markExpenseNoteReimbursed: jasmine.createSpy('markExpenseNoteReimbursed').and.returnValue(of({ success: true, data: null })),
            upsertPermanentFile: jasmine.createSpy('upsertPermanentFile').and.returnValue(of({ success: true, data: {} }))
          }
        },
        { provide: ToastService, useValue: { add: toastAdd } },
        {
          provide: ConfirmationService,
          useValue: { confirm: (c: { accept?: () => void; reject?: () => void }) => (confirmationAccept ? c.accept?.() : c.reject?.()) }
        },
        { provide: Router, useValue: { navigate: navigateSpy } }
      ]
    });
  });

  it('submitExpenseNote confirmé : appelle l’API et notifie', async () => {
    const service = TestBed.inject(FirmGovernanceActionsService);
    const ok = await service.submitExpenseNote('note-1');

    expect(ok).toBe(true);
    expect(submitSpy).toHaveBeenCalledWith('note-1');
    expect(toastAdd).toHaveBeenCalled();
  });

  it('submitExpenseNote annulé : aucun appel API', async () => {
    confirmationAccept = false;
    const service = TestBed.inject(FirmGovernanceActionsService);
    const ok = await service.submitExpenseNote('note-1');

    expect(ok).toBe(false);
    expect(submitSpy).not.toHaveBeenCalled();
  });
});
