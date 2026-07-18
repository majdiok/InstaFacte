import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { AuthService, User } from '@core/services/auth.service';
import { documentationAccessGuard } from './documentation-access.guard';

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

const companyUser: User = {
  ...firmUser,
  role: 'Administrator',
  roleDisplay: 'Administrateur',
  tenantId: '00000000-0000-0000-0000-000000000001',
  companyName: 'Société Test',
  tenantKind: 'Company'
};

const delegatedFirmUser: User = {
  ...firmUser,
  accessMode: 'delegated',
  contextTenantId: '00000000-0000-0000-0000-000000000003',
  contextCompanyName: 'Ste Bouzgarou'
};

function setUser(auth: AuthService, u: User | null): void {
  (auth as unknown as { userSignal: { set: (x: User | null) => void } }).userSignal.set(u);
}

describe('documentationAccessGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient()]
    });
  });

  it('allows non-documentation routes', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUser);

    const result = await TestBed.runInInjectionContext(() =>
      documentationAccessGuard({} as never, { url: '/dashboard' } as never)
    );

    expect(result).toBe(true);
  });

  it('redirects company user from documentation chapter to /dashboard', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, companyUser);

    const result = await TestBed.runInInjectionContext(() =>
      documentationAccessGuard({} as never, { url: '/documentation/rapports' } as never)
    );

    const router = TestBed.inject(Router);
    expect(router.serializeUrl(result as never)).toContain('/dashboard');
  });

  it('redirects firm native user from /documentation to /firm/dashboard', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, firmUser);

    const result = await TestBed.runInInjectionContext(() =>
      documentationAccessGuard({} as never, { url: '/documentation' } as never)
    );

    const router = TestBed.inject(Router);
    expect(router.serializeUrl(result as never)).toContain('/firm/dashboard');
  });

  it('redirects delegated firm user from documentation to /dashboard', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedFirmUser);

    const result = await TestBed.runInInjectionContext(() =>
      documentationAccessGuard({} as never, { url: '/documentation/ventes' } as never)
    );

    const router = TestBed.inject(Router);
    expect(router.serializeUrl(result as never)).toContain('/dashboard');
  });
});
