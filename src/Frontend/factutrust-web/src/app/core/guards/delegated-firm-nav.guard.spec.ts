import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService, User } from '@core/services/auth.service';
import { delegatedFirmNavGuard } from './delegated-firm-nav.guard';

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

describe('delegatedFirmNavGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('allows company user on /quotes', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, { ...firmUser, role: 'Administrator', tenantKind: 'Company' });
    const result = await TestBed.runInInjectionContext(() =>
      delegatedFirmNavGuard({} as never, { url: '/quotes' } as never)
    );
    expect(result).toBe(true);
  });

  it('blocks delegated firm user on /quotes', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    const result = await TestBed.runInInjectionContext(() =>
      delegatedFirmNavGuard({} as never, { url: '/quotes' } as never)
    );
    expect(result).not.toBe(true);
    const router = TestBed.inject(Router);
    expect(router.serializeUrl(result as never)).toContain('/access-denied');
  });

  it('allows delegated firm user on /invoices', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    const result = await TestBed.runInInjectionContext(() =>
      delegatedFirmNavGuard({} as never, { url: '/invoices' } as never)
    );
    expect(result).toBe(true);
  });

  it('allows delegated firm user on /reports/sales', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    const result = await TestBed.runInInjectionContext(() =>
      delegatedFirmNavGuard({} as never, { url: '/reports/sales' } as never)
    );
    expect(result).toBe(true);
  });

  it('blocks delegated firm user on /reports/profit', async () => {
    const auth = TestBed.inject(AuthService);
    setUser(auth, delegatedUser);
    const result = await TestBed.runInInjectionContext(() =>
      delegatedFirmNavGuard({} as never, { url: '/reports/profit' } as never)
    );
    expect(result).not.toBe(true);
  });
});
