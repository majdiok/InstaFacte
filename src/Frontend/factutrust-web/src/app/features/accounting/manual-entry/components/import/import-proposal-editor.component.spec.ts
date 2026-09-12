import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { ImportProposalEditorComponent } from './import-proposal-editor.component';
import { AccountingService } from '../../../services/accounting.service';
import { TaxService } from '@core/services/tax.service';
import { BankAccountService } from '@core/services/bank-account.service';
import { ClientService } from '@core/services/client.service';
import { SupplierService } from '@core/services/supplier.service';
import { JournalEntryProposal } from '../../models/accounting-document-import.models';

function sampleProposal(): JournalEntryProposal {
  return {
    direction: 'PURCHASE',
    directionReason: null,
    journalCode: 'JA',
    entryDate: '2026-08-01',
    label: 'Facture test',
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

describe('ImportProposalEditorComponent', () => {
  let fixture: ComponentFixture<ImportProposalEditorComponent>;
  let component: ImportProposalEditorComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ImportProposalEditorComponent],
      providers: [
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
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

    fixture = TestBed.createComponent(ImportProposalEditorComponent);
    component = fixture.componentInstance;
    component.proposal = sampleProposal();
    fixture.detectChanges();
  });

  it('initializes from proposal and exposes merged label', () => {
    const merged = component.getMergedProposal();
    expect(merged.label).toBe('Facture test');
    expect(merged.lines.length).toBe(2);
    expect(component.isBalanced()).toBe(true);
  });

  it('reports manual edits after label change', () => {
    expect(component.hasManualEdits()).toBe(false);
    const labelDe = fixture.debugElement.query(By.css('#eh-label'));
    labelDe.triggerEventHandler('ngModelChange', 'Libellé modifié');
    fixture.detectChanges();
    expect(component.hasManualEdits()).toBe(true);
    expect(component.getMergedProposal().label).toBe('Libellé modifié');
  });
});
