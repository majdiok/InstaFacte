import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { ManualEntryComponent } from './manual-entry.component';
import { AccountingService } from '../services/accounting.service';
import { AuthService } from '@core/services/auth.service';
import { TaxService } from '@core/services/tax.service';
import { BankAccountService } from '@core/services/bank-account.service';
import { ClientService } from '@core/services/client.service';
import { SupplierService } from '@core/services/supplier.service';
import { RouterTestingModule } from '@angular/router/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';

describe('ManualEntryComponent', () => {
  let fixture: ComponentFixture<ManualEntryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ManualEntryComponent, RouterTestingModule],
      providers: [
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AccountingService,
          useValue: {
            getChartOfAccounts: () => of({ success: true, data: [] }),
            getJournals: () => of({ success: true, data: [] }),
            getPeriods: () => of({ success: true, data: [] }),
            getJournalTemplates: () => of({ success: true, data: [] }),
            createManualJournalEntry: () => of({ success: true, data: 'entry-id' }),
            updateDraftJournalEntry: () => of({ success: true, data: true }),
            getJournalEntry: () => of({ success: true, data: null }),
            createJournalTemplate: () => of({ success: true }),
            getThirdPartyProfile: () => of({ success: true, data: null }),
            runTemplateRecurrence: () => of({ success: true }),
            uploadEntryAttachment: () => of({ success: true, data: null })
          }
        },
        {
          provide: TaxService,
          useValue: { getVatRates: jasmine.createSpy('getVatRates').and.returnValue(of({ success: true, data: [{ id: '1', percent: 19, label: '19%' }] })) }
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
        },
        {
          provide: AuthService,
          useValue: {
            hasAllPermissions: () => true,
            hasPermission: () => true,
            isAccountingFirm: () => true,
            isDelegatedMode: () => true,
            user: signal(null)
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ManualEntryComponent);
    fixture.detectChanges();
  });

  it('renders four entry mode tabs', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Saisie standard');
    expect(el.textContent).toContain('Saisie guidée');
    expect(el.textContent).toContain('Saisie par modèle');
    expect(el.textContent).toContain('Saisie récurrente');
  });

  it('renders header form with journal field', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#eh-journal')).toBeTruthy();
    expect(el.textContent).toContain('Informations générales');
  });

  it('stays in create mode without entryId query param', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Saisie des écritures comptables');
    expect(el.textContent).toContain('Saisie guidée');
    expect(el.textContent).toContain('Enregistrer');
    expect(el.textContent).not.toContain('Enregistrer les modifications');
  });

  describe("import d'une pièce comptable", () => {
    function pressCtrlS(): void {
      document.dispatchEvent(
        new KeyboardEvent('keydown', { key: 's', ctrlKey: true, bubbles: true, cancelable: true })
      );
      fixture.detectChanges();
    }

    it('openDocumentImport ouvre la modale', () => {
      const cmp = fixture.componentInstance;
      expect(cmp.documentImportVisible()).toBeFalse();

      cmp.openDocumentImport();

      expect(cmp.documentImportVisible()).toBeTrue();
    });

    it('neutralise Ctrl+S tant que la modale d’import est ouverte', () => {
      const cmp = fixture.componentInstance;
      const submit = spyOn(cmp, 'submit');

      cmp.documentImportVisible.set(true);
      fixture.detectChanges();
      pressCtrlS();

      expect(submit)
        .withContext("Ctrl+S ne doit pas enregistrer pendant l'analyse d'une facture")
        .not.toHaveBeenCalled();
    });

    it('laisse Ctrl+S fonctionner quand la modale est fermée', () => {
      const cmp = fixture.componentInstance;
      const submit = spyOn(cmp, 'submit');

      // Écriture soumettable : deux lignes équilibrées.
      cmp.store.entryLabel.set('Test');
      const [first, second] = cmp.store.lines();
      cmp.store.setLines([
        { ...first, accountNumber: '607', lineLabel: 'A', debit: 100, credit: null },
        { ...second, accountNumber: '4011', lineLabel: 'B', debit: null, credit: 100 }
      ]);
      cmp.documentImportVisible.set(false);
      fixture.detectChanges();
      pressCtrlS();

      expect(submit).toHaveBeenCalled();
    });
  });
});

describe('ManualEntryComponent edit mode', () => {
  let fixture: ComponentFixture<ManualEntryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ManualEntryComponent, RouterTestingModule],
      providers: [
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap({
                entryId: 'entry-42',
                returnFrom: '2026-08-01',
                returnTo: '2026-08-31'
              })
            }
          }
        },
        {
          provide: AccountingService,
          useValue: {
            getChartOfAccounts: () => of({ success: true, data: [] }),
            getJournals: () => of({ success: true, data: [] }),
            getPeriods: () => of({ success: true, data: [] }),
            getJournalTemplates: () => of({ success: true, data: [] }),
            createManualJournalEntry: () => of({ success: true, data: 'entry-id' }),
            updateDraftJournalEntry: () => of({ success: true, data: true }),
            getJournalEntry: () => of({
              success: true,
              data: {
                id: 'entry-42',
                entryNumber: 6,
                journalCode: 'JV',
                entryDate: '2026-08-14',
                label: 'Client — FAC-2026-000019',
                sourceEntityType: 'Invoice',
                isAutoGenerated: true,
                isReversed: false,
                reversesEntryId: null,
                status: 0,
                isDraft: true,
                pieceRef: 'FAC-2026-000019',
                attachmentCount: 0,
                lines: [
                  { id: 'l1', lineNumber: 1, accountNumber: '4111', label: 'Client', debit: 100, credit: 0, currency: 'TND' },
                  { id: 'l2', lineNumber: 2, accountNumber: '707', label: 'Ventes', debit: 0, credit: 100, currency: 'TND' }
                ]
              }
            }),
            createJournalTemplate: () => of({ success: true }),
            getThirdPartyProfile: () => of({ success: true, data: null }),
            runTemplateRecurrence: () => of({ success: true }),
            uploadEntryAttachment: () => of({ success: true, data: null })
          }
        },
        {
          provide: TaxService,
          useValue: { getVatRates: jasmine.createSpy('getVatRates').and.returnValue(of({ success: true, data: [{ id: '1', percent: 19, label: '19%' }] })) }
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
        },
        {
          provide: AuthService,
          useValue: {
            hasAllPermissions: () => true,
            hasPermission: () => true,
            isAccountingFirm: () => true,
            isDelegatedMode: () => true,
            user: signal(null)
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ManualEntryComponent);
    fixture.detectChanges();
  });

  it('loads draft entry and switches to edit chrome', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Modification de l\'écriture JV n° 6 — Brouillon');
    expect(el.textContent).toContain('Enregistrer les modifications');
    expect(el.textContent).toContain('facture de vente');
    expect(el.textContent).not.toContain('Saisie guidée');
    const journal = el.querySelector('#eh-journal') as HTMLSelectElement | null;
    expect(journal?.disabled).toBeTrue();
    const date = el.querySelector('#eh-date') as HTMLInputElement | null;
    expect(date?.disabled).toBeTrue();
  });
});
