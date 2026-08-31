import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { AdjustableRecurringDraft, RecurringContractService } from '@core/services/recurring-contract.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { DraftAdjustDialogComponent } from './draft-adjust-dialog.component';

function draft(partial: Partial<AdjustableRecurringDraft> = {}): AdjustableRecurringDraft {
  return {
    billingRunId: 'run-1',
    invoiceDraftId: 'draft-1',
    contractId: 'ctr-1',
    currency: 'TND',
    periodFrom: '2026-06-01',
    periodTo: '2026-06-30',
    contractBillingFrequency: 'Monthly',
    isConverted: false,
    expiresAt: '2026-07-30T00:00:00Z',
    lines: [{
      index: 0,
      productId: 'prod-1',
      productName: 'Abonnement Cloud',
      designation: 'Abonnement',
      quantity: 1,
      unitPriceHT: 100,
      vatRate: 19,
      fodecApplicable: false
    }],
    totals: {
      totalHT: 100,
      totalVat: 19,
      totalFodec: 0,
      fiscalStampAmount: 0,
      totalTTC: 119,
      currency: 'TND'
    },
    ...partial
  };
}

describe('DraftAdjustDialogComponent', () => {
  let service: jasmine.SpyObj<RecurringContractService>;
  let confirmation: jasmine.SpyObj<ConfirmationService>;
  let toast: jasmine.SpyObj<ToastService>;

  function createComponent(): DraftAdjustDialogComponent {
    service = jasmine.createSpyObj('RecurringContractService', [
      'getAdjustableDraft', 'adjustDraftLines', 'issueBillingRun'
    ]);
    confirmation = jasmine.createSpyObj('ConfirmationService', ['confirm']);
    toast = jasmine.createSpyObj('ToastService', ['add']);
    const errors = jasmine.createSpyObj('ErrorHandlerService', ['logError', 'extractErrorMessage']);
    errors.extractErrorMessage.and.returnValue('erreur');

    service.getAdjustableDraft.and.returnValue(of(draft()));
    service.adjustDraftLines.and.returnValue(of(draft({
      lines: [{
        index: 0,
        productId: 'prod-1',
        productName: 'Abonnement Cloud',
        designation: 'Abonnement',
        quantity: 2,
        unitPriceHT: 150,
        vatRate: 19,
        fodecApplicable: false
      }]
    })));
    service.issueBillingRun.and.returnValue(of({
      invoiceId: 'inv-1',
      invoiceNumber: 'FAC-2026-0001',
      status: 'Validée',
      createdAt: '2026-06-01T00:00:00Z'
    }));
    confirmation.confirm.and.callFake((cfg: { accept?: () => void }) => cfg.accept?.());

    TestBed.configureTestingModule({
      imports: [DraftAdjustDialogComponent],
      providers: [
        provideNoopAnimations(),
        { provide: RecurringContractService, useValue: service },
        { provide: ConfirmationService, useValue: confirmation },
        { provide: ToastService, useValue: toast },
        { provide: ErrorHandlerService, useValue: errors }
      ]
    });

    const cmp = TestBed.createComponent(DraftAdjustDialogComponent).componentInstance;
    cmp.billingRunId = 'run-1';
    cmp.onShow();
    return cmp;
  }

  it('charge le brouillon et n\'expose pas d\'ajout / suppression de ligne', () => {
    const cmp = createComponent();
    expect(service.getAdjustableDraft).toHaveBeenCalledWith('run-1');
    expect(cmp.lines.length).toBe(1);
    expect(cmp.lines[0].productName).toBe('Abonnement Cloud');
    expect(cmp.lines[0].vatRate).toBe(19);
    expect((cmp as unknown as { addLine?: unknown }).addLine).toBeUndefined();
    expect((cmp as unknown as { removeLine?: unknown }).removeLine).toBeUndefined();
  });

  it('refuse l\'enregistrement si la quantité est nulle', () => {
    const cmp = createComponent();
    cmp.lines[0].quantity = 0;
    expect(cmp.canPersist()).toBeFalse();
  });

  it('Enregistrer appelle uniquement le PATCH whitelist puis émet saved', () => {
    const cmp = createComponent();
    const saved = jasmine.createSpy('saved');
    cmp.saved.subscribe(saved);
    cmp.lines[0].quantity = 2;
    cmp.lines[0].unitPriceHT = 150;
    cmp.save();

    expect(service.adjustDraftLines).toHaveBeenCalledWith('run-1', [
      { index: 0, designation: 'Abonnement', quantity: 2, unitPriceHT: 150 }
    ]);
    expect(service.issueBillingRun).not.toHaveBeenCalled();
    expect(saved).toHaveBeenCalled();
  });

  it('Émettre persiste le brouillon dirty puis réutilise issueBillingRun', () => {
    const cmp = createComponent();
    const issued = jasmine.createSpy('issued');
    cmp.issued.subscribe(issued);
    cmp.lines[0].quantity = 2;
    cmp.lines[0].unitPriceHT = 150;
    cmp.confirmIssue();

    expect(service.adjustDraftLines).toHaveBeenCalled();
    expect(service.issueBillingRun).toHaveBeenCalledWith('run-1');
    expect(issued).toHaveBeenCalled();
    expect(issued.calls.mostRecent().args[0].invoiceNumber).toBe('FAC-2026-0001');
  });

  it('Émetteur sans modification n\'appelle pas le PATCH', () => {
    const cmp = createComponent();
    cmp.confirmIssue();

    expect(service.adjustDraftLines).not.toHaveBeenCalled();
    expect(service.issueBillingRun).toHaveBeenCalledWith('run-1');
  });

  it('n\'émet pas si le PATCH préalable échoue', () => {
    const cmp = createComponent();
    service.adjustDraftLines.and.returnValue(throwError(() => ({ status: 400 })));
    cmp.lines[0].quantity = 3;
    cmp.confirmIssue();

    expect(service.issueBillingRun).not.toHaveBeenCalled();
  });
});
