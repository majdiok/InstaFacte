import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { JournalComponent } from './journal.component';
import { AccountingService } from '../services/accounting.service';
import { AccountingMonitoringService } from '../shared/accounting-monitoring.service';
import { AuthService } from '@core/services/auth.service';

describe('JournalComponent', () => {
  let fixture: ComponentFixture<JournalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [JournalComponent],
      providers: [
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: AccountingService,
          useValue: {
            getJournal: () =>
              of({
                success: true,
                data: [
                  {
                    id: 'entry-1',
                    entryDate: '2026-01-15',
                    journalCode: 'JV',
                    entryNumber: 1,
                    label: 'Test',
                    status: 0,
                    isDraft: true,
                    isReversed: false,
                    pieceRef: null,
                    attachmentCount: 0,
                    lines: [{ label: 'Ligne', accountNumber: '411', debit: 100, credit: 0 }]
                  }
                ]
              }),
            exportJournal: () => of(new Blob()),
            validateJournalEntry: () => of({ success: true }),
            reverseJournalEntry: () => of({ success: true }),
            // Catalogue de journaux : vide ici, le sélecteur retombe sur les journaux standards.
            getJournals: () => of({ success: true, data: [] }),
            getJournalEntryAttachments: () => of({ success: true, data: [] }),
            uploadJournalEntryAttachment: () => of({ success: true }),
            deleteJournalEntryAttachment: () => of({ success: true }),
            downloadJournalEntryAttachment: () => of(new Blob())
          }
        },
        { provide: AccountingMonitoringService, useValue: { logError: jasmine.createSpy('logError') } },
        {
          // Depuis la réserve de la validation au cabinet en mode délégué, l'action « Valider »
          // n'apparaît que pour un tel utilisateur : le doublon doit donc en incarner un, sinon
          // le scénario teste un écran où le bouton est légitimement absent.
          provide: AuthService,
          useValue: {
            hasAllPermissions: () => true,
            hasPermission: () => true,
            isAccountingFirm: () => true,
            isDelegatedMode: () => true
          }
        }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(JournalComponent);
    fixture.detectChanges();
  });

  it('renders validate and attachment actions for draft entries', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Valider');
    expect(el.querySelector('app-accounting-table-actions')).toBeTruthy();
    expect(el.querySelectorAll('app-button').length).toBeGreaterThan(2);
  });

  it('renders harmonized toolbar actions', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Actualiser');
    expect(el.textContent).toContain('Exporter');
    expect(el.querySelector('app-accounting-toolbar-actions')).toBeTruthy();
  });
});
