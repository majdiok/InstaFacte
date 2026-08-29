import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { CashDeskService, CashOperationListItem } from '@core/services/cash-desk.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { HttpForbiddenDialogService } from '@core/services/http-forbidden-dialog.service';
import { HttpValidationDialogService } from '@core/services/http-validation-dialog.service';
import { errorInterceptor } from '@core/interceptors/error.interceptor';
import { SKIP_ERROR_TOAST } from '@core/http-context';
import { environment } from '@environments/environment';
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
            isFirmDelegatedReadonly: () => authReadonly,
            hasPermission: () => true,
            hasAllPermissions: () => true
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

describe('CashDeskComponent — permission gating', () => {
  function setup(perms: string[]) {
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
            getOperations: () =>
              of({ success: true, data: { items: [{ ...baseOperation }], totalCount: 1 } }),
            getFeatureFlags: () => of({ success: true, data: { vatEnabled: false } })
          }
        },
        {
          provide: AuthService,
          useValue: {
            isFirmDelegatedReadonly: () => false,
            hasPermission: (p: string) => perms.includes(p),
            hasAllPermissions: (ps: string[]) => ps.every(p => perms.includes(p)),
            user: () => null
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CashDeskComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('hides « Enregistrer une opération » and « Remise en banque » without payments:create', () => {
    const fixture = setup(['payments:update']);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Enregistrer une opération');
    expect(text).not.toContain('Remise en banque');
    expect(text).toContain('Autorisation « Trésorerie — saisie » requise.');
  });

  it('shows « Enregistrer une opération » and « Remise en banque » with payments:create', () => {
    const fixture = setup(['payments:create', 'payments:update']);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Enregistrer une opération');
    expect(text).toContain('Remise en banque');
  });

  it('hides the « Annuler » action without payments:update', () => {
    const fixture = setup(['payments:create']);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('button[aria-label="Annuler l\'opération"]')).toBeNull();
  });

  it('shows the « Annuler » action with payments:update', () => {
    const fixture = setup(['payments:create', 'payments:update']);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('button[aria-label="Annuler l\'opération"]')).not.toBeNull();
  });
});

describe('CashDeskComponent — submitCancel 403 & SKIP_ERROR_TOAST (intégration HTTP)', () => {
  let component: CashDeskComponent;
  let httpMock: HttpTestingController;
  let forbiddenDialog: jasmine.SpyObj<HttpForbiddenDialogService>;

  beforeEach(async () => {
    forbiddenDialog = jasmine.createSpyObj('HttpForbiddenDialogService', ['showAccessDenied']);

    await TestBed.configureTestingModule({
      imports: [CashDeskComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        CashDeskService,
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        ErrorHandlerService,
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: HttpForbiddenDialogService, useValue: forbiddenDialog },
        {
          provide: HttpValidationDialogService,
          useValue: jasmine.createSpyObj('HttpValidationDialogService', ['showValidationFailed'])
        },
        {
          provide: AuthService,
          useValue: {
            isFirmDelegatedReadonly: () => false,
            hasPermission: () => true,
            hasAllPermissions: () => true,
            logout: jasmine.createSpy('logout')
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CashDeskComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    // Pas de detectChanges : ngOnInit (loadAll) n'est pas déclenché → aucun appel HTTP de chargement.
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('porte SKIP_ERROR_TOAST sur POST /operations/{id}/cancel et n\'ouvre pas la modale globale sur 403', () => {
    (component as any).operationToCancel = baseOperation;
    component.cancelReason = 'Erreur de saisie';

    component.submitCancel();

    const req = httpMock.expectOne(
      r =>
        r.method === 'POST' &&
        r.url === `${environment.apiUrl}/cash-desk/operations/${baseOperation.id}/cancel`
    );
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();

    req.flush(
      { success: false, message: 'Permission payments:update requise.' },
      { status: 403, statusText: 'Forbidden' }
    );

    expect(component.cancelError()).toBe(
      'Action refusée : autorisations insuffisantes (Trésorerie — mise à jour).'
    );
    expect(forbiddenDialog.showAccessDenied).not.toHaveBeenCalled();
  });
});
