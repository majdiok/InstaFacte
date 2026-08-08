import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors, HttpContext } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { errorInterceptor } from './error.interceptor';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { HttpForbiddenDialogService } from '@core/services/http-forbidden-dialog.service';
import { HttpValidationDialogService } from '@core/services/http-validation-dialog.service';
import { AuthService } from '@core/services/auth.service';
import { SKIP_ERROR_TOAST } from '@core/http-context';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toastService: jasmine.SpyObj<ToastService>;
  let forbiddenDialog: jasmine.SpyObj<HttpForbiddenDialogService>;
  let validationDialog: jasmine.SpyObj<HttpValidationDialogService>;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    toastService = jasmine.createSpyObj('ToastService', ['add']);
    forbiddenDialog = jasmine.createSpyObj('HttpForbiddenDialogService', ['showAccessDenied']);
    validationDialog = jasmine.createSpyObj('HttpValidationDialogService', ['showValidationFailed']);
    authService = jasmine.createSpyObj('AuthService', ['logout']);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        ErrorHandlerService,
        { provide: ToastService, useValue: toastService },
        { provide: HttpForbiddenDialogService, useValue: forbiddenDialog },
        { provide: HttpValidationDialogService, useValue: validationDialog },
        { provide: AuthService, useValue: authService }
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('shows toast only for simple 400 without structured validation', (done) => {
    http.post('/api/test', {}).subscribe({
      error: () => {
        expect(validationDialog.showValidationFailed).not.toHaveBeenCalled();
        expect(toastService.add).toHaveBeenCalled();
        done();
      }
    });

    const req = httpMock.expectOne('/api/test');
    req.flush(
      { success: false, error: 'Erreur métier simple' },
      { status: 400, statusText: 'Bad Request' }
    );
    httpMock.verify();
  });

  it('shows validation dialog and not toast for VALIDATION_FAILED', (done) => {
    http.post('/api/invoices/validate', {}).subscribe({
      error: () => {
        expect(validationDialog.showValidationFailed).toHaveBeenCalled();
        expect(toastService.add).not.toHaveBeenCalled();
        done();
      }
    });

    const req = httpMock.expectOne('/api/invoices/validate');
    req.flush(
      {
        code: 'VALIDATION_FAILED',
        message: 'La validation a échoué.',
        fieldErrors: {
          identificationNumber: [{ message: 'Le numéro CIN doit être composé de 8 chiffres' }]
        }
      },
      { status: 400, statusText: 'Bad Request' }
    );
    httpMock.verify();
  });

  it('shows neither toast nor validation dialog on 404 GET when SKIP_ERROR_TOAST is set', done => {
    const ctx = new HttpContext().set(SKIP_ERROR_TOAST, true);

    http.get('/api/public/street/storefronts/unknown-slug', { context: ctx }).subscribe({
      error: () => {
        expect(toastService.add).not.toHaveBeenCalled();
        expect(validationDialog.showValidationFailed).not.toHaveBeenCalled();
        done();
      }
    });

    const req = httpMock.expectOne('/api/public/street/storefronts/unknown-slug');
    req.flush({ success: false, message: 'Vitrine introuvable.' }, { status: 404, statusText: 'Not Found' });
    httpMock.verify();
  });

  it('shows neither toast nor validation dialog when SKIP_ERROR_TOAST is set', (done) => {
    const ctx = new HttpContext().set(SKIP_ERROR_TOAST, true);

    http.post('/api/any', {}, { context: ctx }).subscribe({
      error: () => {
        expect(validationDialog.showValidationFailed).not.toHaveBeenCalled();
        expect(toastService.add).not.toHaveBeenCalled();
        done();
      }
    });

    const req = httpMock.expectOne('/api/any');
    req.flush(
      {
        code: 'VALIDATION_FAILED',
        fieldErrors: { a: [{ message: 'x' }] }
      },
      { status: 400, statusText: 'Bad Request' }
    );
    httpMock.verify();
  });

  it('shows conflict warning message for purchase-order confirmation 409', (done) => {
    http.patch('/api/purchaseorders/123/confirm', {}).subscribe({
      error: () => {
        expect(validationDialog.showValidationFailed).not.toHaveBeenCalled();
        expect(toastService.add).toHaveBeenCalled();
        const call = toastService.add.calls.mostRecent();
        expect(call.args[0].severity).toBe('warn');
        expect(call.args[0].summary).toContain('Conflit');
        done();
      }
    });

    const req = httpMock.expectOne('/api/purchaseorders/123/confirm');
    req.flush(
      { success: false, error: 'Cette commande ne peut pas être confirmée dans son état actuel' },
      { status: 409, statusText: 'Conflict' }
    );
    httpMock.verify();
  });

  it('suppresses global toast for honoraires payment 409 (dialog owns the message)', (done) => {
    http.post('/api/honoraires/invoices/fad25fd1-c365-40b0-a075-9c4880205fbd/payments', {}).subscribe({
      error: () => {
        expect(toastService.add).not.toHaveBeenCalled();
        expect(validationDialog.showValidationFailed).not.toHaveBeenCalled();
        done();
      }
    });

    const req = httpMock.expectOne(
      '/api/honoraires/invoices/fad25fd1-c365-40b0-a075-9c4880205fbd/payments'
    );
    req.flush(
      { code: 'CONCURRENCY_CONFLICT', message: 'Conflit de concurrence' },
      { status: 409, statusText: 'Conflict' }
    );
    httpMock.verify();
  });

  it('shows toast with server message when 400 body is JSON inside Blob (responseType blob)', (done) => {
    http
      .post('/api/withholding-tax/tej-export/generate', {}, { responseType: 'blob' })
      .subscribe({
        error: () => {
          expect(toastService.add).toHaveBeenCalled();
          const call = toastService.add.calls.mostRecent();
          expect(call.args[0].detail).toContain('Aucune facture fournisseur soldée');
          expect(validationDialog.showValidationFailed).not.toHaveBeenCalled();
          done();
        }
      });

    const req = httpMock.expectOne('/api/withholding-tax/tej-export/generate');
    req.flush(new Blob([JSON.stringify({ error: 'Aucune facture fournisseur soldée avec retenue à la source pour cette période' })], { type: 'application/json' }), {
      status: 400,
      statusText: 'Bad Request'
    });
    httpMock.verify();
  });

  it('does NOT log out the user on a 400 whose message contains "contexte"', (done) => {
    // Regression: a 400 from the application layer (e.g. EF Core tracking conflict translated
    // into "Erreur de contexte") used to trigger a false auto-logout. The interceptor must now
    // restrict tenant/context detection to status 403 and never log out on a 400.
    http.post('/api/deliverynotes', {}).subscribe({
      error: () => {
        expect(authService.logout).not.toHaveBeenCalled();
        expect(toastService.add).toHaveBeenCalled();
        const lastCall = toastService.add.calls.mostRecent();
        expect(lastCall.args[0].summary).not.toBe('Session invalide');
        done();
      }
    });

    const req = httpMock.expectOne('/api/deliverynotes');
    req.flush(
      { success: false, error: 'Erreur de contexte. Assurez-vous que vous êtes connecté à une entreprise valide.' },
      { status: 400, statusText: 'Bad Request' }
    );
    httpMock.verify();
  });

  it('logs out the user on a 403 with a tenant context message', (done) => {
    // Positive regression: a real tenant/context failure from TenantMiddleware comes as 403,
    // and must still trigger the auto-logout flow.
    http.get('/api/clients').subscribe({
      error: () => {
        expect(authService.logout).toHaveBeenCalled();
        const lastCall = toastService.add.calls.mostRecent();
        expect(lastCall.args[0].summary).toBe('Session invalide');
        done();
      }
    });

    const req = httpMock.expectOne('/api/clients');
    req.flush(
      { success: false, error: 'Contexte entreprise invalide. Veuillez vous reconnecter.' },
      { status: 403, statusText: 'Forbidden' }
    );
    httpMock.verify();
  });

  it('does not show forbidden dialog for 403 when SKIP_ERROR_TOAST is set', (done) => {
    const ctx = new HttpContext().set(SKIP_ERROR_TOAST, true);

    http.post('/api/ai/warm-up', {}, { context: ctx }).subscribe({
      error: () => {
        expect(forbiddenDialog.showAccessDenied).not.toHaveBeenCalled();
        expect(toastService.add).not.toHaveBeenCalled();
        done();
      }
    });

    const req = httpMock.expectOne('/api/ai/warm-up');
    req.flush({ success: false, message: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });
    httpMock.verify();
  });

  it('does not log when 401 and SKIP_ERROR_TOAST is set (background poll)', (done) => {
    const errorHandler = TestBed.inject(ErrorHandlerService);
    const logSpy = spyOn(errorHandler, 'logError');
    const ctx = new HttpContext().set(SKIP_ERROR_TOAST, true);

    http.get('/api/notifications', { context: ctx }).subscribe({
      error: () => {
        expect(logSpy).not.toHaveBeenCalled();
        done();
      }
    });

    const req = httpMock.expectOne('/api/notifications');
    req.flush({ success: false }, { status: 401, statusText: 'Unauthorized' });
    httpMock.verify();
  });
});
