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
});
