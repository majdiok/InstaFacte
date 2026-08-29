import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BankDepositService, BankDepositType, CreateBankDepositPayload } from './bank-deposit.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { ToastService } from '@core/services/toast.service';
import { HttpForbiddenDialogService } from '@core/services/http-forbidden-dialog.service';
import { HttpValidationDialogService } from '@core/services/http-validation-dialog.service';
import { AuthService } from '@core/services/auth.service';
import { errorInterceptor } from '@core/interceptors/error.interceptor';
import { SKIP_ERROR_TOAST } from '@core/http-context';
import { environment } from '@environments/environment';

describe('BankDepositService — SKIP_ERROR_TOAST sur create & cancel', () => {
  let service: BankDepositService;
  let httpMock: HttpTestingController;
  let forbiddenDialog: jasmine.SpyObj<HttpForbiddenDialogService>;

  const depositPayload: CreateBankDepositPayload = {
    depositType: BankDepositType.Cash,
    depositDate: '2026-08-15',
    bankAccountId: 'a1',
    amount: 100,
    quantity: 1
  };

  beforeEach(() => {
    forbiddenDialog = jasmine.createSpyObj('HttpForbiddenDialogService', ['showAccessDenied']);

    TestBed.configureTestingModule({
      providers: [
        BankDepositService,
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        ErrorHandlerService,
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['add']) },
        { provide: HttpForbiddenDialogService, useValue: forbiddenDialog },
        {
          provide: HttpValidationDialogService,
          useValue: jasmine.createSpyObj('HttpValidationDialogService', ['showValidationFailed'])
        },
        { provide: AuthService, useValue: jasmine.createSpyObj('AuthService', ['logout']) }
      ]
    });

    service = TestBed.inject(BankDepositService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('create porte SKIP_ERROR_TOAST et ne déclenche pas la modale globale sur 403', () => {
    service.create(depositPayload).subscribe({ error: () => undefined });

    const req = httpMock.expectOne(r => r.method === 'POST' && r.url === `${environment.apiUrl}/bank-deposits`);
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    req.flush({ success: false, message: 'Permission payments:create requise.' }, { status: 403, statusText: 'Forbidden' });

    expect(forbiddenDialog.showAccessDenied).not.toHaveBeenCalled();
  });

  it('cancel porte SKIP_ERROR_TOAST et ne déclenche pas la modale globale sur 403', () => {
    service.cancel('dep-1', 'Erreur').subscribe({ error: () => undefined });

    const req = httpMock.expectOne(
      r => r.method === 'POST' && r.url === `${environment.apiUrl}/bank-deposits/dep-1/cancel`
    );
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    req.flush({ success: false, message: 'Permission payments:update requise.' }, { status: 403, statusText: 'Forbidden' });

    expect(forbiddenDialog.showAccessDenied).not.toHaveBeenCalled();
  });
});
