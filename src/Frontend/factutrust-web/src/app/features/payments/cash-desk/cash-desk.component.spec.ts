import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { CashDeskService, CashOperationListItem } from '@core/services/cash-desk.service';
import { CashDeskComponent } from './cash-desk.component';

const baseOperation: CashOperationListItem = {
  id: 'op-1',
  operationType: 1,
  operationTypeDisplay: 'Encaissement',
  operationDate: '2026-08-15',
  method: 0,
  methodDisplay: 'Espèces',
  label: 'Vente comptant',
  category: null,
  categoryDisplay: null,
  revenueCategory: 0,
  revenueCategoryDisplay: 'Encaissement ventes au comptant',
  document: 'ENC-001',
  amount: 100,
  currency: 'TND',
  reference: null,
  notes: null,
  status: 0,
  origin: 0,
  sourceType: null,
  sourceId: null,
  sourceInvoiceId: null,
  sourceInvoiceNumber: null,
  sourceSupplierInvoiceId: null,
  sourceSupplierInvoiceNumber: null
};

describe('CashDeskComponent — firm delegated readonly', () => {
  let authReadonly: boolean;

  function createComponent(): CashDeskComponent {
    const fixture = TestBed.createComponent(CashDeskComponent);
    return fixture.componentInstance;
  }

  beforeEach(async () => {
    authReadonly = false;
    await TestBed.configureTestingModule({
      imports: [CashDeskComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: CashDeskService,
          useValue: {
            invalidateCachesAfterCashLedgerMutation: () => undefined,
            getBalances: () => of({ success: true, data: null }),
            getOperations: () => of({ success: true, data: { items: [], totalCount: 0 } }),
            getFeatureFlags: () => of({ success: true, data: { vatEnabled: false } })
          }
        },
        {
          provide: AuthService,
          useValue: {
            isFirmDelegatedReadonly: () => authReadonly
          }
        }
      ]
    }).compileComponents();
  });

  it('does not open add dialog when readonly', () => {
    authReadonly = true;
    const component = createComponent();
    component.openAddDialog();
    expect(component.addDialogVisible).toBe(false);
  });

  it('does not open bank deposit wizard when readonly', () => {
    authReadonly = true;
    const component = createComponent();
    component.openBankDepositWizard();
    expect(component.bankDepositWizardVisible).toBe(false);
  });

  it('opens add dialog when not readonly', () => {
    authReadonly = false;
    const component = createComponent();
    component.openAddDialog();
    expect(component.addDialogVisible).toBe(true);
  });
});

describe('CashDeskComponent — list rendering (TVA)', () => {
  function setup(operations: CashOperationListItem[]) {
    TestBed.configureTestingModule({
      imports: [CashDeskComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        {
          provide: CashDeskService,
          useValue: {
            invalidateCachesAfterCashLedgerMutation: () => undefined,
            getBalances: () => of({ success: true, data: null }),
            getOperations: () => of({ success: true, data: { items: operations, totalCount: operations.length } }),
            getFeatureFlags: () => of({ success: true, data: { vatEnabled: false } })
          }
        },
        {
          provide: AuthService,
          useValue: {
            isFirmDelegatedReadonly: () => false,
            hasAllPermissions: () => true,
            hasPermission: () => true,
            isAccountingFirm: () => false,
            isDelegatedMode: () => false,
            user: signal(null)
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CashDeskComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the list unchanged when no operation carries VAT data (non-regression)', () => {
    const fixture = setup([{ ...baseOperation }]);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Vente comptant');
    expect(text).toContain('ENC-001');
    expect(text).not.toContain('TVA 19 %');
    expect(text).not.toMatch(/TVA \d+ %/);
  });

  it('shows a discreet badge when vatRatePercent is present', () => {
    const fixture = setup([{ ...baseOperation, vatRatePercent: 19, htAmount: 84.034, vatAmount: 15.966 }]);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('TVA 19 %');
  });
});
