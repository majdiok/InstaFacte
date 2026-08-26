import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { EntrySubmitService } from './entry-submit.service';
import { EntryFormStore } from './entry-form.store';
import { AccountingService } from '../../services/accounting.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { createEmptyLine } from '../models/entry-form.model';

describe('EntrySubmitService', () => {
  let service: EntrySubmitService;
  let store: EntryFormStore;
  let api: jasmine.SpyObj<AccountingService>;

  beforeEach(() => {
    api = jasmine.createSpyObj('AccountingService', [
      'createManualJournalEntry',
      'updateDraftJournalEntry',
      'uploadEntryAttachment'
    ]);
    TestBed.configureTestingModule({
      providers: [
        EntrySubmitService,
        EntryFormStore,
        { provide: AccountingService, useValue: api },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: ErrorHandlerService, useValue: { extractErrorMessage: () => 'err' } }
      ]
    });
    service = TestBed.inject(EntrySubmitService);
    store = TestBed.inject(EntryFormStore);
  });

  it('submits balanced entry', (done) => {
    store.entryLabel.set('Test');
    store.lines.set([
      { ...createEmptyLine(), accountNumber: '607', debit: 100 },
      { ...createEmptyLine(), accountNumber: '4011', credit: 100 }
    ]);
    api.createManualJournalEntry.and.returnValue(of({ success: true, data: 'id-1' }));

    service.submit(store, [], 'navigate').subscribe(result => {
      expect(result.success).toBe(true);
      expect(result.entryId).toBe('id-1');
      expect(api.createManualJournalEntry).toHaveBeenCalled();
      done();
    });
  });

  it('returns validation error when unbalanced', (done) => {
    store.entryLabel.set('Test');
    store.lines.set([
      { ...createEmptyLine(), accountNumber: '607', debit: 100 },
      { ...createEmptyLine(), accountNumber: '4011', credit: 50 }
    ]);

    service.submit(store, [], 'navigate').subscribe(result => {
      expect(result.success).toBe(false);
      expect(api.createManualJournalEntry).not.toHaveBeenCalled();
      done();
    });
  });

  it('updates existing draft via PUT and does not create', (done) => {
    store.entryLabel.set('Modifié');
    store.editingEntryId.set('entry-42');
    store.lines.set([
      { ...createEmptyLine(), accountNumber: '607', debit: 100 },
      { ...createEmptyLine(), accountNumber: '4011', credit: 100 }
    ]);
    api.updateDraftJournalEntry.and.returnValue(of({ success: true, data: true }));

    service.submit(store, [], 'navigate').subscribe(result => {
      expect(result.success).toBe(true);
      expect(result.entryId).toBe('entry-42');
      expect(api.updateDraftJournalEntry).toHaveBeenCalled();
      expect(api.createManualJournalEntry).not.toHaveBeenCalled();
      done();
    });
  });
});
