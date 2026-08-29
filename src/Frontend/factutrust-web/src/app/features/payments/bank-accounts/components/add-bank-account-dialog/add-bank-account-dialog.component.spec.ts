import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { BankAccountService, BankAccountDto } from '@core/services/bank-account.service';
import { AccountingService } from '@features/accounting/services/accounting.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { HttpForbiddenDialogService } from '@core/services/http-forbidden-dialog.service';
import { HttpValidationDialogService } from '@core/services/http-validation-dialog.service';
import { AuthService } from '@core/services/auth.service';
import { errorInterceptor } from '@core/interceptors/error.interceptor';
import { SKIP_ERROR_TOAST } from '@core/http-context';
import { environment } from '@environments/environment';
import { AddBankAccountDialogComponent } from './add-bank-account-dialog.component';

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

describe('AddBankAccountDialogComponent — 403 & SKIP_ERROR_TOAST (intégration HTTP)', () => {
  let component: AddBankAccountDialogComponent;
  let httpMock: HttpTestingController;
  let forbiddenDialog: jasmine.SpyObj<HttpForbiddenDialogService>;

  beforeEach(async () => {
    forbiddenDialog = jasmine.createSpyObj('HttpForbiddenDialogService', ['showAccessDenied']);

    await TestBed.configureTestingModule({
      imports: [AddBankAccountDialogComponent],
      providers: [
        provideNoopAnimations(),
        BankAccountService,
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        ErrorHandlerService,
        { provide: AccountingService, useValue: { getChartOfAccounts: () => of({ success: true, data: [] }) } },
        { provide: ToastService, useValue: { add: jasmine.createSpy('add') } },
        { provide: HttpForbiddenDialogService, useValue: forbiddenDialog },
        {
          provide: HttpValidationDialogService,
          useValue: jasmine.createSpyObj('HttpValidationDialogService', ['showValidationFailed'])
        },
        { provide: AuthService, useValue: jasmine.createSpyObj('AuthService', ['logout']) }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AddBankAccountDialogComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    // Pas de detectChanges : ngOnChanges (loadBanksAndForm) non déclenché → pas d'appels HTTP de chargement.

    // Forme valide : IBAN tunisien + RIB dérivé.
    component.form.controls.bankCode.setValue('10');
    component.form.controls.bankName.setValue('STB');
    component.form.controls.iban.setValue('TN5910000000000000000000');
    component.syncRibFromIban();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('create: porte SKIP_ERROR_TOAST, message français « saisie » sur 403, pas de modale globale', () => {
    component.editingAccount = null;
    component.submit();

    const req = httpMock.expectOne(
      r => r.method === 'POST' && r.url === `${environment.apiUrl}/bank-accounts`
    );
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    req.flush({ success: false, message: 'Permission payments:create requise.' }, { status: 403, statusText: 'Forbidden' });

    expect(component.errorMessage()).toBe(
      'Action refusée : autorisations insuffisantes (Trésorerie — saisie).'
    );
    expect(forbiddenDialog.showAccessDenied).not.toHaveBeenCalled();
  });

  it('update: porte SKIP_ERROR_TOAST, message français « mise à jour » sur 403, pas de modale globale', () => {
    component.editingAccount = fullAccount;
    component.submit();

    const req = httpMock.expectOne(
      r => r.method === 'PUT' && r.url === `${environment.apiUrl}/bank-accounts/${fullAccount.id}`
    );
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    req.flush({ success: false, message: 'Permission payments:update requise.' }, { status: 403, statusText: 'Forbidden' });

    expect(component.errorMessage()).toBe(
      'Action refusée : autorisations insuffisantes (Trésorerie — mise à jour).'
    );
    expect(forbiddenDialog.showAccessDenied).not.toHaveBeenCalled();
  });
});
