import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { ToastService } from '@core/services/toast.service';
import { BankAccountService, BankAccountDto } from '@core/services/bank-account.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { HttpForbiddenDialogService } from '@core/services/http-forbidden-dialog.service';
import { HttpValidationDialogService } from '@core/services/http-validation-dialog.service';
import { errorInterceptor } from '@core/interceptors/error.interceptor';
import { SKIP_ERROR_TOAST } from '@core/http-context';
import { environment } from '@environments/environment';
import { BankAccountsComponent } from './bank-accounts.component';

describe('BankAccountsComponent — firm delegated readonly', () => {
  let fixture: ComponentFixture<BankAccountsComponent>;
  let authReadonly: boolean;

  const sampleAccount = {
    id: 'a1',
    designation: 'Compte principal',
    bankName: 'STB',
    bankCode: '10',
    iban: 'TN5900100000000000000000',
    rib: '10 000 0000000000 00',
    isDefault: false
  };

  beforeEach(async () => {
    authReadonly = false;
    await TestBed.configureTestingModule({
      imports: [BankAccountsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: BankAccountService,
          useValue: {
            list: () => of({ success: true, data: [sampleAccount] })
          }
        },
        {
          provide: ConfirmationService,
          useValue: { confirm: jasmine.createSpy('confirm') }
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

    fixture = TestBed.createComponent(BankAccountsComponent);
  });

  it('shows CRUD buttons for non-readonly users', () => {
    authReadonly = false;
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent ?? '';
    expect(text).toContain('Ajouter un compte');
    expect(text).toContain('Modifier');
    expect(text).toContain('Supprimer');
  });

  it('hides CRUD buttons in firm delegated readonly mode', () => {
    authReadonly = true;
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent ?? '';
    expect(text).not.toContain('Ajouter un compte');
    expect(text).not.toContain('Modifier');
    expect(text).not.toContain('Supprimer');
    expect(text).not.toContain('Défaut');
  });

  it('does not open create dialog when readonly', () => {
    authReadonly = true;
    fixture.detectChanges();
    fixture.componentInstance.openCreate();
    expect(fixture.componentInstance.dialogVisible).toBe(false);
  });
});

const fullAccount: BankAccountDto = {
  id: 'a1',
  companyId: 'c1',
  designation: 'Compte principal',
  bankName: 'STB',
  bankCode: '10',
  agencyName: null,
  rib: '10000000000000000000',
  iban: 'TN5910000000000000000000',
  swiftBic: null,
  isDefault: false,
  isActive: true,
  chartOfAccountNumber: null,
  currency: 'TND'
};

describe('BankAccountsComponent — permission gating', () => {
  function setup(perms: string[]) {
    TestBed.configureTestingModule({
      imports: [BankAccountsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        {
          provide: BankAccountService,
          useValue: { list: () => of({ success: true, data: [{ ...fullAccount }] }) }
        },
        { provide: ConfirmationService, useValue: { confirm: jasmine.createSpy('confirm') } },
        {
          provide: AuthService,
          useValue: {
            isFirmDelegatedReadonly: () => false,
            hasPermission: (p: string) => perms.includes(p),
            hasAllPermissions: (ps: string[]) => ps.every(p => perms.includes(p))
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(BankAccountsComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('hides « Ajouter un compte » without payments:create', () => {
    const fixture = setup(['payments:update']);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Ajouter un compte');
  });

  it('shows « Ajouter un compte » with payments:create', () => {
    const fixture = setup(['payments:create', 'payments:update']);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Ajouter un compte');
  });

  it('hides edit/delete/setDefault actions without payments:update', () => {
    const fixture = setup(['payments:create']);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Modifier');
    expect(text).not.toContain('Supprimer');
    expect(text).not.toContain('Défaut');
  });

  it('shows edit/delete/setDefault actions with payments:update', () => {
    const fixture = setup(['payments:create', 'payments:update']);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Modifier');
    expect(text).toContain('Supprimer');
    expect(text).toContain('Défaut');
  });
});

describe('BankAccountsComponent — 403 & SKIP_ERROR_TOAST (intégration HTTP)', () => {
  let component: BankAccountsComponent;
  let httpMock: HttpTestingController;
  let forbiddenDialog: jasmine.SpyObj<HttpForbiddenDialogService>;
  let toastAdd: jasmine.Spy;
  let confirmSpy: jasmine.Spy;

  beforeEach(async () => {
    forbiddenDialog = jasmine.createSpyObj('HttpForbiddenDialogService', ['showAccessDenied']);
    toastAdd = jasmine.createSpy('add');
    confirmSpy = jasmine.createSpy('confirm');

    await TestBed.configureTestingModule({
      imports: [BankAccountsComponent],
      providers: [
        provideNoopAnimations(),
        BankAccountService,
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        ErrorHandlerService,
        { provide: ToastService, useValue: { add: toastAdd } },
        { provide: HttpForbiddenDialogService, useValue: forbiddenDialog },
        {
          provide: HttpValidationDialogService,
          useValue: jasmine.createSpyObj('HttpValidationDialogService', ['showValidationFailed'])
        },
        { provide: ConfirmationService, useValue: { confirm: confirmSpy } },
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

    const fixture = TestBed.createComponent(BankAccountsComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    // Pas de detectChanges : ngOnInit (load) non déclenché → pas d'appel HTTP de chargement.
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('setDefault: porte SKIP_ERROR_TOAST, toast français sur 403, pas de modale globale', () => {
    component.setDefault(fullAccount);

    const req = httpMock.expectOne(
      r => r.method === 'POST' && r.url === `${environment.apiUrl}/bank-accounts/${fullAccount.id}/set-default`
    );
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    req.flush({ success: false, message: 'Permission payments:update requise.' }, { status: 403, statusText: 'Forbidden' });

    expect(toastAdd).toHaveBeenCalledWith(
      jasmine.objectContaining({
        severity: 'error',
        detail: 'Action refusée : autorisations insuffisantes (Trésorerie — mise à jour).'
      })
    );
    expect(forbiddenDialog.showAccessDenied).not.toHaveBeenCalled();
  });

  it('delete: porte SKIP_ERROR_TOAST, toast français sur 403, pas de modale globale', () => {
    confirmSpy.and.callFake((opts: { accept: () => void }) => opts.accept());

    component.confirmDelete(fullAccount);

    const req = httpMock.expectOne(
      r => r.method === 'DELETE' && r.url === `${environment.apiUrl}/bank-accounts/${fullAccount.id}`
    );
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    req.flush({ success: false, message: 'Permission payments:update requise.' }, { status: 403, statusText: 'Forbidden' });

    expect(toastAdd).toHaveBeenCalledWith(
      jasmine.objectContaining({
        severity: 'error',
        detail: 'Action refusée : autorisations insuffisantes (Trésorerie — mise à jour).'
      })
    );
    expect(forbiddenDialog.showAccessDenied).not.toHaveBeenCalled();
  });
});
