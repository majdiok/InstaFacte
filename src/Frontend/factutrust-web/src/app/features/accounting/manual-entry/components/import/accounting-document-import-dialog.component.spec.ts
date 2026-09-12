import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { AccountingDocumentImportDialogComponent } from './accounting-document-import-dialog.component';
import { AccountingDocumentImportService } from '../../services/accounting-document-import.service';
import { AccountingService } from '../../../services/accounting.service';
import { TaxService } from '@core/services/tax.service';
import { BankAccountService } from '@core/services/bank-account.service';
import { ClientService } from '@core/services/client.service';
import { SupplierService } from '@core/services/supplier.service';
import { JournalEntryProposal } from '../../models/accounting-document-import.models';
import { ImportProposalEditorComponent } from './import-proposal-editor.component';
import { ConfirmationService } from '@core/services/confirmation.service';

function sampleProposal(): JournalEntryProposal {
  return {
    direction: 'PURCHASE',
    directionReason: null,
    journalCode: 'JA',
    entryDate: '2026-08-01',
    label: 'Facture',
    pieceRef: 'FAC-1',
    pieceDate: '2026-08-01',
    thirdParty: null,
    lines: [
      {
        accountNumber: '607',
        accountLabel: 'Achats',
        accountStatus: 'found',
        role: 'expense',
        label: 'HT',
        debit: 100,
        credit: 0,
        vatRatePercent: null,
        thirdPartyId: null,
        thirdPartyKind: null
      },
      {
        accountNumber: '4011',
        accountLabel: 'Fournisseurs',
        accountStatus: 'found',
        role: 'thirdparty',
        label: 'Fournisseur',
        debit: 0,
        credit: 100,
        vatRatePercent: null,
        thirdPartyId: null,
        thirdPartyKind: null
      }
    ],
    totalDebit: 100,
    totalCredit: 100,
    documentTotalTtc: 100,
    extraction: {
      documentType: 'INVOICE',
      documentNumber: 'FAC-1',
      issueDate: '2026-08-01',
      dueDate: null,
      documentStatus: null,
      currency: 'TND',
      seller: null,
      buyer: null,
      lines: [],
      vatBreakdown: [],
      totalHt: 100,
      totalVat: 0,
      fodecAmount: null,
      fiscalStampAmount: null,
      withholdingAmount: null,
      totalTtc: 100,
      confidence: 'high',
      warnings: [],
      extractionMethod: 'native-pdf',
      ocrApplied: false,
      vatBreakdownRecomputed: false
    },
    diagnostics: [],
    hasBlockingDiagnostic: false
  };
}

describe('AccountingDocumentImportDialogComponent', () => {
  let fixture: ComponentFixture<AccountingDocumentImportDialogComponent>;
  let component: AccountingDocumentImportDialogComponent;
  let proposeSpy: jasmine.Spy;

  beforeEach(async () => {
    proposeSpy = jasmine.createSpy('propose').and.returnValue(of(sampleProposal()));

    await TestBed.configureTestingModule({
      imports: [AccountingDocumentImportDialogComponent],
      providers: [
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AccountingDocumentImportService,
          useValue: {
            capabilities: () => of({ enabled: true, aiFallbackAvailable: true }),
            propose: proposeSpy
          }
        },
        {
          provide: AccountingService,
          useValue: {
            // Le store de référence charge aussi les devises depuis ce service.
            getCurrencies: () => of({ success: true, data: [] }),
            getChartOfAccounts: () =>
              of({
                success: true,
                data: [{ id: '1', accountNumber: '607', label: 'Achats', accountClass: 6, isActive: true, isSystem: false, natureType: 0, level: 1 }]
              }),
            getPeriods: () => of({ success: true, data: [] }),
            getJournals: () => of({ success: true, data: [{ code: 'JA', label: 'Achats' }] })
          }
        },
        {
          provide: TaxService,
          useValue: {
            getVatRates: () => of({ success: true, data: [{ id: '1', percent: 19, label: '19%' }] })
          }
        },
        {
          provide: BankAccountService,
          useValue: { list: () => of({ success: true, data: [] }) }
        },
        {
          provide: ClientService,
          useValue: { getClients: () => of({ success: true, data: { items: [] } }) }
        },
        {
          provide: SupplierService,
          useValue: { getSuppliers: () => of({ success: true, data: { items: [] } }) }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AccountingDocumentImportDialogComponent);
    component = fixture.componentInstance;
    component.visible = true;
    fixture.detectChanges();
  });

  it('canApplyNow is false when proposal has blocking diagnostic', () => {
    component.proposal.set({
      ...sampleProposal(),
      diagnostics: [{ severity: 'blocking', code: 'PeriodClosed', message: 'Clôturé', relatedLineIndex: null }],
      hasBlockingDiagnostic: true
    });
    component.phase.set('review');
    fixture.detectChanges();
    expect(component.canApplyNow()).toBe(false);
  });

  it('changeDirection asks confirmation when manual edits exist', () => {
    const confirmSpy = spyOn(TestBed.inject(ConfirmationService), 'confirm');
    component.proposal.set(sampleProposal());
    component.phase.set('review');
    component['currentFile'] = new File(['x'], 'test.pdf');
    fixture.detectChanges();

    component.proposalEditor = {
      hasManualEdits: () => true
    } as ImportProposalEditorComponent;

    component.changeDirection('SALE');
    expect(confirmSpy).toHaveBeenCalled();
    expect(proposeSpy).not.toHaveBeenCalled();
  });
});
