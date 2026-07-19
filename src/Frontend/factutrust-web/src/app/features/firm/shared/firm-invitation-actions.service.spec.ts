import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FirmAssignmentService } from '@core/services/firm-assignment.service';
import { FirmBadgeService } from '@core/services/firm-badge.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { FirmInvitationActionsService } from './firm-invitation-actions.service';

describe('FirmInvitationActionsService', () => {
  let acceptSpy: jasmine.Spy;
  let rejectSpy: jasmine.Spy;
  let invalidateSpy: jasmine.Spy;
  let toastAdd: jasmine.Spy;
  let confirmationAccept: boolean;
  let modalResult: Promise<string>;

  beforeEach(() => {
    acceptSpy = jasmine.createSpy('acceptInvitation').and.returnValue(of({ success: true, data: null }));
    rejectSpy = jasmine.createSpy('rejectInvitation').and.returnValue(of({ success: true, data: null }));
    invalidateSpy = jasmine.createSpy('invalidate');
    toastAdd = jasmine.createSpy('add');
    confirmationAccept = true;
    modalResult = Promise.resolve('Dossier incomplet');

    TestBed.configureTestingModule({
      providers: [
        FirmInvitationActionsService,
        { provide: FirmAssignmentService, useValue: { acceptInvitation: acceptSpy, rejectInvitation: rejectSpy } },
        { provide: FirmBadgeService, useValue: { invalidate: invalidateSpy } },
        { provide: ToastService, useValue: { add: toastAdd } },
        {
          provide: ConfirmationService,
          useValue: { confirm: (c: { accept?: () => void; reject?: () => void }) => (confirmationAccept ? c.accept?.() : c.reject?.()) }
        },
        {
          provide: NgbModal,
          useValue: { open: () => ({ componentInstance: {}, result: modalResult }) }
        }
      ]
    });
  });

  it('accept confirmé : appelle l’API, notifie et invalide le badge', async () => {
    const service = TestBed.inject(FirmInvitationActionsService);
    const ok = await service.accept({ id: 'a1', companyName: 'Ste X' });

    expect(ok).toBe(true);
    expect(acceptSpy).toHaveBeenCalledWith('a1');
    expect(invalidateSpy).toHaveBeenCalled();
    expect(toastAdd).toHaveBeenCalled();
  });

  it('accept annulé dans la confirmation : aucun appel API', async () => {
    confirmationAccept = false;
    const service = TestBed.inject(FirmInvitationActionsService);
    const ok = await service.accept({ id: 'a1', companyName: 'Ste X' });

    expect(ok).toBe(false);
    expect(acceptSpy).not.toHaveBeenCalled();
  });

  it('reject : transmet le motif saisi et invalide le badge', async () => {
    const service = TestBed.inject(FirmInvitationActionsService);
    const ok = await service.reject({ id: 'a2', companyName: 'Ste Y' });

    expect(ok).toBe(true);
    expect(rejectSpy).toHaveBeenCalledWith('a2', 'Dossier incomplet');
    expect(invalidateSpy).toHaveBeenCalled();
  });

  it('reject : dialog fermé sans confirmer → aucun appel API', async () => {
    modalResult = Promise.reject('dismissed');
    const service = TestBed.inject(FirmInvitationActionsService);
    const ok = await service.reject({ id: 'a2', companyName: 'Ste Y' });

    expect(ok).toBe(false);
    expect(rejectSpy).not.toHaveBeenCalled();
  });

  it('accept : erreur API → toast erreur, pas d’invalidation', async () => {
    acceptSpy.and.returnValue(throwError(() => ({ error: { message: 'Boom' } })));
    const service = TestBed.inject(FirmInvitationActionsService);
    const ok = await service.accept({ id: 'a1', companyName: 'Ste X' });

    expect(ok).toBe(false);
    expect(invalidateSpy).not.toHaveBeenCalled();
    expect(toastAdd).toHaveBeenCalled();
  });
});
