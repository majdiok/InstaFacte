import { TestBed } from '@angular/core/testing';
import { NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';
import { ConfirmationService } from './confirmation.service';
import { ConfirmModalComponent } from '@shared/components/confirm-modal/confirm-modal.component';

describe('ConfirmationService', () => {
  let service: ConfirmationService;
  let modal: jasmine.SpyObj<NgbModal>;
  let modalRef: {
    componentInstance: ConfirmModalComponent;
    result: Promise<unknown>;
    close: jasmine.Spy;
    dismiss: jasmine.Spy;
  };

  beforeEach(() => {
    modalRef = {
      componentInstance: {
        message: '',
        header: '',
        icon: '',
        acceptLabel: '',
        rejectLabel: '',
        acceptButtonStyleClass: '',
        showRejectButton: true
      } as ConfirmModalComponent,
      result: Promise.resolve(),
      close: jasmine.createSpy('close'),
      dismiss: jasmine.createSpy('dismiss')
    };

    modal = jasmine.createSpyObj('NgbModal', ['open']);
    modal.open.and.returnValue(modalRef as unknown as NgbModalRef);

    TestBed.configureTestingModule({
      providers: [
        ConfirmationService,
        { provide: NgbModal, useValue: modal }
      ]
    });

    service = TestBed.inject(ConfirmationService);
  });

  it('opens confirm modal with default size sm when size is omitted', () => {
    service.confirm({ message: 'Confirmer ?' });

    expect(modal.open).toHaveBeenCalledWith(
      ConfirmModalComponent,
      jasmine.objectContaining({ size: 'sm' })
    );
  });

  it('opens confirm modal with size md when requested', () => {
    service.confirm({
      message: 'Confirmer ?',
      size: 'md',
      acceptLabel: 'Annuler l\'inventaire',
      rejectLabel: 'Continuer le comptage'
    });

    expect(modal.open).toHaveBeenCalledWith(
      ConfirmModalComponent,
      jasmine.objectContaining({ size: 'md' })
    );
    expect(modalRef.componentInstance.acceptLabel).toBe('Annuler l\'inventaire');
    expect(modalRef.componentInstance.rejectLabel).toBe('Continuer le comptage');
  });

  it('invokes accept callback when modal closes', async () => {
    const accept = jasmine.createSpy('accept');
    modalRef.result = Promise.resolve();

    service.confirm({ message: 'OK ?', accept });
    await modalRef.result;

    expect(accept).toHaveBeenCalled();
  });

  it('invokes reject callback when modal is dismissed', async () => {
    const reject = jasmine.createSpy('reject');
    modalRef.result = Promise.reject('dismiss');

    service.confirm({ message: 'OK ?', reject });

    try {
      await modalRef.result;
    } catch {
      // expected dismiss rejection
    }
    // Allow microtask queue to flush the service .then/.catch handlers
    await Promise.resolve();
    await Promise.resolve();

    expect(reject).toHaveBeenCalled();
  });
});
