import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { environment } from '@environments/environment';
import { SKIP_ERROR_TOAST } from '@core/http-context';
import { AuthService, User } from './auth.service';
import { FirmContextService } from './firm-context.service';
import { ToastService } from './toast.service';

const firmUser: User = {
  id: 'u1',
  email: 'firm@test.c',
  firstName: 'O',
  lastName: 'G',
  fullName: 'O G',
  role: 'FirmManager',
  roleDisplay: 'Responsable cabinet',
  tenantId: '00000000-0000-0000-0000-000000000002',
  companyName: 'Cabinet Test',
  tenantKind: 'AccountingFirm',
  twoFactorEnabled: false
};

const delegatedUser: User = {
  ...firmUser,
  accessMode: 'delegated',
  contextTenantId: '00000000-0000-0000-0000-000000000003',
  contextCompanyName: 'Ste Bouzgarou'
};

const nativeAuthResponse = {
  accessToken: 'access',
  refreshToken: 'refresh',
  expiresAt: new Date().toISOString(),
  requires2Fa: false,
  user: { ...firmUser, accessMode: 'native' as const }
};

function setUser(auth: AuthService, u: User | null): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

function flushAiWarmUp(http: HttpTestingController): void {
  http.match(r => r.url.includes('/ai/warm-up')).forEach(req =>
    req.flush({ success: true, data: null, message: null, errors: [] })
  );
}

describe('FirmContextService', () => {
  let service: FirmContextService;
  let httpMock: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    service = TestBed.inject(FirmContextService);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('clearContext returns true without API call when already native', async () => {
    setUser(auth, firmUser);
    const result = await service.clearContext();
    expect(result).toBe(true);
    httpMock.expectNone(`${environment.apiUrl}/firm/context/clear`);
  });

  it('clearContext applies native tokens and returns true', async () => {
    setUser(auth, delegatedUser);
    const promise = service.clearContext();
    const req = httpMock.expectOne(`${environment.apiUrl}/firm/context/clear`);
    expect(req.request.method).toBe('POST');
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(true);
    req.flush({ success: true, data: nativeAuthResponse, message: null, errors: [] });
    const result = await promise;
    flushAiWarmUp(httpMock);
    expect(result).toBe(true);
    expect(auth.user()?.accessMode).not.toBe('delegated');
    expect(auth.user()?.contextTenantId).toBeUndefined();
  });

  it('clearContext returns false when API reports failure', async () => {
    setUser(auth, delegatedUser);
    const promise = service.clearContext();
    const req = httpMock.expectOne(`${environment.apiUrl}/firm/context/clear`);
    req.flush({ success: false, data: null, message: 'error', errors: [] });
    const result = await promise;
    expect(result).toBe(false);
  });

  it('switchClient sends SKIP_ERROR_TOAST and applies delegated tokens', async () => {
    setUser(auth, firmUser);
    const delegatedResponse = {
      ...nativeAuthResponse,
      user: { ...firmUser, accessMode: 'delegated' as const, contextTenantId: delegatedUser.contextTenantId }
    };
    const promise = service.switchClient(delegatedUser.contextTenantId!);
    const req = httpMock.expectOne(`${environment.apiUrl}/firm/context/switch`);
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(true);
    req.flush({ success: true, data: delegatedResponse, message: null, errors: [] });
    await promise;
    flushAiWarmUp(httpMock);
    expect(auth.user()?.accessMode).toBe('delegated');
  });

  it('returnToFirmHome navigates to /firm/dashboard on success', async () => {
    setUser(auth, delegatedUser);
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    const promise = service.returnToFirmHome();
    const req = httpMock.expectOne(`${environment.apiUrl}/firm/context/clear`);
    req.flush({ success: true, data: nativeAuthResponse, message: null, errors: [] });
    const result = await promise;
    flushAiWarmUp(httpMock);

    expect(result).toBe(true);
    expect(router.navigate).toHaveBeenCalledWith(['/firm/dashboard']);
  });

  it('returnToFirmHome shows toast and returns false on failure', async () => {
    setUser(auth, delegatedUser);
    const toast = TestBed.inject(ToastService);
    spyOn(toast, 'add');

    const promise = service.returnToFirmHome();
    const req = httpMock.expectOne(`${environment.apiUrl}/firm/context/clear`);
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBe(true);
    req.flush({ success: false, data: null, message: 'error', errors: [] });
    const result = await promise;

    expect(result).toBe(false);
    expect(toast.add).toHaveBeenCalled();
  });
});
