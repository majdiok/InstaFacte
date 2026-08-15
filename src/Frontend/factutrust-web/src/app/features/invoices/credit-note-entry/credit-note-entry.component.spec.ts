import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { CreditNoteEntryComponent } from './credit-note-entry.component';

describe('CreditNoteEntryComponent', () => {
  let modalOpen: jasmine.Spy;
  let navigate: jasmine.Spy;

  function createComponent(result: Promise<unknown>): void {
    modalOpen = jasmine.createSpy('open').and.returnValue({ result });
    navigate = jasmine.createSpy('navigate').and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      imports: [CreditNoteEntryComponent],
      providers: [
        { provide: NgbModal, useValue: { open: modalOpen } },
        { provide: Router, useValue: { navigate } }
      ]
    });

    const fixture = TestBed.createComponent(CreditNoteEntryComponent);
    fixture.detectChanges(); // déclenche ngOnInit → ouverture du dialogue
  }

  it('opens the invoice selection dialog on init', () => {
    createComponent(new Promise(() => { }));
    expect(modalOpen).toHaveBeenCalledTimes(1);
  });

  it('navigates to the credit-note wizard after invoice selection', async () => {
    createComponent(Promise.resolve({ id: 'inv-1' }));
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(navigate).toHaveBeenCalledWith(['/invoices', 'inv-1', 'credit-note']);
  });

  it('returns to the credit-notes list when the dialog is dismissed', async () => {
    createComponent(Promise.reject('dismiss'));
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(navigate).toHaveBeenCalledWith(['/invoices/credit-notes']);
  });

  it('returns to the credit-notes list when the dialog closes without selection', async () => {
    createComponent(Promise.resolve(null));
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(navigate).toHaveBeenCalledWith(['/invoices/credit-notes']);
  });
});
