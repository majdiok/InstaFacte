import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService, User } from '@core/services/auth.service';
import { FirmContextService } from '@core/services/firm-context.service';
import { ToastService } from '@core/services/toast.service';
import { firmNativeGuard } from './accounting-firms.guard';

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

function setUser(auth: AuthService, u: User | null): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

describe('firmNativeGuard', () => {
  let firmContext: jasmine.SpyObj<FirmContextService>;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    firmContext = jasmine.createSpyObj('FirmContextService', ['clearContext']);
    toast = jasmine.createSpyObj('ToastService', ['add']);

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: FirmContextService, useValue: firmContext },
        { provide: ToastService, useValue: toast }
      ]
    });
  });

  it('allows native firm user on /firm/dashboard without calling clearContext', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, firmUser);

    const result = await TestBed.runInInjectionContext(() =>
      firmNativeGuard({} as never, { url: '/firm/dashboard' } as never)
    );

    expect(result).toBe(true);
    expect(firmContext.clearContext).not.toHaveBeenCalled();
  });

  it('calls clearContext when delegated user navigates to /firm/dashboard', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    firmContext.clearContext.and.resolveTo(true);

    const result = await TestBed.runInInjectionContext(() =>
      firmNativeGuard({} as never, { url: '/firm/dashboard' } as never)
    );

    expect(firmContext.clearContext).toHaveBeenCalled();
    expect(result).toBe(true);
  });

  it('does not call clearContext when delegated user navigates to /firm/open/:tenantId', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);

    const result = await TestBed.runInInjectionContext(() =>
      firmNativeGuard(
        {} as never,
        { url: '/firm/open/00000000-0000-0000-0000-000000000003' } as never
      )
    );

    expect(firmContext.clearContext).not.toHaveBeenCalled();
    expect(result).toBe(true);
  });

  it('blocks navigation when clearContext fails for delegated user', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    firmContext.clearContext.and.resolveTo(false);

    const result = await TestBed.runInInjectionContext(() =>
      firmNativeGuard({} as never, { url: '/firm/clients' } as never)
    );

    expect(result).toBe(false);
    expect(toast.add).toHaveBeenCalled();
  });

  it('redirects non-firm user to /dashboard', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, { ...firmUser, role: 'Administrator', tenantKind: 'Company' });

    const result = await TestBed.runInInjectionContext(() =>
      firmNativeGuard({} as never, { url: '/firm/dashboard' } as never)
    );

    expect(result).not.toBe(true);
    const router = TestBed.inject(Router);
    expect(router.serializeUrl(result as never)).toContain('/dashboard');
  });
});
